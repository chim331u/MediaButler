-- MediaButler SQLite Schema
-- Clean schema for SQLC code generation

CREATE TABLE IF NOT EXISTS TrackedFiles (
    -- SHA256 hash of the file content, serves as unique identifier
    Hash TEXT PRIMARY KEY NOT NULL CHECK(length(Hash) = 64),

    -- Original filename including extension
    FileName TEXT NOT NULL CHECK(length(FileName) > 0),

    -- Full path where the file was originally discovered
    OriginalPath TEXT NOT NULL CHECK(length(OriginalPath) > 0),

    -- File size in bytes
    FileSize INTEGER NOT NULL CHECK(FileSize > 0),

    -- Current processing status of the file (0-8)
    Status INTEGER NOT NULL DEFAULT 0 CHECK(Status >= 0 AND Status <= 8),

    -- Category suggested by ML classification
    SuggestedCategory TEXT,

    -- ML classification confidence score (0.0 to 1.0)
    Confidence REAL NOT NULL DEFAULT 0.0 CHECK(Confidence >= 0 AND Confidence <= 1),

    -- Final category confirmed by user or system
    Category TEXT,

    -- Target path for file organization
    TargetPath TEXT,

    -- Actual path where file was moved
    MovedToPath TEXT,

    -- UTC timestamp when ML classification was completed
    ClassifiedAt DATETIME,

    -- UTC timestamp when file was successfully moved
    MovedAt DATETIME,

    -- Most recent error message encountered during processing
    LastError TEXT,

    -- UTC timestamp of the most recent error
    LastErrorAt DATETIME,

    -- Number of processing retry attempts made
    RetryCount INTEGER NOT NULL DEFAULT 0 CHECK(RetryCount >= 0),

    -- UTC timestamp when the entity was created
    CreatedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- UTC timestamp when the entity was last modified
    LastUpdateDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- Optional contextual notes about the entity
    Note TEXT,

    -- Indicates if the entity is active (not soft-deleted)
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK(IsActive IN (0, 1))
);

-- Performance-critical indexes from .NET EF Core migration

-- Primary workflow index: status + active records
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Status_IsActive
    ON TrackedFiles(Status, IsActive)
    WHERE IsActive = 1;

-- Multi-status query optimization (for /api/files/by-statuses endpoint)
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_MultiStatus_Query
    ON TrackedFiles(Status, Category, CreatedDate)
    WHERE IsActive = 1;

-- Classification workflow optimization
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Classification_Workflow
    ON TrackedFiles(Status, Confidence, ClassifiedAt)
    WHERE Status = 2;

-- Organization workflow optimization
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Organization_Workflow
    ON TrackedFiles(Status, Category, MovedAt)
    WHERE Status IN (3, 4, 5);

-- Error monitoring optimization
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Error_Monitoring
    ON TrackedFiles(Status, RetryCount, LastErrorAt)
    WHERE Status IN (6, 7);

-- Original path lookup for duplicate detection
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_OriginalPath
    ON TrackedFiles(OriginalPath);

-- Category statistics optimization
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Category_Stats
    ON TrackedFiles(Category, Status, MovedAt)
    WHERE Category IS NOT NULL;

-- Filename analysis index
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Filename_Analysis
    ON TrackedFiles(FileName, Category, Confidence)
    WHERE Category IS NOT NULL AND Confidence > 0;

-- Performance analytics index
CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Performance_Analytics
    ON TrackedFiles(FileSize, Status, CreatedDate);
