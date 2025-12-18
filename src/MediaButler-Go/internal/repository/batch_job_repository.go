package repository

import (
	"context"
	"database/sql"
	"fmt"
	"time"

	"github.com/lucapaganotti/mediabutler-go/internal/domain"
)

// BatchJobRepository handles database operations for batch jobs
type BatchJobRepository interface {
	// Job CRUD
	Create(ctx context.Context, job *domain.BatchJob) error
	GetByID(ctx context.Context, jobID string) (*domain.BatchJob, error)
	UpdateStatus(ctx context.Context, jobID string, status domain.JobStatus) error
	UpdateProgress(ctx context.Context, jobID string, processed, successful, failed int) error
	MarkStarted(ctx context.Context, jobID string) error
	MarkCompleted(ctx context.Context, jobID string, status domain.JobStatus, processed, successful, failed int) error
	MarkFailed(ctx context.Context, jobID string, errorMsg string, retry bool) error
	MarkCancelled(ctx context.Context, jobID string) error

	// Job queries
	List(ctx context.Context, status *domain.JobStatus, limit, offset int) ([]*domain.BatchJob, error)
	GetQueued(ctx context.Context, limit int) ([]*domain.BatchJob, error)
	GetStatistics(ctx context.Context) (map[domain.JobStatus]int, error)
	DeleteOldJobs(ctx context.Context, olderThan time.Time) (int64, error)

	// Job items
	CreateItems(ctx context.Context, items []*domain.BatchJobItem) error
	GetItems(ctx context.Context, jobID string) ([]*domain.BatchJobItem, error)
	GetItemsWithFileDetails(ctx context.Context, jobID string) ([]*domain.BatchJobItem, error)
	UpdateItemStatus(ctx context.Context, itemID int64, status domain.ItemStatus) error
	MarkItemProcessing(ctx context.Context, itemID int64) error
	MarkItemCompleted(ctx context.Context, itemID int64, targetPath, actualPath string, processingTimeMs int64) error
	MarkItemFailed(ctx context.Context, itemID int64, errorMsg string, processingTimeMs int64) error
}

// batchJobRepository implements BatchJobRepository
type batchJobRepository struct {
	db *sql.DB
}

// NewBatchJobRepository creates a new batch job repository
func NewBatchJobRepository(db *sql.DB) BatchJobRepository {
	return &batchJobRepository{db: db}
}

// Create creates a new batch job
func (r *batchJobRepository) Create(ctx context.Context, job *domain.BatchJob) error {
	// Marshal metadata to JSON
	if err := job.MarshalMetadata(); err != nil {
		return fmt.Errorf("marshal metadata: %w", err)
	}

	query := `
		INSERT INTO batch_jobs (
			id, batch_name, status, queued_at, total_files, processed_files,
			successful_files, failed_files, continue_on_error, dry_run,
			max_concurrency, retry_count, max_retries, metadata,
			created_date, last_update_date
		) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
	`

	_, err := r.db.ExecContext(ctx, query,
		job.ID,
		job.BatchName,
		job.Status,
		job.QueuedAt,
		job.TotalFiles,
		job.ProcessedFiles,
		job.SuccessfulFiles,
		job.FailedFiles,
		job.ContinueOnError,
		job.DryRun,
		job.MaxConcurrency,
		job.RetryCount,
		job.MaxRetries,
		job.MetadataJSON,
		job.CreatedDate,
		job.LastUpdateDate,
	)

	if err != nil {
		return fmt.Errorf("create batch job: %w", err)
	}

	return nil
}

// GetByID retrieves a batch job by ID
func (r *batchJobRepository) GetByID(ctx context.Context, jobID string) (*domain.BatchJob, error) {
	query := `SELECT * FROM batch_jobs WHERE id = ? LIMIT 1`

	job := &domain.BatchJob{}
	err := r.db.QueryRowContext(ctx, query, jobID).Scan(
		&job.ID,
		&job.BatchName,
		&job.Status,
		&job.QueuedAt,
		&job.StartedAt,
		&job.CompletedAt,
		&job.TotalFiles,
		&job.ProcessedFiles,
		&job.SuccessfulFiles,
		&job.FailedFiles,
		&job.ContinueOnError,
		&job.DryRun,
		&job.MaxConcurrency,
		&job.RetryCount,
		&job.MaxRetries,
		&job.NextRetryAt,
		&job.MetadataJSON,
		&job.ErrorMessage,
		&job.CreatedDate,
		&job.LastUpdateDate,
	)

	if err == sql.ErrNoRows {
		return nil, fmt.Errorf("batch job not found: %s", jobID)
	}
	if err != nil {
		return nil, fmt.Errorf("get batch job: %w", err)
	}

	// Unmarshal metadata
	if err := job.UnmarshalMetadata(); err != nil {
		return nil, fmt.Errorf("unmarshal metadata: %w", err)
	}

	return job, nil
}

