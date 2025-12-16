-- TrackedFile CRUD Operations

-- name: GetFileByHash :one
SELECT * FROM TrackedFiles
WHERE Hash = ? AND IsActive = 1
LIMIT 1;

-- name: GetFileByHashIncludeDeleted :one
SELECT * FROM TrackedFiles
WHERE Hash = ?
LIMIT 1;

-- name: CreateFile :exec
INSERT INTO TrackedFiles (
    Hash, FileName, OriginalPath, FileSize, Status,
    CreatedDate, LastUpdateDate, IsActive
) VALUES (?, ?, ?, ?, ?, ?, ?, ?);

-- name: UpdateFile :exec
UPDATE TrackedFiles
SET FileName = ?,
    OriginalPath = ?,
    FileSize = ?,
    Status = ?,
    SuggestedCategory = ?,
    Confidence = ?,
    Category = ?,
    TargetPath = ?,
    ClassifiedAt = ?,
    MovedAt = ?,
    LastError = ?,
    LastErrorAt = ?,
    RetryCount = ?,
    LastUpdateDate = ?,
    Note = ?
WHERE Hash = ? AND IsActive = 1;

-- name: UpdateClassification :exec
UPDATE TrackedFiles
SET SuggestedCategory = ?,
    Confidence = ?,
    ClassifiedAt = ?,
    Status = ?,
    LastUpdateDate = ?
WHERE Hash = ? AND IsActive = 1;

-- name: UpdateCategoryConfirmation :exec
UPDATE TrackedFiles
SET Category = ?,
    TargetPath = ?,
    Status = ?,
    LastUpdateDate = ?
WHERE Hash = ? AND IsActive = 1;

-- name: UpdateMoveCompletion :exec
UPDATE TrackedFiles
SET MovedToPath = ?,
    MovedAt = ?,
    Status = ?,
    LastUpdateDate = ?
WHERE Hash = ? AND IsActive = 1;

-- name: SoftDeleteFile :exec
UPDATE TrackedFiles
SET IsActive = 0,
    LastUpdateDate = ?,
    Note = ?
WHERE Hash = ?;

-- name: RestoreFile :exec
UPDATE TrackedFiles
SET IsActive = 1,
    LastUpdateDate = ?,
    Note = ?
WHERE Hash = ?;

-- Workflow Queries

-- name: GetFilesByStatus :many
SELECT * FROM TrackedFiles
WHERE Status = ? AND IsActive = 1
ORDER BY CreatedDate DESC
LIMIT ? OFFSET ?;

-- name: GetFilesByStatuses :many
SELECT * FROM TrackedFiles
WHERE Status IN (sqlc.slice('statuses'))
  AND IsActive = 1
  AND (sqlc.narg('category') IS NULL OR Category = sqlc.narg('category'))
  AND (sqlc.narg('search_term') IS NULL
       OR FileName LIKE '%' || sqlc.narg('search_term') || '%'
       OR Category LIKE '%' || sqlc.narg('search_term') || '%')
ORDER BY
  CASE WHEN sqlc.narg('order_by') = 'CreatedDate' THEN CreatedDate END ASC,
  CASE WHEN sqlc.narg('order_by') = 'LastUpdateDate' THEN LastUpdateDate END DESC,
  CASE WHEN sqlc.narg('order_by') = 'FileName' THEN FileName END ASC,
  LastUpdateDate DESC
LIMIT ? OFFSET ?;

-- name: CountFilesByStatuses :one
SELECT COUNT(*) FROM TrackedFiles
WHERE Status IN (sqlc.slice('statuses'))
  AND IsActive = 1
  AND (sqlc.narg('category') IS NULL OR Category = sqlc.narg('category'))
  AND (sqlc.narg('search_term') IS NULL
       OR FileName LIKE '%' || sqlc.narg('search_term') || '%'
       OR Category LIKE '%' || sqlc.narg('search_term') || '%');

