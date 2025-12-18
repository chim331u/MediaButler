package service

import (
	"context"
	"fmt"
	"time"

	"github.com/google/uuid"
	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/internal/jobs/batch"
	"github.com/lucapaganotti/mediabutler-go/internal/repository"
	"github.com/lucapaganotti/mediabutler-go/pkg/result"
	"github.com/rs/zerolog"
)

// BatchOrganizeRequest represents a batch organization request
type BatchOrganizeRequest struct {
	BatchName       string                            `json:"batchName"`
	Operations      []domain.FileOrganizeOperation    `json:"operations"`
	ContinueOnError bool                              `json:"continueOnError"`
	DryRun          bool                              `json:"dryRun"`
	MaxConcurrency  int                               `json:"maxConcurrency"`
	Metadata        map[string]interface{}            `json:"metadata,omitempty"`
}

// BatchValidationRequest represents a pre-flight validation request
type BatchValidationRequest struct {
	FileHashes []string `json:"fileHashes"`
}

// BatchValidationResult represents validation results
type BatchValidationResult struct {
	Valid       bool                               `json:"valid"`
	TotalFiles  int                                `json:"totalFiles"`
	ValidFiles  int                                `json:"validFiles"`
	InvalidOps  []InvalidOperationError            `json:"invalidOperations,omitempty"`
}

// InvalidOperationError represents a validation error for a specific operation
type InvalidOperationError struct {
	FileHash string `json:"fileHash"`
	Reason   string `json:"reason"`
}

// BatchJobFilter represents filtering options for listing batch jobs
type BatchJobFilter struct {
	Status    *domain.JobStatus `json:"status,omitempty"`
	FromDate  *time.Time        `json:"fromDate,omitempty"`
	ToDate    *time.Time        `json:"toDate,omitempty"`
	Limit     int               `json:"limit"`
	Offset    int               `json:"offset"`
}

// FileActionsService defines business logic for batch file operations
type FileActionsService interface {
	// OrganizeBatch creates a new batch organization job
	OrganizeBatch(ctx context.Context, request BatchOrganizeRequest) result.Result[string]

	// GetBatchStatus retrieves the status of a batch job
	GetBatchStatus(ctx context.Context, jobID string) result.Result[*domain.BatchJob]

	// CancelBatchJob cancels a running or queued batch job
	CancelBatchJob(ctx context.Context, jobID string) result.Result[bool]

	// ListBatchJobs lists batch jobs with optional filtering
	ListBatchJobs(ctx context.Context, filter BatchJobFilter) result.Result[[]*domain.BatchJob]

	// ValidateBatch performs pre-flight validation without creating a job
	ValidateBatch(ctx context.Context, request BatchValidationRequest) result.Result[BatchValidationResult]
}

// fileActionsService implements FileActionsService
type fileActionsService struct {
	batchRepo       repository.BatchJobRepository
	fileRepo        repository.FileRepository
	fileOrgService  FileOrganizationService
	scheduler       *batch.Scheduler
	uow             repository.UnitOfWork
	logger          zerolog.Logger
}

// NewFileActionsService creates a new FileActionsService instance
func NewFileActionsService(
	batchRepo repository.BatchJobRepository,
	fileRepo repository.FileRepository,
	fileOrgService FileOrganizationService,
	scheduler *batch.Scheduler,
	uow repository.UnitOfWork,
	logger zerolog.Logger,
) FileActionsService {
	return &fileActionsService{
		batchRepo:      batchRepo,
		fileRepo:       fileRepo,
		fileOrgService: fileOrgService,
		scheduler:      scheduler,
		uow:            uow,
		logger:         logger.With().Str("component", "file-actions-service").Logger(),
	}
}

