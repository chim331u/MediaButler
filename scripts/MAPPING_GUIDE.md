# FileCat to MediaButler - Field Mapping Guide

## Overview

This guide documents the complete field mapping logic used by the FileCat migration tool, including the status-dependent category assignments.

## Status Mapping Logic

### Primary Status Determination

The migration tool determines the MediaButler status based on FileCat flags:

```
IF IsNotToMove = 1
    → Status = 8 (Ignored)

ELSE IF IsToCategorize = 0 AND has filecategory
    → Status = 5 (Moved)

ELSE
    → Status = 2 (Classified)
```

### Status Descriptions

| Status | Value | FileCat Condition | Meaning in MediaButler |
|--------|-------|-------------------|------------------------|
| **Ignored** | 8 | `IsNotToMove = 1` | Files marked to never be moved/processed |
| **Moved** | 5 | `IsToCategorize = 0` + has category | Files already organized to final location |
| **Classified** | 2 | All other active records | Files awaiting user confirmation before moving |

## Category Field Mapping (Simple - Always from FileCat)

### Universal Category Migration

**All files with `filecategory` get their category migrated directly**:

| Source Field | Target Field | Value | Example |
|-------------|--------------|-------|---------|
| `filecategory` | `Category` | Original casing preserved | "Breaking Bad" |
| N/A | `SuggestedCategory` | `null` | - |
| N/A | `Confidence` | `0.0` | - |

**Rationale**:
- FileCat categories are **manual categorizations** by the user
- These should be preserved in `Category` field (confirmed category)
- ML will later calculate `SuggestedCategory` and `Confidence` for validation/comparison
- This allows MediaButler to compare manual vs ML classifications

### Status-Specific Fields

#### For Status = 5 (Moved)
- `Category`: From `filecategory` (preserved)
- `MovedAt`: `DateTime.UtcNow` (current timestamp)
- `ClassifiedAt`: `null`

#### For Status = 2 (Classified)
- `Category`: From `filecategory` (preserved)
- `ClassifiedAt`: From `LastUpdateDate`
- `MovedAt`: `null`

#### For Status = 8 (Ignored)
- `Category`: From `filecategory` if present, otherwise `null`
- `ClassifiedAt`: `null`
- `MovedAt`: `null`

## Complete Field Mapping Table

### Direct Mappings

| FileCat Field | MediaButler Field | Transformation | Notes |
|--------------|-------------------|----------------|-------|
| `hash` (generated) | `Hash` | SHA256 from path+name | Primary key |
| `name` | `FileName` | Refactored via watch folder method | Normalized filename |
| `Path` | `MovedToPath` | Direct copy | Final file location |
| `filesize` | `FileSize` | Cast to `long` | Bytes |
| `LastUpdatedDate` | `LastUpdateDate` | Direct copy | Audit trail |
| `CreatedDate` | `CreatedDate` | Direct copy | Audit trail |
| `Note` | `Note` | Direct copy | User notes |
| `IsActive` | `IsActive` | `IsActive && !IsDeleted` | Active and not deleted |

### Conditional Mappings

| FileCat Field | MediaButler Field | Condition | Value |
|--------------|-------------------|-----------|-------|
| `filecategory` | `Category` | Has category | Original casing (always) |
| - | `SuggestedCategory` | Always | `null` (ML calculates) |
| `LastUpdatedDate` | `ClassifiedAt` | Status = 2 + has category | Original timestamp |
| - | `MovedAt` | Status = 5 | `DateTime.UtcNow` |
| - | `Confidence` | Always | `0.0` (ML calculates) |

### Fixed/Default Values

| MediaButler Field | Value | Reason |
|-------------------|-------|--------|
| `OriginalPath` | `""` (empty) | Legacy data - watch folder path unknown |
| `TargetPath` | `filecategory + filename` | Constructed path for organization |
| `RetryCount` | `0` | No retries needed for migrated data |
| `LastError` | `"File marked as deleted..."` (if deleted) | Only for deleted files |
| `LastErrorAt` | `LastUpdatedDate` (if deleted) | Only for deleted files |

## Examples

### Example 1: Already Organized File

**FileCat Record**:
```
Name: "Breaking.Bad.S01E01.mkv"
Path: "/library/Breaking Bad/Breaking.Bad.S01E01.mkv"
FileCategory: "Breaking Bad"
IsToCategorize: 0
IsNotToMove: 0
```