// UpdateStatus updates the job status
func (r *batchJobRepository) UpdateStatus(ctx context.Context, jobID string, status domain.JobStatus) error {
	query := `UPDATE batch_jobs SET status = ?, last_update_date = ? WHERE id = ?`

	_, err := r.db.ExecContext(ctx, query, status, time.Now().UTC(), jobID)
	if err != nil {
		return fmt.Errorf("update batch job status: %w", err)
	}

	return nil
}

// UpdateProgress updates processed file counts
func (r *batchJobRepository) UpdateProgress(ctx context.Context, jobID string, processed, successful, failed int) error {
	query := `
		UPDATE batch_jobs
		SET processed_files = ?, successful_files = ?, failed_files = ?, last_update_date = ?
		WHERE id = ?
	`

	_, err := r.db.ExecContext(ctx, query, processed, successful, failed, time.Now().UTC(), jobID)
	if err != nil {
		return fmt.Errorf("update batch job progress: %w", err)
	}

	return nil
}

// MarkStarted marks job as started
func (r *batchJobRepository) MarkStarted(ctx context.Context, jobID string) error {
	now := time.Now().UTC()
	query := `UPDATE batch_jobs SET status = 'Processing', started_at = ?, last_update_date = ? WHERE id = ?`

	_, err := r.db.ExecContext(ctx, query, now, now, jobID)
	if err != nil {
		return fmt.Errorf("mark batch job started: %w", err)
	}

	return nil
}

// MarkCompleted marks job as completed
func (r *batchJobRepository) MarkCompleted(ctx context.Context, jobID string, status domain.JobStatus, processed, successful, failed int) error {
	now := time.Now().UTC()
	query := `
		UPDATE batch_jobs
		SET status = ?, completed_at = ?, processed_files = ?, successful_files = ?, failed_files = ?, last_update_date = ?
		WHERE id = ?
	`

	_, err := r.db.ExecContext(ctx, query, status, now, processed, successful, failed, now, jobID)
	if err != nil {
		return fmt.Errorf("mark batch job completed: %w", err)
	}

	return nil
}

// MarkFailed marks job as failed with optional retry
func (r *batchJobRepository) MarkFailed(ctx context.Context, jobID string, errorMsg string, retry bool) error {
	now := time.Now().UTC()

	var query string
	var args []interface{}

	if retry {
		// Calculate next retry time (will be set by caller)
		query = `
			UPDATE batch_jobs
			SET status = 'Queued', error_message = ?, retry_count = retry_count + 1,
			    next_retry_at = ?, last_update_date = ?
			WHERE id = ?
		`
		// Next retry will be calculated based on retry_count
		retryDelays := []int{30, 60, 120} // seconds
		var retryCount int
		r.db.QueryRowContext(ctx, "SELECT retry_count FROM batch_jobs WHERE id = ?", jobID).Scan(&retryCount)

		var nextRetry *time.Time
		if retryCount < len(retryDelays) {
			t := now.Add(time.Duration(retryDelays[retryCount]) * time.Second)
			nextRetry = &t
		}

		args = []interface{}{errorMsg, nextRetry, now, jobID}
	} else {
		query = `
			UPDATE batch_jobs
			SET status = 'Failed', completed_at = ?, error_message = ?, last_update_date = ?
			WHERE id = ?
		`
		args = []interface{}{now, errorMsg, now, jobID}
	}

	_, err := r.db.ExecContext(ctx, query, args...)
	if err != nil {
		return fmt.Errorf("mark batch job failed: %w", err)
	}

	return nil
}

