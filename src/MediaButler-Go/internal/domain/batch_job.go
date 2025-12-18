package domain

import (
	"encoding/json"
	"time"
)

// JobStatus represents the current state of a batch job
type JobStatus string

const (
	JobStatusQueued     JobStatus = "Queued"
	JobStatusProcessing JobStatus = "Processing"
	JobStatusCompleted  JobStatus = "Completed"
	JobStatusFailed     JobStatus = "Failed"
	JobStatusCancelled  JobStatus = "Cancelled"
)

// ItemStatus represents the current state of a batch job item
type ItemStatus string

const (
	ItemStatusPending    ItemStatus = "Pending"
	ItemStatusProcessing ItemStatus = "Processing"
	ItemStatusCompleted  ItemStatus = "Completed"
	ItemStatusFailed     ItemStatus = "Failed"
)

// BatchJob represents a batch file organization operation
type BatchJob struct {
	ID             string    `json:"id" db:"id"`
	BatchName      string    `json:"batchName" db:"batch_name"`
	Status         JobStatus `json:"status" db:"status"`
	QueuedAt       time.Time `json:"queuedAt" db:"queued_at"`
	StartedAt      *time.Time `json:"startedAt,omitempty" db:"started_at"`
	CompletedAt    *time.Time `json:"completedAt,omitempty" db:"completed_at"`

	// Progress tracking
	TotalFiles      int `json:"totalFiles" db:"total_files"`
	ProcessedFiles  int `json:"processedFiles" db:"processed_files"`
	SuccessfulFiles int `json:"successfulFiles" db:"successful_files"`
	FailedFiles     int `json:"failedFiles" db:"failed_files"`

	// Configuration
	ContinueOnError bool `json:"continueOnError" db:"continue_on_error"`
	DryRun          bool `json:"dryRun" db:"dry_run"`
	MaxConcurrency  int  `json:"maxConcurrency" db:"max_concurrency"`

	// Retry logic
	RetryCount    int        `json:"retryCount" db:"retry_count"`
	MaxRetries    int        `json:"maxRetries" db:"max_retries"`
	NextRetryAt   *time.Time `json:"nextRetryAt,omitempty" db:"next_retry_at"`

	// Metadata and errors
	Metadata     map[string]interface{} `json:"metadata,omitempty"`
	MetadataJSON *string                `db:"metadata"` // JSON storage in DB
	ErrorMessage *string                `json:"errorMessage,omitempty" db:"error_message"`

	// Audit fields
	CreatedDate    time.Time `json:"createdDate" db:"created_date"`
	LastUpdateDate time.Time `json:"lastUpdateDate" db:"last_update_date"`
}

// BatchJobItem represents a single file operation within a batch job
type BatchJobItem struct {
	ID               int64      `json:"id" db:"id"`
	BatchJobID       string     `json:"batchJobId" db:"batch_job_id"`
	FileHash         string     `json:"fileHash" db:"file_hash"`
	ConfirmedCategory string    `json:"confirmedCategory" db:"confirmed_category"`
	CustomTargetPath *string    `json:"customTargetPath,omitempty" db:"custom_target_path"`
	Status           ItemStatus `json:"status" db:"status"`

	// Results
	TargetPath       *string    `json:"targetPath,omitempty" db:"target_path"`
	ActualPath       *string    `json:"actualPath,omitempty" db:"actual_path"`
	ErrorMessage     *string    `json:"errorMessage,omitempty" db:"error_message"`
	ProcessingTimeMs *int64     `json:"processingTimeMs,omitempty" db:"processing_time_ms"`
	ProcessedAt      *time.Time `json:"processedAt,omitempty" db:"processed_at"`

	// Metadata
	Metadata     map[string]interface{} `json:"metadata,omitempty"`
	MetadataJSON *string                `db:"metadata"` // JSON storage in DB

	// Audit
	CreatedDate time.Time `json:"createdDate" db:"created_date"`

	// Optional joined fields (from tracked_files)
	FileName     *string `json:"fileName,omitempty" db:"file_name"`
	OriginalPath *string `json:"originalPath,omitempty" db:"original_path"`
	FileSize     *int64  `json:"fileSize,omitempty" db:"file_size"`
}

// NewBatchJob creates a new batch job with default values
func NewBatchJob(id, batchName string, totalFiles int, continueOnError, dryRun bool, maxConcurrency int) *BatchJob {
	now := time.Now().UTC()
	return &BatchJob{
		ID:              id,
		BatchName:       batchName,
		Status:          JobStatusQueued,
		QueuedAt:        now,
		TotalFiles:      totalFiles,
		ProcessedFiles:  0,
		SuccessfulFiles: 0,
		FailedFiles:     0,
		ContinueOnError: continueOnError,
		DryRun:          dryRun,
		MaxConcurrency:  maxConcurrency,
		RetryCount:      0,
		MaxRetries:      3,
		Metadata:        make(map[string]interface{}),
		CreatedDate:     now,
		LastUpdateDate:  now,
	}
}

