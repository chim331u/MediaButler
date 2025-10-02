# FileCat Migration Tool - Changelog

## Version 1.0.6 (October 2025)

### Changes Made

#### 1. **Category Preservation** ✅
- **Previous Behavior**: Categories were converted to UPPERCASE (e.g., "breaking bad" → "BREAKING BAD")
- **New Behavior**: Categories are preserved with original casing from FileCat database
- **Affected Fields**:
  - `Category`: Now saves as-is from `filecategory` field
  - `SuggestedCategory`: Now saves as-is from `filecategory` field
- **Reason**: Maintain data consistency with original FileCat categorization

**Code Changes**:
```csharp
// Before:
SuggestedCategory = source.FileCategory.ToUpperInvariant()
Category = source.FileCategory.ToUpperInvariant()

// After:
SuggestedCategory = source.FileCategory  // Keep original casing
Category = source.FileCategory           // Keep original casing
```

#### 2. **MovedAt Timestamp Addition** ✅
- **Previous Behavior**: `MovedAt` field was set to `DBNull.Value` for all records
- **New Behavior**: `MovedAt` is set to current UTC date/time for files with Status.Moved (5)
- **Logic**:
  - Status = 5 (Moved) → `MovedAt = DateTime.UtcNow`
  - Status = 2 (Classified) → `MovedAt = null`
  - Status = 8 (Ignored) → `MovedAt = null`
- **Reason**: Track when files were organized into final location

**Code Changes**:
```csharp
// Before:
insertCommand.Parameters.AddWithValue("@MovedAt", DBNull.Value);

// After:
insertCommand.Parameters.AddWithValue("@MovedAt", (object)trackedFile.MovedAt ?? DBNull.Value);

// Mapping logic:
MovedAt = status == 5 ? DateTime.UtcNow : (DateTime?)null
```

#### 3. **TrackedFileRecord Model Update** ✅
- Added `MovedAt` property to `TrackedFileRecord` class
- Type: `DateTime?` (nullable DateTime)
- Used in mapping and database insertion

**Code Changes**:
```csharp
public class TrackedFileRecord
{
    // ... existing properties ...
    public DateTime? ClassifiedAt { get; set; }
    public DateTime? MovedAt { get; set; }  // NEW
    public string? LastError { get; set; }
    // ... remaining properties ...
}
```

### Migration Behavior Summary

#### Status-Based MovedAt Logic (Updated)

| FileCat Condition | MediaButler Status | MovedAt Value | Category Value |
|------------------|-------------------|---------------|----------------|
| `IsNotToMove = 1` | Ignored (8) | `null` | `null` |
| `IsToCategorize = 0` + has category | Moved (5) | `DateTime.UtcNow` | From `filecategory` |
| All other records | Classified (2) | `null` | `null` |

#### Category Mapping (Updated)

**Important**: Category fields are now status-dependent:

| Source Field | Target Field | Condition | Transformation | Example |
|-------------|--------------|-----------|----------------|---------|
| `filecategory` | `Category` | Status = 5 (Moved) only | **None** (preserves original) | "Breaking Bad" → "Breaking Bad" |
| `filecategory` | `SuggestedCategory` | Status = 2 (Classified) only | **None** (preserves original) | "The Office" → "The Office" |

**Logic**:
- **Status.Moved (5)**: `Category` is populated from `filecategory` (files already organized)
- **Status.Classified (2)**: `SuggestedCategory` is populated from `filecategory` (awaiting confirmation)
- **Status.Ignored (8)**: Both `Category` and `SuggestedCategory` are `null`

### Testing Recommendations

Before running live migration with these changes:

1. **Test with Dry Run**:
   ```bash
   cd /Users/luca/temp/MediaButler/scripts
   dotnet run --project MigrationTool.csproj -- --dry-run
   ```

2. **Verify Category Casing**:
   - Check that categories appear with original casing in preview
   - Example output should show: `Category: Breaking Bad` (not `BREAKING BAD`)

3. **Verify MovedAt Timestamps**:
   - Files with Status.Moved (5) should show `MovedAt = [current date]`
   - Files with Status.Classified (2) or Ignored (8) should show `MovedAt = null`

4. **Database Verification**:
   After migration, verify with SQL:
   ```sql
   -- Check category casing preservation
   SELECT DISTINCT Category FROM TrackedFiles ORDER BY Category;

   -- Check MovedAt for Moved files (Status = 5)
   SELECT FileName, Category, Status, MovedAt
   FROM TrackedFiles
   WHERE Status = 5
   LIMIT 10;

   -- Verify Classified files have null MovedAt (Status = 2)
   SELECT FileName, Category, Status, MovedAt
   FROM TrackedFiles
   WHERE Status = 2
   LIMIT 10;
   ```

### Backward Compatibility

These changes are **backward compatible** with MediaButler v1.0.5+:
- All existing database fields are supported
- No schema changes required
- Category casing change is cosmetic (functionality unchanged)
- MovedAt field already exists in TrackedFiles table

### Files Modified

1. `/scripts/FileCatMigrationTool.cs`
   - Line 27: Updated documentation comment
   - Line 277: Changed MovedAt parameter binding
   - Line 336-338: Removed `.ToUpperInvariant()` from category fields
   - Line 341: Added MovedAt logic based on status
   - Line 446: Added MovedAt property to TrackedFileRecord

2. `/scripts/README.md`
   - Line 65: Updated category mapping documentation
   - Line 67: Added MovedAt field mapping
   - Line 86-92: Updated Additional Field Defaults section

### Version Compatibility

| MediaButler Version | Migration Tool Version | Compatible |
|---------------------|----------------------|------------|
| v1.0.5 | v1.0.5 | ✅ Yes |
| v1.0.6 | v1.0.6 | ✅ Yes (this version) |
| v1.0.7 | v1.0.6 | ✅ Yes |

### Notes for Future Versions

- Category casing is now preserved from source database
- If MediaButler Web UI requires specific category casing (e.g., UPPERCASE), add normalization in UI layer, not migration tool
- MovedAt timestamp uses `DateTime.UtcNow` at migration time (not original FileCat date)
- Consider adding command-line option to preserve original moved dates if needed in future

---

**Migration Tool Build**: ✅ Successful (11 warnings, 0 errors)
**Date**: October 1, 2025
**Author**: Claude Code Assistant