// MarkCancelled marks job as cancelled
func (r *batchJobRepository) MarkCancelled(ctx context.Context, jobID string) error {
	now := time.Now().UTC()
	query := `UPDATE batch_jobs SET status = 'Cancelled', completed_at = ?, last_update_date = ? WHERE id = ?`

	_, err := r.db.ExecContext(ctx, query, now, now, jobID)
	if err != nil {
		return fmt.Errorf("mark batch job cancelled: %w", err)
	}

	return nil
}

// List retrieves batch jobs with optional status filter
func (r *batchJobRepository) List(ctx context.Context, status *domain.JobStatus, limit, offset int) ([]*domain.BatchJob, error) {
	var query string
	var args []interface{}

	if status != nil {
		query = `SELECT * FROM batch_jobs WHERE status = ? ORDER BY queued_at DESC LIMIT ? OFFSET ?`
		args = []interface{}{*status, limit, offset}
	} else {
		query = `SELECT * FROM batch_jobs ORDER BY queued_at DESC LIMIT ? OFFSET ?`
		args = []interface{}{limit, offset}
	}

	rows, err := r.db.QueryContext(ctx, query, args...)
	if err != nil {
		return nil, fmt.Errorf("list batch jobs: %w", err)
	}
	defer rows.Close()

	jobs := make([]*domain.BatchJob, 0)
	for rows.Next() {
		job := &domain.BatchJob{}
		err := rows.Scan(
			&job.ID,
			&job.BatchName,
			&job.Status,
			&job.QueuedAt,
			&job.StartedAt,
			&job.CompletedAt,
			&job.TotalFiles,
			&job.ProcessedFiles,
			&job.SuccessfulFiles,
			&job.FailedFiles,
			&job.ContinueOnError,
			&job.DryRun,
			&job.MaxConcurrency,
			&job.RetryCount,
			&job.MaxRetries,
			&job.NextRetryAt,
			&job.MetadataJSON,
			&job.ErrorMessage,
			&job.CreatedDate,
			&job.LastUpdateDate,
		)
		if err != nil {
			return nil, fmt.Errorf("scan batch job: %w", err)
		}

		// Unmarshal metadata
		if err := job.UnmarshalMetadata(); err != nil {
			return nil, fmt.Errorf("unmarshal metadata: %w", err)
		}

		jobs = append(jobs, job)
	}

	return jobs, nil
}

// GetQueued retrieves queued jobs ready for processing
func (r *batchJobRepository) GetQueued(ctx context.Context, limit int) ([]*domain.BatchJob, error) {
	query := `
		SELECT * FROM batch_jobs
		WHERE status = 'Queued'
		  AND (next_retry_at IS NULL OR next_retry_at <= datetime('now'))
		ORDER BY queued_at ASC
		LIMIT ?
	`

	rows, err := r.db.QueryContext(ctx, query, limit)
	if err != nil {
		return nil, fmt.Errorf("get queued batch jobs: %w", err)
	}
	defer rows.Close()

	jobs := make([]*domain.BatchJob, 0)
	for rows.Next() {
		job := &domain.BatchJob{}
		err := rows.Scan(
			&job.ID,
			&job.BatchName,
			&job.Status,
			&job.QueuedAt,
			&job.StartedAt,
			&job.CompletedAt,
			&job.TotalFiles,
			&job.ProcessedFiles,
			&job.SuccessfulFiles,
			&job.FailedFiles,
			&job.ContinueOnError,
			&job.DryRun,
			&job.MaxConcurrency,
			&job.RetryCount,
			&job.MaxRetries,
			&job.NextRetryAt,
			&job.MetadataJSON,
			&job.ErrorMessage,
			&job.CreatedDate,
			&job.LastUpdateDate,
		)
		if err != nil {
			return nil, fmt.Errorf("scan batch job: %w", err)
		}

		if err := job.UnmarshalMetadata(); err != nil {
			return nil, fmt.Errorf("unmarshal metadata: %w", err)
		}

		jobs = append(jobs, job)
	}

	return jobs, nil
}

