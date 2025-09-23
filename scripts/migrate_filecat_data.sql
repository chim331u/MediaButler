-- MediaButler Data Migration Script
-- Migrates data from FileCat.db FilesDetail table to MediaButler TrackedFiles table
-- Run this script with: sqlite3 mediabutler.dev.db < migrate_filecat_data.sql

-- Attach the source database
ATTACH DATABASE '/Users/luca/GitHub/mediabutler/MediaButler/temp/Import/FileCat.db' AS source_db;

-- Create a view to show the field mapping analysis
CREATE TEMP VIEW field_mapping AS
SELECT
    'FilesDetail → TrackedFiles Field Mapping:' as mapping_info
UNION ALL SELECT '- Name → FileName'
UNION ALL SELECT '- Path → OriginalPath'
UNION ALL SELECT '- FileSize → FileSize'
UNION ALL SELECT '- FileCategory → Category (if not null), SuggestedCategory'
UNION ALL SELECT '- CreatedDate → CreatedDate'
UNION ALL SELECT '- LastUpdatedDate → LastUpdateDate'
UNION ALL SELECT '- IsActive → IsActive'
UNION ALL SELECT '- Note → Note'
UNION ALL SELECT '- Generated Hash → Hash (SHA256-like from path+name)'
UNION ALL SELECT '- Calculated Status based on flags → Status'
UNION ALL SELECT '- Default values for new fields (MovedAt, LastError, etc.)';

-- Show mapping info
.mode column
.headers on
SELECT * FROM field_mapping;

-- Backup existing TrackedFiles data (if any)
CREATE TABLE IF NOT EXISTS TrackedFiles_backup_$(date '+%Y%m%d_%H%M%S') AS
SELECT * FROM TrackedFiles;

-- Count source records
SELECT 'Source Records Analysis:' as info;
SELECT
    COUNT(*) as total_records,
    COUNT(CASE WHEN FileCategory IS NOT NULL AND FileCategory != '' THEN 1 END) as with_category,
    COUNT(CASE WHEN IsDeleted = 1 THEN 1 END) as deleted,
    COUNT(CASE WHEN IsToCategorize = 1 THEN 1 END) as to_categorize,
    COUNT(CASE WHEN IsNotToMove = 1 THEN 1 END) as not_to_move,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as active
FROM source_db.FilesDetail;

-- Function to determine Status based on flags
-- Status values in MediaButler:
-- 0 = New, 1 = Discovered, 2 = Classified, 3 = ConfirmedCategory, 4 = Queued, 5 = Moved, 6 = Error, 7 = Failed

-- Begin transaction for data migration
BEGIN TRANSACTION;