// OrganizeBatch creates a new batch organization job
func (s *fileActionsService) OrganizeBatch(ctx context.Context, request BatchOrganizeRequest) result.Result[string] {
	s.logger.Info().
		Str("batch_name", request.BatchName).
		Int("operation_count", len(request.Operations)).
		Bool("dry_run", request.DryRun).
		Msg("Creating batch organization job")

	// Validate request
	if err := s.validateBatchRequest(request); err != nil {
		s.logger.Error().Err(err).Msg("Batch request validation failed")
		return result.Failure[string](err)
	}

	// Extract file hashes from operations
	fileHashes := make([]string, len(request.Operations))
	for i, op := range request.Operations {
		fileHashes[i] = op.TrackedFile.Hash
	}

	// Validate all files exist and are in valid states
	validationResult := s.ValidateBatch(ctx, BatchValidationRequest{FileHashes: fileHashes})
	if validationResult.IsFailure() {
		return result.Failure[string](validationResult.Error())
	}

	validation := validationResult.Value()
	if !validation.Valid {
		return result.Failure[string](fmt.Errorf("batch validation failed: %d invalid operations", len(validation.InvalidOps)))
	}

	// Create batch job and items in transaction
	jobID := uuid.New().String()

	err := repository.WithTransaction(ctx, s.uow, func(tx *repository.Transaction) error {
		// Create batch job
		job := &domain.BatchJob{
			ID:              jobID,
			BatchName:       request.BatchName,
			Status:          domain.JobStatusQueued,
			QueuedAt:        time.Now(),
			TotalFiles:      len(request.Operations),
			ProcessedFiles:  0,
			SuccessfulFiles: 0,
			FailedFiles:     0,
			ContinueOnError: request.ContinueOnError,
			DryRun:          request.DryRun,
			MaxConcurrency:  request.MaxConcurrency,
			RetryCount:      0,
			MaxRetries:      3,
			CreatedDate:     time.Now(),
			LastUpdateDate:  time.Now(),
		}

		// Set metadata if provided
		if request.Metadata != nil {
			job.SetMetadata(request.Metadata)
		}

		// Create job in database
		if err := tx.BatchJobs().Create(ctx, job); err != nil {
			return fmt.Errorf("create batch job: %w", err)
		}

		// Create batch job items
		items := make([]*domain.BatchJobItem, len(request.Operations))
		for i, op := range request.Operations {
			items[i] = &domain.BatchJobItem{
				BatchJobID:        jobID,
				FileHash:          op.TrackedFile.Hash,
				ConfirmedCategory: op.ConfirmedCategory,
				TargetPath:        op.TargetPath,
				Status:            domain.ItemStatusPending,
				CreatedDate:       time.Now(),
			}

			if op.CustomTargetPath != nil {
				items[i].ActualPath = op.CustomTargetPath
			}
		}

		// Batch insert items
		if err := tx.BatchJobs().CreateItems(ctx, items); err != nil {
			return fmt.Errorf("create batch job items: %w", err)
		}

		s.logger.Info().
			Str("job_id", jobID).
			Int("item_count", len(items)).
			Msg("Created batch job and items")

		return nil
	})

	if err != nil {
		s.logger.Error().Err(err).Str("job_id", jobID).Msg("Failed to create batch job")
		return result.Failure[string](err)
	}

	s.logger.Info().
		Str("job_id", jobID).
		Str("batch_name", request.BatchName).
		Msg("Batch job created successfully")

	return result.Success(jobID)
}

// GetBatchStatus retrieves the status of a batch job
func (s *fileActionsService) GetBatchStatus(ctx context.Context, jobID string) result.Result[*domain.BatchJob] {
	s.logger.Debug().Str("job_id", jobID).Msg("Getting batch job status")

	job, err := s.batchRepo.GetByID(ctx, jobID)
	if err != nil {
		s.logger.Error().Err(err).Str("job_id", jobID).Msg("Failed to get batch job")
		return result.Failure[*domain.BatchJob](err)
	}

	return result.Success(job)
}

// CancelBatchJob cancels a running or queued batch job
func (s *fileActionsService) CancelBatchJob(ctx context.Context, jobID string) result.Result[bool] {
	s.logger.Info().Str("job_id", jobID).Msg("Cancelling batch job")

	// Get job first to check status
	jobResult := s.GetBatchStatus(ctx, jobID)
	if jobResult.IsFailure() {
		return result.Failure[bool](jobResult.Error())
	}

	job := jobResult.Value()

	// Check if job can be cancelled
	if job.Status == domain.JobStatusCompleted || job.Status == domain.JobStatusCancelled {
		return result.Failure[bool](fmt.Errorf("job cannot be cancelled: already %s", job.Status))
	}

	// Cancel via scheduler (handles running jobs)
	if err := s.scheduler.CancelJob(jobID); err != nil {
		s.logger.Error().Err(err).Str("job_id", jobID).Msg("Failed to cancel job via scheduler")
		return result.Failure[bool](err)
	}

	s.logger.Info().Str("job_id", jobID).Msg("Batch job cancelled successfully")
	return result.Success(true)
}