// NewBatchJobItem creates a new batch job item
func NewBatchJobItem(batchJobID, fileHash, confirmedCategory string, customTargetPath *string) *BatchJobItem {
	now := time.Now().UTC()
	return &BatchJobItem{
		BatchJobID:        batchJobID,
		FileHash:          fileHash,
		ConfirmedCategory: confirmedCategory,
		CustomTargetPath:  customTargetPath,
		Status:            ItemStatusPending,
		Metadata:          make(map[string]interface{}),
		CreatedDate:       now,
	}
}

// ProgressPercentage calculates the job completion percentage
func (j *BatchJob) ProgressPercentage() int {
	if j.TotalFiles == 0 {
		return 0
	}
	return (j.ProcessedFiles * 100) / j.TotalFiles
}

// MarkAsStarted updates the job status to Processing
func (j *BatchJob) MarkAsStarted() {
	now := time.Now().UTC()
	j.Status = JobStatusProcessing
	j.StartedAt = &now
	j.LastUpdateDate = now
}

// MarkAsCompleted updates the job status to Completed
func (j *BatchJob) MarkAsCompleted() {
	now := time.Now().UTC()
	j.Status = JobStatusCompleted
	j.CompletedAt = &now
	j.LastUpdateDate = now
}

// MarkAsFailed updates the job status to Failed with error message
func (j *BatchJob) MarkAsFailed(errorMsg string) {
	now := time.Now().UTC()
	j.Status = JobStatusFailed
	j.CompletedAt = &now
	j.ErrorMessage = &errorMsg
	j.LastUpdateDate = now
}

// MarkAsCancelled updates the job status to Cancelled
func (j *BatchJob) MarkAsCancelled() {
	now := time.Now().UTC()
	j.Status = JobStatusCancelled
	j.CompletedAt = &now
	j.LastUpdateDate = now
}

// IncrementRetry increments retry count and calculates next retry time
func (j *BatchJob) IncrementRetry() time.Duration {
	j.RetryCount++
	j.LastUpdateDate = time.Now().UTC()

	// Exponential backoff: 30s, 60s, 120s
	delays := []int{30, 60, 120} // seconds
	if j.RetryCount > len(delays) {
		return 0 // No more retries
	}

	delay := time.Duration(delays[j.RetryCount-1]) * time.Second
	nextRetry := time.Now().UTC().Add(delay)
	j.NextRetryAt = &nextRetry

	// Reset to Queued for retry
	j.Status = JobStatusQueued

	return delay
}

// CanRetry checks if the job can be retried
func (j *BatchJob) CanRetry() bool {
	return j.RetryCount < j.MaxRetries
}

// UpdateProgress updates processed file counts
func (j *BatchJob) UpdateProgress(processed, successful, failed int) {
	j.ProcessedFiles = processed
	j.SuccessfulFiles = successful
	j.FailedFiles = failed
	j.LastUpdateDate = time.Now().UTC()
}

// MarshalMetadata converts metadata map to JSON string for DB storage
func (j *BatchJob) MarshalMetadata() error {
	if j.Metadata == nil || len(j.Metadata) == 0 {
		return nil
	}

	jsonBytes, err := json.Marshal(j.Metadata)
	if err != nil {
		return err
	}

	jsonStr := string(jsonBytes)
	j.MetadataJSON = &jsonStr
	return nil
}

// UnmarshalMetadata converts JSON string from DB to metadata map
func (j *BatchJob) UnmarshalMetadata() error {
	if j.MetadataJSON == nil || *j.MetadataJSON == "" {
		j.Metadata = make(map[string]interface{})
		return nil
	}

	return json.Unmarshal([]byte(*j.MetadataJSON), &j.Metadata)
}

// MarshalMetadata converts metadata map to JSON string for DB storage
func (i *BatchJobItem) MarshalMetadata() error {
	if i.Metadata == nil || len(i.Metadata) == 0 {
		return nil
	}

	jsonBytes, err := json.Marshal(i.Metadata)
	if err != nil {
		return err
	}

	jsonStr := string(jsonBytes)
	i.MetadataJSON = &jsonStr
	return nil
}

// UnmarshalMetadata converts JSON string from DB to metadata map
func (i *BatchJobItem) UnmarshalMetadata() error {
	if i.MetadataJSON == nil || *i.MetadataJSON == "" {
		i.Metadata = make(map[string]interface{})
		return nil
	}

	return json.Unmarshal([]byte(*i.MetadataJSON), &i.Metadata)
}

// MarkAsProcessing updates item status to Processing
func (i *BatchJobItem) MarkAsProcessing() {
	i.Status = ItemStatusProcessing
}

// MarkAsCompleted updates item status to Completed with results
func (i *BatchJobItem) MarkAsCompleted(targetPath, actualPath string, processingTimeMs int64) {
	now := time.Now().UTC()
	i.Status = ItemStatusCompleted
	i.TargetPath = &targetPath
	i.ActualPath = &actualPath
	i.ProcessingTimeMs = &processingTimeMs
	i.ProcessedAt = &now
}

// MarkAsFailed updates item status to Failed with error
func (i *BatchJobItem) MarkAsFailed(errorMsg string, processingTimeMs int64) {
	now := time.Now().UTC()
	i.Status = ItemStatusFailed
	i.ErrorMessage = &errorMsg
	i.ProcessingTimeMs = &processingTimeMs
	i.ProcessedAt = &now
}
