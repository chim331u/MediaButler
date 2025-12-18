-- Batch Job Processing Tables
-- These tables support background batch file organization operations
-- Designed for ARM32 with SQLite (no Redis dependency)

-- Batch Jobs Table
-- Stores high-level batch job information and status
CREATE TABLE IF NOT EXISTS batch_jobs (
    id TEXT PRIMARY KEY,                    -- UUID v4
    batch_name TEXT NOT NULL,               -- User-friendly name
    status TEXT NOT NULL,                   -- Queued, Processing, Completed, Failed, Cancelled
    queued_at DATETIME NOT NULL,            -- When job was created
    started_at DATETIME,                    -- When job execution started
    completed_at DATETIME,                  -- When job finished (success or failure)

    -- Progress tracking
    total_files INTEGER NOT NULL,           -- Total files in batch
    processed_files INTEGER DEFAULT 0,      -- Files processed so far
    successful_files INTEGER DEFAULT 0,     -- Successfully organized files
    failed_files INTEGER DEFAULT 0,         -- Failed files

    -- Configuration
    continue_on_error BOOLEAN DEFAULT 0,    -- Whether to continue on individual file errors
    dry_run BOOLEAN DEFAULT 0,              -- Validation only, no actual file movement
    max_concurrency INTEGER DEFAULT 1,      -- Max concurrent operations (ARM32: usually 1)

    -- Retry logic
    retry_count INTEGER DEFAULT 0,          -- Current retry attempt
    max_retries INTEGER DEFAULT 3,          -- Maximum retry attempts
    next_retry_at DATETIME,                 -- When to retry if failed

    -- Metadata and errors
    metadata TEXT,                          -- JSON blob for additional info
    error_message TEXT,                     -- Error description if failed

    -- Audit fields (BaseEntity pattern)
    created_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_update_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Batch Job Items Table
-- Stores individual file operations within a batch
CREATE TABLE IF NOT EXISTS batch_job_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    batch_job_id TEXT NOT NULL,             -- FK to batch_jobs.id

    -- File identification
    file_hash TEXT NOT NULL,                -- SHA256 hash of file
    confirmed_category TEXT NOT NULL,       -- Target category for organization
    custom_target_path TEXT,                -- Optional custom path override

    -- Status
    status TEXT NOT NULL,                   -- Pending, Processing, Completed, Failed

    -- Results
    target_path TEXT,                       -- Generated target path
    actual_path TEXT,                       -- Actual path after resolution (conflicts, etc.)
    error_message TEXT,                     -- Error if failed
    processing_time_ms INTEGER,             -- Time taken to process this file
    processed_at DATETIME,                  -- When file was processed

    -- Metadata
    metadata TEXT,                          -- JSON blob for file-specific info

    -- Audit fields
    created_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (batch_job_id) REFERENCES batch_jobs(id) ON DELETE CASCADE
);

-- Indexes for performance

-- Query jobs by status and queued time (scheduler polling)
CREATE INDEX IF NOT EXISTS idx_batch_jobs_status_queued
ON batch_jobs(status, queued_at DESC);

-- Query jobs by next retry time (retry scheduler)
CREATE INDEX IF NOT EXISTS idx_batch_jobs_next_retry
ON batch_jobs(next_retry_at)
WHERE status = 'Queued' AND next_retry_at IS NOT NULL;

-- Recent jobs query (monitoring)
CREATE INDEX IF NOT EXISTS idx_batch_jobs_queued_at
ON batch_jobs(queued_at DESC);

-- Job items by batch (fetch all items for a job)
CREATE INDEX IF NOT EXISTS idx_batch_job_items_batch_id
ON batch_job_items(batch_job_id);

-- Job items by status (query pending items)
CREATE INDEX IF NOT EXISTS idx_batch_job_items_batch_status
ON batch_job_items(batch_job_id, status);

-- File hash lookup (validation)
CREATE INDEX IF NOT EXISTS idx_batch_job_items_file_hash
ON batch_job_items(file_hash);
