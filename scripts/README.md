# MediaButler FileCat Migration Tools

This directory contains tools to migrate data from a FileCat database to MediaButler's TrackedFiles system.

## Overview

The migration tools help you import existing file metadata from FileCat.db into MediaButler, preserving:
- File names and paths
- File categories and organization status
- Creation and modification dates
- File size information
- Active/deleted status

## Files in this directory

- **`run_migration.sh`** - Interactive bash script with menu-driven migration options
- **`migrate_filecat_data.sql`** - Direct SQL migration script for quick bulk import
- **`FileCatMigrationTool.cs`** - Robust C# migration tool with proper hash generation
- **`README.md`** - This documentation file

## Prerequisites

1. **Source Database**: FileCat.db file should be located at `temp/Import/FileCat.db`
2. **Target Database**: MediaButler database should exist at `temp/mediabutler.dev.db`
   - Run MediaButler API at least once to create the database
3. **Dependencies**:
   - SQLite3 command-line tool
   - .NET 8 SDK (for C# migration tool)

## Migration Methods

### Method 1: Interactive Script (Recommended)

The easiest way to migrate is using the interactive script:

```bash
./scripts/run_migration.sh
```

This script provides a menu with the following options:
1. **SQL Script Migration** - Quick migration with generated hashes
2. **C# Tool Migration** - Robust migration with proper SHA256 hashes
3. **Analyze source data only** - Preview what will be migrated
4. **Show current target database status** - Check existing MediaButler data
5. **Exit**

### Method 2: Direct SQL Migration

For quick bulk migration (generates pseudo-hashes):

```bash
sqlite3 temp/mediabutler.dev.db < scripts/migrate_filecat_data.sql
```

### Method 3: C# Migration Tool

For the most robust migration with proper hash generation:

```bash
cd scripts
dotnet run FileCatMigrationTool.cs -- --dry-run  # Preview migration
dotnet run FileCatMigrationTool.cs -- --live     # Perform migration
```

## Field Mapping

| FileCat.FilesDetail | MediaButler.TrackedFiles | Notes |
|---------------------|-------------------------|--------|
| `Name` | `FileName` | Direct mapping |
| `Path` | `OriginalPath` | Direct mapping |
| `FileSize` | `FileSize` | Direct mapping |
| `FileCategory` | `Category`, `SuggestedCategory` | Maps to both fields |
| `CreatedDate` | `CreatedDate` | Direct mapping |
| `LastUpdatedDate` | `LastUpdateDate` | Direct mapping |
| `IsActive` | `IsActive` | Considering `IsDeleted` flag |
| `Note` | `Note` | Direct mapping |
| `Path + Name` | `Hash` | Generated SHA256 hash |
| Various flags | `Status` | Calculated from `IsDeleted`, `IsToCategorize`, etc. |

## Status Mapping Logic

MediaButler uses numeric status codes. The migration maps FileCat flags as follows:

- `IsDeleted = 1` → Status `6` (Error)
- `IsNotToMove = 1` → Status `3` (ConfirmedCategory)
- `FileCategory exists` → Status `3` (ConfirmedCategory)
- `IsToCategorize = 1` → Status `1` (Discovered)
- Default → Status `1` (Discovered)

## Important Notes

### Hash Generation
- **SQL Migration**: Generates pseudo-hashes from file path + name
- **C# Tool**: Generates proper SHA256 hashes from file path + name
- **Limitation**: Without access to actual file content, these are not true content hashes

### Post-Migration Steps
1. **Re-scan files**: Run MediaButler file discovery to generate proper content-based SHA256 hashes
2. **Verify data**: Check that categories and statuses look correct
3. **Run ML classification**: Re-classify files using MediaButler's ML engine if needed

### Data Safety
- **Backups**: Both tools create automatic backups before migration
- **Dry run**: Always test with dry run mode first
- **Transaction safety**: C# tool uses database transactions for atomic operations

## Example Migration Workflow

1. **Prepare databases**:
   ```bash
   # Ensure source database exists
   ls temp/Import/FileCat.db

   # Ensure target database exists (run MediaButler API once)
   ls temp/mediabutler.dev.db
   ```

2. **Analyze source data**:
   ```bash
   ./scripts/run_migration.sh
   # Choose option 3: "Analyze source data only"
   ```

3. **Perform migration**:
   ```bash
   ./scripts/run_migration.sh
   # Choose option 2: "C# Tool Migration" (recommended)
   # Follow prompts for dry run, then live migration
   ```

4. **Verify results**:
   ```bash
   ./scripts/run_migration.sh
   # Choose option 4: "Show current target database status"
   ```

5. **Post-migration setup**:
   - Start MediaButler API
   - Run file discovery to scan watch folders
   - Verify migrated files appear in the web UI

## Troubleshooting

### Common Issues

**Database not found**:
- Ensure FileCat.db is in the correct location
- Run MediaButler API at least once to create target database

**Permission errors**:
- Ensure scripts have execute permissions: `chmod +x scripts/run_migration.sh`
- Check database file permissions

**Duplicate hash errors**:
- The hash generation may create duplicates for very similar file paths
- Use the C# tool which has better hash generation logic

**Missing categories**:
- Some FileCat categories may not map perfectly to MediaButler
- Review and adjust categories in MediaButler web UI after migration

### Validation Queries

Check migration success with these SQL queries:

```sql
-- Count migrated records
SELECT COUNT(*) FROM TrackedFiles;

-- Check status distribution
SELECT Status, COUNT(*) FROM TrackedFiles GROUP BY Status;

-- Check categories
SELECT Category, COUNT(*) FROM TrackedFiles
WHERE Category IS NOT NULL GROUP BY Category;

-- Sample migrated data
SELECT FileName, Category, Status, CreatedDate
FROM TrackedFiles ORDER BY CreatedDate DESC LIMIT 10;
```

## Support

If you encounter issues:
1. Check the troubleshooting section above
2. Review migration logs for error messages
3. Verify database schemas match expectations
4. Consider running a smaller test migration first

The migration tools are designed to be safe and reversible, with automatic backups and dry-run capabilities.