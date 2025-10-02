# FileCat to MediaButler Migration Tool

This tool migrates data from the legacy FileCat database to the new MediaButler system. It safely transfers file tracking data while preserving audit trails and mapping legacy status flags to MediaButler's enhanced workflow states.

## Prerequisites

Before running the migration tool, ensure:

1. **Source Database**: FileCat database (`FileCat.db`) with `FilesDetail` table exists
2. **Target Database**: MediaButler database is initialized (run MediaButler API at least once)
3. **Backup**: Create backups of both databases before migration
4. **.NET 8 SDK**: Installed on your system

## Migration Process

### Step 1: Dry Run (Preview Migration)

**Always run in dry-run mode first** to preview the migration without making any changes:

```bash
cd /Users/luca/temp/MediaButler/scripts
dotnet run --project MigrationTool.csproj -- --dry-run
```

This will:
- Display records from FileCat database that will be migrated
- Show field mappings and status conversions
- Validate source database structure
- **No changes are made to the target database**

### Step 2: Review Output

Examine the dry-run output carefully:
- Verify record counts match expectations
- Check that status mappings are correct
- Ensure category names are properly normalized to UPPERCASE
- Validate file paths and naming conventions

### Step 3: Live Migration

Once satisfied with the dry-run preview, execute the live migration:

```bash
cd /Users/luca/temp/MediaButler/scripts
dotnet run --project MigrationTool.csproj -- --live
```

This will:
- Perform actual data migration from FileCat to MediaButler
- Use transaction-based migration with rollback support on errors
- Migrate all active records (`IsActive = true`)
- Preserve audit trail (CreatedDate, LastUpdateDate)

## Data Mapping

### Field Mappings

| FileCat Field | MediaButler Field | Transformation |
|--------------|-------------------|----------------|
| `id` | Not migrated | Auto-generated in MediaButler |
| `hash` | `Hash` | SHA256 hash (primary key) |
| `name` | `FileName` | Normalized using watch folder method |
| `Path` | `MovedToPath` | Full file path after organization |
| `filesize` | `FileSize` | Migrated as-is (bytes) |
| `filecategory` | `Category` | **Always migrated** (original casing preserved) |
| `lastupdate` | `LastUpdateDate` | Preserved timestamp |
| N/A | `SuggestedCategory` | Set to `null` (ML will calculate) |
| N/A | `Confidence` | Set to `0.0` (ML will calculate) |
| N/A | `MovedAt` | Set to current UTC date for Status.Moved (5) files |
| `IsNotToMove` | `Status` | Mapped to Status enum (see below) |
| `IsToCategorize` | `Status` | Mapped to Status enum (see below) |

### Status Mapping Logic

The migration tool maps FileCat's boolean flags to MediaButler's FileStatus enum:

| FileCat Condition | MediaButler Status | Enum Value | Description |
|------------------|-------------------|------------|-------------|
| `IsNotToMove = 1` | `Ignored` | 8 | Files marked to never move |
| `IsToCategorize = 0` AND has category | `Moved` | 5 | Files already organized and moved |
| All other records | `Classified` | 2 | Files awaiting user confirmation |

### Additional Field Defaults

Fields not present in FileCat are set to MediaButler defaults:

- `OriginalPath`: Set to empty string (legacy data)
- `Category`: **Always migrated** from FileCat `filecategory` (manual categorization)
- `SuggestedCategory`: Set to `null` (ML will calculate suggestions)
- `Confidence`: Set to `0.0` (ML will calculate confidence scores)
- `Status`: Mapped based on FileCat flags (see table above)
- `MovedAt`: Set to current UTC date/time for files with Status.Moved (5)
- `ClassifiedAt`: Set to `LastUpdateDate` for Status.Classified (2) files with category
- `CreatedDate`: Preserved from FileCat `CreatedDate`
- `LastUpdateDate`: Preserved from FileCat `lastupdate`
- `IsActive`: Set to `true` (only active records are migrated)