// ListBatchJobs lists batch jobs with optional filtering
func (s *fileActionsService) ListBatchJobs(ctx context.Context, filter BatchJobFilter) result.Result[[]*domain.BatchJob] {
	s.logger.Debug().
		Interface("filter", filter).
		Msg("Listing batch jobs")

	// Apply defaults
	if filter.Limit <= 0 {
		filter.Limit = 50
	}
	if filter.Limit > 100 {
		filter.Limit = 100
	}

	// Query repository based on filter
	var jobs []*domain.BatchJob
	var err error

	if filter.Status != nil {
		// Filter by status
		jobs, err = s.batchRepo.ListByStatus(ctx, *filter.Status, filter.Limit, filter.Offset)
	} else if filter.FromDate != nil || filter.ToDate != nil {
		// Filter by date range
		from := filter.FromDate
		to := filter.ToDate
		if from == nil {
			// Default to 30 days ago
			t := time.Now().AddDate(0, 0, -30)
			from = &t
		}
		if to == nil {
			// Default to now
			t := time.Now()
			to = &t
		}
		jobs, err = s.batchRepo.ListByDateRange(ctx, *from, *to, filter.Limit, filter.Offset)
	} else {
		// List all recent jobs
		jobs, err = s.batchRepo.ListRecent(ctx, filter.Limit, filter.Offset)
	}

	if err != nil {
		s.logger.Error().Err(err).Msg("Failed to list batch jobs")
		return result.Failure[[]*domain.BatchJob](err)
	}

	s.logger.Debug().Int("job_count", len(jobs)).Msg("Retrieved batch jobs")
	return result.Success(jobs)
}

// ValidateBatch performs pre-flight validation without creating a job
func (s *fileActionsService) ValidateBatch(ctx context.Context, request BatchValidationRequest) result.Result[BatchValidationResult] {
	s.logger.Debug().
		Int("file_count", len(request.FileHashes)).
		Msg("Validating batch request")

	result := BatchValidationResult{
		Valid:      true,
		TotalFiles: len(request.FileHashes),
		ValidFiles: 0,
		InvalidOps: []InvalidOperationError{},
	}

	// Validate each file
	for _, hash := range request.FileHashes {
		// Get file from database
		fileResult := s.fileRepo.GetByHash(ctx, hash)
		if fileResult.IsFailure() {
			result.Valid = false
			result.InvalidOps = append(result.InvalidOps, InvalidOperationError{
				FileHash: hash,
				Reason:   fmt.Sprintf("File not found: %v", fileResult.Error()),
			})
			continue
		}

		file := fileResult.Value()

		// Check if file is in valid state for organization
		if file.Status == domain.FileStatusMoved {
			result.Valid = false
			result.InvalidOps = append(result.InvalidOps, InvalidOperationError{
				FileHash: hash,
				Reason:   "File already moved",
			})
			continue
		}

		if file.Status == domain.FileStatusIgnored {
			result.Valid = false
			result.InvalidOps = append(result.InvalidOps, InvalidOperationError{
				FileHash: hash,
				Reason:   "File is ignored",
			})
			continue
		}

		if file.Status == domain.FileStatusError {
			result.Valid = false
			result.InvalidOps = append(result.InvalidOps, InvalidOperationError{
				FileHash: hash,
				Reason:   "File in error state",
			})
			continue
		}

		// Check if confirmed category is set
		if file.ConfirmedCategory == nil || *file.ConfirmedCategory == "" {
			result.Valid = false
			result.InvalidOps = append(result.InvalidOps, InvalidOperationError{
				FileHash: hash,
				Reason:   "File category not confirmed",
			})
			continue
		}

		result.ValidFiles++
	}

	s.logger.Info().
		Int("total", result.TotalFiles).
		Int("valid", result.ValidFiles).
		Int("invalid", len(result.InvalidOps)).
		Bool("valid", result.Valid).
		Msg("Batch validation completed")

	return Success(result)
}

// validateBatchRequest validates the batch organization request
func (s *fileActionsService) validateBatchRequest(request BatchOrganizeRequest) error {
	if request.BatchName == "" {
		return fmt.Errorf("batch name is required")
	}

	if len(request.Operations) == 0 {
		return fmt.Errorf("no operations provided")
	}

	if len(request.Operations) > 1000 {
		return fmt.Errorf("batch size exceeds maximum of 1000 operations")
	}

	if request.MaxConcurrency <= 0 {
		request.MaxConcurrency = 1
	}

	// Validate each operation
	for i, op := range request.Operations {
		if op.TrackedFile == nil {
			return fmt.Errorf("operation %d: tracked file is required", i)
		}
		if op.TrackedFile.Hash == "" {
			return fmt.Errorf("operation %d: file hash is required", i)
		}
		if op.ConfirmedCategory == "" {
			return fmt.Errorf("operation %d: confirmed category is required", i)
		}
		if op.TargetPath == "" {
			return fmt.Errorf("operation %d: target path is required", i)
		}
	}

	return nil
}

// Success is a helper to create a successful result
func Success[T any](value T) result.Result[T] {
	return result.Success(value)
}