// GetStatistics returns job counts by status
func (r *batchJobRepository) GetStatistics(ctx context.Context) (map[domain.JobStatus]int, error) {
	query := `
		SELECT
			SUM(CASE WHEN status = 'Queued' THEN 1 ELSE 0 END) as queued_jobs,
			SUM(CASE WHEN status = 'Processing' THEN 1 ELSE 0 END) as processing_jobs,
			SUM(CASE WHEN status = 'Completed' THEN 1 ELSE 0 END) as completed_jobs,
			SUM(CASE WHEN status = 'Failed' THEN 1 ELSE 0 END) as failed_jobs,
			SUM(CASE WHEN status = 'Cancelled' THEN 1 ELSE 0 END) as cancelled_jobs
		FROM batch_jobs
	`

	var queued, processing, completed, failed, cancelled int
	err := r.db.QueryRowContext(ctx, query).Scan(&queued, &processing, &completed, &failed, &cancelled)
	if err != nil {
		return nil, fmt.Errorf("get batch job statistics: %w", err)
	}

	stats := map[domain.JobStatus]int{
		domain.JobStatusQueued:     queued,
		domain.JobStatusProcessing: processing,
		domain.JobStatusCompleted:  completed,
		domain.JobStatusFailed:     failed,
		domain.JobStatusCancelled:  cancelled,
	}

	return stats, nil
}

// DeleteOldJobs deletes completed/failed/cancelled jobs older than specified time
func (r *batchJobRepository) DeleteOldJobs(ctx context.Context, olderThan time.Time) (int64, error) {
	query := `
		DELETE FROM batch_jobs
		WHERE status IN ('Completed', 'Failed', 'Cancelled')
		  AND completed_at < ?
	`

	result, err := r.db.ExecContext(ctx, query, olderThan)
	if err != nil {
		return 0, fmt.Errorf("delete old batch jobs: %w", err)
	}

	count, _ := result.RowsAffected()
	return count, nil
}

// CreateItems creates batch job items
func (r *batchJobRepository) CreateItems(ctx context.Context, items []*domain.BatchJobItem) error {
	tx, err := r.db.BeginTx(ctx, nil)
	if err != nil {
		return fmt.Errorf("begin transaction: %w", err)
	}
	defer tx.Rollback()

	query := `
		INSERT INTO batch_job_items (
			batch_job_id, file_hash, confirmed_category, custom_target_path,
			status, metadata, created_date
		) VALUES (?, ?, ?, ?, ?, ?, ?)
	`

	stmt, err := tx.PrepareContext(ctx, query)
	if err != nil {
		return fmt.Errorf("prepare statement: %w", err)
	}
	defer stmt.Close()

	for _, item := range items {
		// Marshal metadata
		if err := item.MarshalMetadata(); err != nil {
			return fmt.Errorf("marshal metadata: %w", err)
		}

		_, err := stmt.ExecContext(ctx,
			item.BatchJobID,
			item.FileHash,
			item.ConfirmedCategory,
			item.CustomTargetPath,
			item.Status,
			item.MetadataJSON,
			item.CreatedDate,
		)
		if err != nil {
			return fmt.Errorf("create batch job item: %w", err)
		}
	}

	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit transaction: %w", err)
	}

	return nil
}

// GetItems retrieves all items for a batch job
func (r *batchJobRepository) GetItems(ctx context.Context, jobID string) ([]*domain.BatchJobItem, error) {
	query := `SELECT * FROM batch_job_items WHERE batch_job_id = ? ORDER BY id ASC`

	rows, err := r.db.QueryContext(ctx, query, jobID)
	if err != nil {
		return nil, fmt.Errorf("get batch job items: %w", err)
	}
	defer rows.Close()

	return r.scanJobItems(rows)
}

// GetItemsWithFileDetails retrieves items with joined file details
func (r *batchJobRepository) GetItemsWithFileDetails(ctx context.Context, jobID string) ([]*domain.BatchJobItem, error) {
	query := `
		SELECT
			bji.*,
			tf.file_name,
			tf.original_path,
			tf.file_size
		FROM batch_job_items bji
		INNER JOIN tracked_files tf ON bji.file_hash = tf.hash
		WHERE bji.batch_job_id = ?
		  AND tf.is_active = 1
		ORDER BY bji.id ASC
	`

	rows, err := r.db.QueryContext(ctx, query, jobID)
	if err != nil {
		return nil, fmt.Errorf("get batch job items with file details: %w", err)
	}
	defer rows.Close()

	return r.scanJobItemsWithFileDetails(rows)
}