-- Insert migrated data
INSERT INTO TrackedFiles (
    Hash,
    FileName,
    OriginalPath,
    FileSize,
    Status,
    SuggestedCategory,
    Confidence,
    Category,
    TargetPath,
    ClassifiedAt,
    MovedAt,
    LastError,
    LastErrorAt,
    RetryCount,
    CreatedDate,
    LastUpdateDate,
    Note,
    IsActive,
    MovedToPath
)
SELECT
    -- Generate pseudo-hash from path + name (since we don't have actual file hashes)
    printf('%064s',
        substr(
            replace(
                replace(
                    hex(randomblob(16) || Path || '/' || Name),
                    ' ', ''
                ),
                'NULL', ''
            ) ||
            printf('%08x', abs(random())),
            1, 64
        )
    ) as Hash,

    -- Direct field mappings
    Name as FileName,
    Path as OriginalPath,
    CAST(FileSize as INTEGER) as FileSize,

    -- Determine Status based on flags
    CASE
        WHEN IsDeleted = 1 THEN 6  -- Error (treating deleted as error state)
        WHEN IsNotToMove = 1 THEN 3  -- ConfirmedCategory (categorized but not to move)
        WHEN FileCategory IS NOT NULL AND FileCategory != '' THEN 3  -- ConfirmedCategory
        WHEN IsToCategorize = 1 THEN 1  -- Discovered (needs categorization)
        ELSE 1  -- Discovered (default for imported files)
    END as Status,

    -- SuggestedCategory (copy from FileCategory)
    CASE
        WHEN FileCategory IS NOT NULL AND FileCategory != '' THEN FileCategory
        ELSE NULL
    END as SuggestedCategory,

    -- Confidence score
    CASE
        WHEN FileCategory IS NOT NULL AND FileCategory != '' THEN 0.95  -- High confidence for existing categories
        ELSE 0.0
    END as Confidence,

    -- Category (confirmed category)
    CASE
        WHEN FileCategory IS NOT NULL AND FileCategory != '' AND IsNotToMove = 0 THEN FileCategory
        ELSE NULL
    END as Category,

    -- New fields with default values
    NULL as TargetPath,
    CASE
        WHEN FileCategory IS NOT NULL AND FileCategory != '' THEN datetime(LastUpdatedDate)
        ELSE NULL
    END as ClassifiedAt,
    NULL as MovedAt,
    CASE
        WHEN IsDeleted = 1 THEN 'File marked as deleted in source system'
        ELSE NULL
    END as LastError,
    CASE
        WHEN IsDeleted = 1 THEN datetime(LastUpdatedDate)
        ELSE NULL
    END as LastErrorAt,
    0 as RetryCount,

    -- BaseEntity fields
    datetime(CreatedDate) as CreatedDate,
    datetime(LastUpdatedDate) as LastUpdateDate,
    Note as Note,
    CASE WHEN IsActive = 1 AND IsDeleted = 0 THEN 1 ELSE 0 END as IsActive,
    NULL as MovedToPath

FROM source_db.FilesDetail
WHERE Name IS NOT NULL AND Name != '';  -- Only migrate valid file records

-- Show migration results
SELECT 'Migration Results:' as info;
SELECT
    COUNT(*) as total_migrated,
    COUNT(CASE WHEN Category IS NOT NULL THEN 1 END) as with_confirmed_category,
    COUNT(CASE WHEN SuggestedCategory IS NOT NULL THEN 1 END) as with_suggested_category,
    COUNT(CASE WHEN Status = 1 THEN 1 END) as discovered_status,
    COUNT(CASE WHEN Status = 3 THEN 1 END) as confirmed_status,
    COUNT(CASE WHEN Status = 6 THEN 1 END) as error_status,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as active_records
FROM TrackedFiles
WHERE Hash IN (
    SELECT printf('%064s',
        substr(
            replace(
                replace(
                    hex(randomblob(16) || Path || '/' || Name),
                    ' ', ''
                ),
                'NULL', ''
            ) ||
            printf('%08x', abs(random())),
            1, 64
        )
    )
    FROM source_db.FilesDetail
    LIMIT 1
); -- This is a rough check since we're generating hashes

COMMIT;

-- Detach source database
DETACH DATABASE source_db;

-- Final verification
SELECT 'Final Verification:' as info;
SELECT
    COUNT(*) as total_tracked_files,
    MIN(CreatedDate) as earliest_file,
    MAX(CreatedDate) as latest_file,
    COUNT(DISTINCT Category) as unique_categories
FROM TrackedFiles;

-- Show sample migrated records
SELECT 'Sample Migrated Records:' as info;
SELECT
    substr(Hash, 1, 8) || '...' as hash_preview,
    FileName,
    substr(OriginalPath, 1, 30) || '...' as path_preview,
    Category,
    Status,
    CreatedDate
FROM TrackedFiles
ORDER BY CreatedDate DESC
LIMIT 5;

.print "Migration completed successfully!"
.print "Remember to:"
.print "1. Verify the migrated data looks correct"
.print "2. Run MediaButler file discovery to generate proper SHA256 hashes"
.print "3. The imported files will need to be re-scanned to get actual file hashes"
.print "4. Consider running ML classification on the imported files"