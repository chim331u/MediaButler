# FileCat Migration Tool - Final Configuration Summary

## Migration Logic (Simplified)

### Category Mapping - CORRECTED ✅

**Key Principle**: FileCat contains **manual user categorizations** that should be preserved as the authoritative category.

```
FileCat.filecategory → TrackedFiles.Category (always, if present)
                     → TrackedFiles.SuggestedCategory = null (ML calculates later)
                     → TrackedFiles.Confidence = 0.0 (ML calculates later)
```

### Why This Approach?

1. **Manual Categorization is Authoritative**: FileCat categories were set manually by users
2. **ML Validation**: ML can run later and populate `SuggestedCategory` for comparison
3. **Quality Control**: Allows comparing manual vs ML categories to improve ML model
4. **Training Data**: Provides ground truth for ML model training

## Field Mapping Quick Reference

| FileCat Field | MediaButler Field | Value | Notes |
|--------------|-------------------|-------|-------|
| `filecategory` | **Category** | Original casing | ✅ Always migrated |
| - | **SuggestedCategory** | `null` | ⚠️ ML populates later |
| - | **Confidence** | `0.0` | ⚠️ ML populates later |
| `IsToCategorize=0` + category | **Status** | 5 (Moved) | Already organized |
| Other active files | **Status** | 2 (Classified) | Awaiting confirmation |
| `IsNotToMove=1` | **Status** | 8 (Ignored) | Never process |

## Post-Migration Workflow

### Step 1: Run Migration
```bash
cd /Users/luca/temp/MediaButler/scripts
dotnet run --project MigrationTool.csproj -- --dry-run  # Preview
dotnet run --project MigrationTool.csproj -- --live     # Execute
```

### Step 2: Verify Migration
```sql
-- Check categories are populated
SELECT COUNT(*) as Total,
       SUM(CASE WHEN Category IS NOT NULL THEN 1 ELSE 0 END) as WithCategory,
       SUM(CASE WHEN SuggestedCategory IS NULL THEN 1 ELSE 0 END) as NoMLSuggestion
FROM TrackedFiles;
```

Expected:
- `WithCategory` = count of files with FileCat category
- `NoMLSuggestion` = Total (all should be null after migration)

### Step 3: Run ML Classification

After migration, MediaButler ML system should:

```csharp
// Pseudocode for ML processing
foreach (var file in trackedFiles) {
    if (file.Category != null) {
        // Manual category exists, run ML for comparison
        var mlResult = classificationService.Classify(file.FileName);

        file.SuggestedCategory = mlResult.Category;
        file.Confidence = mlResult.Confidence;

        // Optional: Log discrepancies for model improvement
        if (file.Category != file.SuggestedCategory) {
            logger.LogWarning($"Manual: {file.Category}, ML: {file.SuggestedCategory}");
        }
    }
}
```

### Step 4: Analyze ML vs Manual

```sql
-- Find discrepancies between manual and ML categories
SELECT
    FileName,
    Category as ManualCategory,
    SuggestedCategory as MLCategory,
    Confidence,
    CASE
        WHEN Category = SuggestedCategory THEN 'Match'
        ELSE 'Mismatch'
    END as Result
FROM TrackedFiles
WHERE Category IS NOT NULL AND SuggestedCategory IS NOT NULL
ORDER BY Confidence DESC
LIMIT 50;
```

## Database State After Migration

### Immediately After Migration

| Field | State | Reason |
|-------|-------|--------|
| `Category` | Populated from FileCat | Manual categorization preserved |
| `SuggestedCategory` | `NULL` | ML not run yet |
| `Confidence` | `0.0` | ML not run yet |
| `Status` | 2, 5, or 8 | Based on FileCat flags |
| `MovedAt` | Set for Status=5 | Current timestamp |

### After ML Processing

| Field | State | Reason |
|-------|-------|--------|
| `Category` | Unchanged | Manual categorization preserved |
| `SuggestedCategory` | Populated | ML classification result |
| `Confidence` | 0.0-1.0 | ML confidence score |
| `Status` | Unchanged | From migration |

## Benefits of This Approach

1. ✅ **Preserves Manual Work**: FileCat categorizations are not lost
2. ✅ **ML Validation**: Can compare manual vs ML for quality control
3. ✅ **Training Data**: Manual categories serve as ground truth
4. ✅ **Flexible**: Can choose to trust manual or ML category per file
5. ✅ **Audit Trail**: Can see both categorizations side-by-side

## Example Scenarios

### Scenario 1: ML Agrees with Manual
```
Category: "Breaking Bad"
SuggestedCategory: "Breaking Bad" (after ML)
Confidence: 0.95
Result: ✅ ML validation confirms manual categorization
```

### Scenario 2: ML Suggests Different Category
```
Category: "The Office"
SuggestedCategory: "Parks and Recreation" (after ML)
Confidence: 0.82
Result: ⚠️ Review needed - possible miscategorization or similar shows
```

### Scenario 3: Low Confidence ML
```
Category: "Stranger Things"
SuggestedCategory: "Stranger Things" (after ML)
Confidence: 0.45
Result: ℹ️ ML agrees but low confidence - may need more training data
```

## Verification Queries

### 1. Check Migration Completeness
```sql
SELECT
    COUNT(*) as TotalFiles,
    SUM(CASE WHEN Category IS NOT NULL THEN 1 ELSE 0 END) as FilesWithCategory,
    AVG(CASE WHEN Category IS NOT NULL THEN 1.0 ELSE 0.0 END) * 100 as CategoryPercentage
FROM TrackedFiles;
```

### 2. Check ML Fields Are Null (Pre-ML)
```sql
SELECT
    'Pre-ML Check' as Phase,
    COUNT(*) as Total,
    SUM(CASE WHEN SuggestedCategory IS NULL THEN 1 ELSE 0 END) as NullSuggestions,
    SUM(CASE WHEN Confidence = 0 THEN 1 ELSE 0 END) as ZeroConfidence
FROM TrackedFiles;
```

### 3. Category Distribution by Status
```sql
SELECT
    Status,
    CASE Status
        WHEN 2 THEN 'Classified'
        WHEN 5 THEN 'Moved'
        WHEN 8 THEN 'Ignored'
    END as StatusName,
    COUNT(*) as FileCount,
    COUNT(DISTINCT Category) as UniqueCategories
FROM TrackedFiles
WHERE Category IS NOT NULL
GROUP BY Status
ORDER BY Status;
```

## Common Questions

**Q: Why not populate SuggestedCategory from FileCat?**
A: FileCat categories are manual (authoritative). ML suggestions should be calculated independently for validation.

**Q: Why set Confidence to 0.0 instead of 1.0 for manual categorization?**
A: Confidence is an ML metric. Manual categorizations don't have confidence scores - they're authoritative.

**Q: What if Category and SuggestedCategory differ?**
A: This is valuable feedback! It indicates:
- Potential manual miscategorization
- ML model needs improvement
- Edge cases requiring human review

**Q: Should I trust Category or SuggestedCategory?**
A: **Category** (manual) is authoritative for migrated data. Use SuggestedCategory for validation and improvement.

---

**Migration Tool Version**: 1.0.6
**Last Updated**: October 1, 2025
**Build Status**: ✅ Successful (0 errors)