**Important**: The migration preserves FileCat's manual categorization in the `Category` field. The ML system will later populate `SuggestedCategory` and `Confidence` for comparison and validation purposes.

## Examples

### Example 1: Complete Migration Workflow

```bash
# Step 1: Navigate to scripts directory
cd /Users/luca/temp/MediaButler/scripts

# Step 2: Preview migration (dry-run)
dotnet run --project MigrationTool.csproj -- --dry-run

# Review output...
# Expected output:
# Found 1,010 records in FileCat database
# Preview of migration:
# - Breaking.Bad.S01E01.mkv → BREAKING BAD (Status: Moved)
# - The.Office.S02E05.mkv → THE OFFICE (Status: Classified)
# ...

# Step 3: Execute live migration
dotnet run --project MigrationTool.csproj -- --live

# Expected output:
# Successfully migrated 1,010 records from FileCat to MediaButler
```

### Example 2: Build and Run Separately

```bash
# Build the migration tool
dotnet build MigrationTool.csproj

# Run with dry-run flag
dotnet bin/Debug/net8.0/MigrationTool.dll --dry-run

# Run live migration
dotnet bin/Debug/net8.0/MigrationTool.dll --live
```

## Database Paths

The migration tool uses the following default database paths:

- **Source (FileCat)**: `../../data/FileCat.db`
- **Target (MediaButler)**: `../../data/MediaButler.db`

To use custom database paths, modify the paths in `FileCatMigrationTool.cs`:

```csharp
var sourcePath = "/path/to/FileCat.db";
var targetPath = "/path/to/MediaButler.db";
```

## Troubleshooting

### Common Issues

**Issue**: "Source database not found"
- **Solution**: Verify FileCat.db exists at the specified path
- Check file permissions

**Issue**: "Target database not found"
- **Solution**: Run MediaButler API at least once to initialize the database
- Ensure EF Core migrations have been applied

**Issue**: "UNIQUE constraint failed: TrackedFiles.Hash"
- **Solution**: Target database already contains records with the same hash
- Consider clearing target database or handling duplicates manually

**Issue**: Migration fails partway through
- **Solution**: The tool uses transactions - failed migration will rollback automatically
- Check error messages for specific field validation issues
- Verify both databases are not locked by other processes

## Safety Features

- **Dry-Run Mode**: Preview migration without making changes
- **Transaction Support**: All-or-nothing migration with automatic rollback on errors
- **Validation**: Checks database structure before migration
- **Audit Trail**: Preserves original timestamps from FileCat
- **Active Records Only**: Only migrates records where `IsActive = true`

## Migration Statistics

Based on testing with production FileCat database:

- **Source Records**: 1,010 files (September 2024 snapshot)
- **Active Records**: All with `IsActive = true`
- **Status Distribution**:
  - Moved (Status 5): ~850 files
  - Classified (Status 2): ~150 files
  - Ignored (Status 8): ~10 files

## Post-Migration Steps

After successful migration:

1. **Verify Data**: Check MediaButler database for migrated records
   ```bash
   sqlite3 ../../data/MediaButler.db "SELECT COUNT(*) FROM TrackedFiles;"
   ```

2. **Start MediaButler API**: Launch the API to verify migrated data
   ```bash
   cd ../src/MediaButler.API
   dotnet run
   ```

3. **Review Web UI**: Access the Web UI to view migrated files
   - Navigate to `http://localhost:5000` (or configured port)
   - Check "Recent Files" page for migrated data

4. **Backup FileCat.db**: Archive the original FileCat database
   ```bash
   cp ../../data/FileCat.db ../../data/backups/FileCat_pre_migration_$(date +%Y%m%d).db
   ```

## Support

For issues or questions about the migration tool:
- Review FileCat database schema compatibility
- Check MediaButler database initialization
- Verify .NET 8 SDK installation
- Consult MediaButler documentation at `/docs/`

---

**Version**: 1.0.5 (Updated September 2024)
**Compatibility**: FileCat v1.x → MediaButler v1.0.5+