-- name: GetFilesReadyForClassification :many
SELECT * FROM TrackedFiles
WHERE Status = 0  -- FileStatus.New
  AND IsActive = 1
ORDER BY CreatedDate ASC
LIMIT ?;

-- name: GetFilesAwaitingConfirmation :many
SELECT * FROM TrackedFiles
WHERE Status = 2  -- FileStatus.Classified
  AND IsActive = 1
ORDER BY ClassifiedAt DESC;

-- name: GetFilesReadyForMoving :many
SELECT * FROM TrackedFiles
WHERE Status = 3  -- FileStatus.ReadyToMove
  AND IsActive = 1
ORDER BY LastUpdateDate ASC
LIMIT ?;

-- name: GetFilesWithErrors :many
SELECT * FROM TrackedFiles
WHERE Status = 6  -- FileStatus.Error
  AND IsActive = 1
ORDER BY LastErrorAt DESC;

-- name: GetFilesReadyForRetry :many
SELECT * FROM TrackedFiles
WHERE Status = 7  -- FileStatus.Retry
  AND IsActive = 1
ORDER BY LastErrorAt ASC
LIMIT ?;

-- name: GetFilesExceedingRetryLimit :many
SELECT * FROM TrackedFiles
WHERE Status = 6  -- FileStatus.Error
  AND RetryCount >= ?
  AND IsActive = 1
ORDER BY RetryCount DESC, LastErrorAt DESC;

-- Analytics Queries

-- name: GetProcessingStats :many
SELECT Status, COUNT(*) as Count
FROM TrackedFiles
WHERE IsActive = 1
GROUP BY Status;

-- name: GetFilesByCategory :many
SELECT * FROM TrackedFiles
WHERE Category = ?
  AND IsActive = 1
ORDER BY MovedAt DESC;

-- name: GetLowConfidenceFiles :many
SELECT * FROM TrackedFiles
WHERE Confidence < ?
  AND Confidence > 0
  AND IsActive = 1
ORDER BY Confidence ASC;

-- name: GetHighConfidenceFiles :many
SELECT * FROM TrackedFiles
WHERE Confidence >= ?
  AND IsActive = 1
ORDER BY Confidence DESC;

-- name: GetDistinctCategories :many
SELECT DISTINCT Category
FROM TrackedFiles
WHERE Category IS NOT NULL
  AND IsActive = 1
ORDER BY Category ASC;

-- Temporal Queries

-- name: GetFilesProcessedInRange :many
SELECT * FROM TrackedFiles
WHERE CreatedDate BETWEEN ? AND ?
  AND IsActive = 1
ORDER BY CreatedDate DESC;

-- name: GetRecentlyMovedFiles :many
SELECT * FROM TrackedFiles
WHERE Status = 5  -- FileStatus.Moved
  AND MovedAt >= datetime('now', ?)  -- e.g., '-24 hours'
  AND IsActive = 1
ORDER BY MovedAt DESC;

-- Lookup Queries

-- name: SearchByFilename :many
SELECT * FROM TrackedFiles
WHERE FileName LIKE '%' || ? || '%'
  AND IsActive = 1
ORDER BY FileName ASC
LIMIT ? OFFSET ?;

-- name: ExistsByOriginalPath :one
SELECT COUNT(*) > 0
FROM TrackedFiles
WHERE OriginalPath = ?
  AND IsActive = 1;

-- name: ExistsByHash :one
SELECT COUNT(*) > 0
FROM TrackedFiles
WHERE Hash = ?
  AND IsActive = 1;

-- Batch Operations

-- name: GetAllFiles :many
SELECT * FROM TrackedFiles
WHERE IsActive = 1
ORDER BY CreatedDate DESC
LIMIT ? OFFSET ?;

-- name: CountAllFiles :one
SELECT COUNT(*) FROM TrackedFiles
WHERE IsActive = 1;

-- name: CountFilesByStatus :one
SELECT COUNT(*) FROM TrackedFiles
WHERE Status = ? AND IsActive = 1;