**MediaButler Record**:
```
FileName: "Breaking Bad S01E01 mkv"
MovedToPath: "/library/Breaking Bad/Breaking.Bad.S01E01.mkv"
Status: 5 (Moved)
Category: "Breaking Bad"           ← From FileCat manual categorization
SuggestedCategory: null            ← ML will populate later
Confidence: 0.0                    ← ML will populate later
MovedAt: 2025-10-01T19:30:00Z (current time)
ClassifiedAt: null
```

### Example 2: File Awaiting Confirmation

**FileCat Record**:
```
Name: "The.Office.S02E05.mkv"
Path: "/watch/The.Office.S02E05.mkv"
FileCategory: "The Office"
IsToCategorize: 1
IsNotToMove: 0
```

**MediaButler Record**:
```
FileName: "The Office S02E05 mkv"
MovedToPath: "/watch/The.Office.S02E05.mkv"
Status: 2 (Classified)
Category: "The Office"             ← From FileCat manual categorization
SuggestedCategory: null            ← ML will populate later
Confidence: 0.0                    ← ML will populate later
MovedAt: null
ClassifiedAt: 2024-09-15T10:00:00Z (from FileCat)
```

### Example 3: Ignored File

**FileCat Record**:
```
Name: "Sample.File.mkv"
Path: "/watch/Sample.File.mkv"
FileCategory: null
IsToCategorize: 0
IsNotToMove: 1
```

**MediaButler Record**:
```
FileName: "Sample File mkv"
MovedToPath: "/watch/Sample.File.mkv"
Status: 8 (Ignored)
Category: null                     ← No category in FileCat
SuggestedCategory: null            ← ML will populate later
Confidence: 0.0                    ← ML will populate later
MovedAt: null
ClassifiedAt: null
```

## Database Verification Queries

After migration, verify the mapping with these SQL queries:

### Verify Status Distribution
```sql
SELECT Status, COUNT(*) as Count
FROM TrackedFiles
GROUP BY Status
ORDER BY Status;
```

Expected results:
- Status 2 (Classified): Files with `IsToCategorize = 1`
- Status 5 (Moved): Files with `IsToCategorize = 0` and category
- Status 8 (Ignored): Files with `IsNotToMove = 1`

### Verify Category Mapping for All Files
```sql
SELECT FileName, Category, SuggestedCategory, Confidence, Status
FROM TrackedFiles
WHERE Category IS NOT NULL
LIMIT 20;
```

Expected:
- `Category` populated from FileCat
- `SuggestedCategory` = `null` (ML will calculate)
- `Confidence` = `0.0` (ML will calculate)

### Verify ML Fields Are Null
```sql
SELECT COUNT(*) as TotalFiles,
       SUM(CASE WHEN Category IS NOT NULL THEN 1 ELSE 0 END) as WithCategory,
       SUM(CASE WHEN SuggestedCategory IS NOT NULL THEN 1 ELSE 0 END) as WithSuggestion,
       SUM(CASE WHEN Confidence > 0 THEN 1 ELSE 0 END) as WithConfidence
FROM TrackedFiles;
```

Expected:
- `WithCategory` > 0 (migrated from FileCat)
- `WithSuggestion` = 0 (ML not run yet)
- `WithConfidence` = 0 (ML not run yet)

### Verify Category Distribution
```sql
SELECT Category, COUNT(*) as FileCount, Status
FROM TrackedFiles
WHERE Category IS NOT NULL
GROUP BY Category, Status
ORDER BY FileCount DESC
LIMIT 10;
```

Expected: Shows FileCat manual categorization distribution by status

## Notes

1. **Category Always Migrated**: `Category` field is populated from FileCat's manual categorization (original casing preserved)
2. **ML Fields Null**: `SuggestedCategory` and `Confidence` are set to null/0.0 for ML to populate later
3. **Two-Layer Validation**: Manual category vs ML suggestion allows validation and quality checking
4. **Timestamps**: `MovedAt` uses current migration time, not original FileCat date
5. **Hash Generation**: Uses path + name combination (not actual file content)
6. **Active Records Only**: Only `IsActive = 1` records are migrated

## Post-Migration ML Processing

After migration, MediaButler's ML system should:

1. **Run Classification**: Process all migrated files through ML classification
2. **Populate SuggestedCategory**: ML predictions go into `SuggestedCategory` field
3. **Populate Confidence**: ML confidence scores go into `Confidence` field
4. **Compare Results**: System can compare manual `Category` vs ML `SuggestedCategory`
5. **Identify Discrepancies**: Find files where manual and ML categories differ for review

---

**Last Updated**: October 1, 2025
**Tool Version**: 1.0.6
**Compatible with**: MediaButler v1.0.7+