// scanJobItems scans rows into BatchJobItem slice
func (r *batchJobRepository) scanJobItems(rows *sql.Rows) ([]*domain.BatchJobItem, error) {
	items := make([]*domain.BatchJobItem, 0)

	for rows.Next() {
		item := &domain.BatchJobItem{}
		err := rows.Scan(
			&item.ID,
			&item.BatchJobID,
			&item.FileHash,
			&item.ConfirmedCategory,
			&item.CustomTargetPath,
			&item.Status,
			&item.TargetPath,
			&item.ActualPath,
			&item.ErrorMessage,
			&item.ProcessingTimeMs,
			&item.ProcessedAt,
			&item.MetadataJSON,
			&item.CreatedDate,
		)
		if err != nil {
			return nil, fmt.Errorf("scan batch job item: %w", err)
		}

		if err := item.UnmarshalMetadata(); err != nil {
			return nil, fmt.Errorf("unmarshal metadata: %w", err)
		}

		items = append(items, item)
	}

	return items, nil
}

// scanJobItemsWithFileDetails scans rows with joined file details
func (r *batchJobRepository) scanJobItemsWithFileDetails(rows *sql.Rows) ([]*domain.BatchJobItem, error) {
	items := make([]*domain.BatchJobItem, 0)

	for rows.Next() {
		item := &domain.BatchJobItem{}
		err := rows.Scan(
			&item.ID,
			&item.BatchJobID,
			&item.FileHash,
			&item.ConfirmedCategory,
			&item.CustomTargetPath,
			&item.Status,
			&item.TargetPath,
			&item.ActualPath,
			&item.ErrorMessage,
			&item.ProcessingTimeMs,
			&item.ProcessedAt,
			&item.MetadataJSON,
			&item.CreatedDate,
			&item.FileName,
			&item.OriginalPath,
			&item.FileSize,
		)
		if err != nil {
			return nil, fmt.Errorf("scan batch job item with file details: %w", err)
		}

		if err := item.UnmarshalMetadata(); err != nil {
			return nil, fmt.Errorf("unmarshal metadata: %w", err)
		}

		items = append(items, item)
	}

	return items, nil
}

// UpdateItemStatus updates item status
func (r *batchJobRepository) UpdateItemStatus(ctx context.Context, itemID int64, status domain.ItemStatus) error {
	query := `UPDATE batch_job_items SET status = ? WHERE id = ?`

	_, err := r.db.ExecContext(ctx, query, status, itemID)
	if err != nil {
		return fmt.Errorf("update batch job item status: %w", err)
	}

	return nil
}

// MarkItemProcessing marks item as processing
func (r *batchJobRepository) MarkItemProcessing(ctx context.Context, itemID int64) error {
	query := `UPDATE batch_job_items SET status = 'Processing' WHERE id = ?`

	_, err := r.db.ExecContext(ctx, query, itemID)
	if err != nil {
		return fmt.Errorf("mark batch job item processing: %w", err)
	}

	return nil
}

// MarkItemCompleted marks item as completed
func (r *batchJobRepository) MarkItemCompleted(ctx context.Context, itemID int64, targetPath, actualPath string, processingTimeMs int64) error {
	now := time.Now().UTC()
	query := `
		UPDATE batch_job_items
		SET status = 'Completed', target_path = ?, actual_path = ?, processing_time_ms = ?, processed_at = ?
		WHERE id = ?
	`

	_, err := r.db.ExecContext(ctx, query, targetPath, actualPath, processingTimeMs, now, itemID)
	if err != nil {
		return fmt.Errorf("mark batch job item completed: %w", err)
	}

	return nil
}

// MarkItemFailed marks item as failed
func (r *batchJobRepository) MarkItemFailed(ctx context.Context, itemID int64, errorMsg string, processingTimeMs int64) error {
	now := time.Now().UTC()
	query := `
		UPDATE batch_job_items
		SET status = 'Failed', error_message = ?, processing_time_ms = ?, processed_at = ?
		WHERE id = ?
	`

	_, err := r.db.ExecContext(ctx, query, errorMsg, processingTimeMs, now, itemID)
	if err != nil {
		return fmt.Errorf("mark batch job item failed: %w", err)
	}

	return nil
}
