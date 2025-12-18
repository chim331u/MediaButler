-- Batch Jobs SQL Queries
-- SQLC will generate type-safe Go code from these queries

-- ============================================================================
-- BATCH JOB QUERIES
-- ============================================================================

-- name: CreateBatchJob :exec
INSERT INTO batch_jobs (
    id,
    batch_name,
    status,
    queued_at,
    total_files,
    processed_files,
    successful_files,
    failed_files,
    continue_on_error,
    dry_run,
    max_concurrency,
    retry_count,
    max_retries,
    metadata,
    created_date,
    last_update_date
) VALUES (
    ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
);

-- name: GetBatchJobByID :one
SELECT * FROM batch_jobs
WHERE id = ?
LIMIT 1;

-- name: UpdateBatchJobStatus :exec
UPDATE batch_jobs
SET status = ?,
    last_update_date = ?
WHERE id = ?;

-- name: UpdateBatchJobStarted :exec
UPDATE batch_jobs
SET status = 'Processing',
    started_at = ?,
    last_update_date = ?
WHERE id = ?;

-- name: UpdateBatchJobProgress :exec
UPDATE batch_jobs
SET processed_files = ?,
    successful_files = ?,
    failed_files = ?,
    last_update_date = ?
WHERE id = ?;

-- name: UpdateBatchJobCompleted :exec
UPDATE batch_jobs
SET status = ?,
    completed_at = ?,
    processed_files = ?,
    successful_files = ?,
    failed_files = ?,
    last_update_date = ?
WHERE id = ?;

-- name: UpdateBatchJobFailed :exec
UPDATE batch_jobs
SET status = 'Failed',
    completed_at = ?,
    error_message = ?,
    retry_count = ?,
    next_retry_at = ?,
    last_update_date = ?
WHERE id = ?;

-- name: UpdateBatchJobCancelled :exec
UPDATE batch_jobs
SET status = 'Cancelled',
    completed_at = ?,
    last_update_date = ?
WHERE id = ?;

-- name: GetQueuedBatchJobs :many
SELECT * FROM batch_jobs
WHERE status = 'Queued'
  AND (next_retry_at IS NULL OR next_retry_at <= datetime('now'))
ORDER BY queued_at ASC
LIMIT ?;

-- name: ListBatchJobs :many
SELECT * FROM batch_jobs
WHERE (sqlc.narg('status') IS NULL OR status = sqlc.narg('status'))
ORDER BY queued_at DESC
LIMIT ? OFFSET ?;

-- name: CountBatchJobsByStatus :one
SELECT COUNT(*) FROM batch_jobs
WHERE status = ?;

-- name: GetBatchJobStatistics :one
SELECT
    COUNT(*) as total_jobs,
    SUM(CASE WHEN status = 'Queued' THEN 1 ELSE 0 END) as queued_jobs,
    SUM(CASE WHEN status = 'Processing' THEN 1 ELSE 0 END) as processing_jobs,
    SUM(CASE WHEN status = 'Completed' THEN 1 ELSE 0 END) as completed_jobs,
    SUM(CASE WHEN status = 'Failed' THEN 1 ELSE 0 END) as failed_jobs,
    SUM(CASE WHEN status = 'Cancelled' THEN 1 ELSE 0 END) as cancelled_jobs
FROM batch_jobs;

-- name: DeleteOldBatchJobs :exec
DELETE FROM batch_jobs
WHERE status IN ('Completed', 'Failed', 'Cancelled')
  AND completed_at < datetime('now', '-30 days');

-- ============================================================================
-- BATCH JOB ITEMS QUERIES
-- ============================================================================

-- name: CreateBatchJobItem :exec
INSERT INTO batch_job_items (
    batch_job_id,
    file_hash,
    confirmed_category,
    custom_target_path,
    status,
    metadata,
    created_date
) VALUES (
    ?, ?, ?, ?, ?, ?, ?
);

-- name: GetBatchJobItems :many
SELECT * FROM batch_job_items
WHERE batch_job_id = ?
ORDER BY id ASC;

-- name: GetBatchJobItemsByStatus :many
SELECT * FROM batch_job_items
WHERE batch_job_id = ?
  AND status = ?
ORDER BY id ASC;

-- name: GetBatchJobItemByID :one
SELECT * FROM batch_job_items
WHERE id = ?
LIMIT 1;

-- name: UpdateBatchJobItemStatus :exec
UPDATE batch_job_items
SET status = ?
WHERE id = ?;

-- name: UpdateBatchJobItemProcessing :exec
UPDATE batch_job_items
SET status = 'Processing'
WHERE id = ?;

-- name: UpdateBatchJobItemCompleted :exec
UPDATE batch_job_items
SET status = 'Completed',
    target_path = ?,
    actual_path = ?,
    processing_time_ms = ?,
    processed_at = ?
WHERE id = ?;

-- name: UpdateBatchJobItemFailed :exec
UPDATE batch_job_items
SET status = 'Failed',
    error_message = ?,
    processing_time_ms = ?,
    processed_at = ?
WHERE id = ?;

-- name: CountBatchJobItemsByStatus :one
SELECT
    COUNT(*) as total,
    SUM(CASE WHEN status = 'Pending' THEN 1 ELSE 0 END) as pending,
    SUM(CASE WHEN status = 'Processing' THEN 1 ELSE 0 END) as processing,
    SUM(CASE WHEN status = 'Completed' THEN 1 ELSE 0 END) as completed,
    SUM(CASE WHEN status = 'Failed' THEN 1 ELSE 0 END) as failed
FROM batch_job_items
WHERE batch_job_id = ?;

-- name: GetBatchJobItemsWithFileDetails :many
SELECT
    bji.*,
    tf.file_name,
    tf.original_path,
    tf.file_size
FROM batch_job_items bji
INNER JOIN tracked_files tf ON bji.file_hash = tf.hash
WHERE bji.batch_job_id = ?
  AND tf.is_active = 1
ORDER BY bji.id ASC;
