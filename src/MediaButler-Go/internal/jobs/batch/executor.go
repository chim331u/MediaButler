package batch

import (
	"context"
	"fmt"
	"time"

	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/internal/jobs/progress"
	"github.com/lucapaganotti/mediabutler-go/internal/repository"
	"github.com/lucapaganotti/mediabutler-go/internal/service"
	"github.com/rs/zerolog"
)

const JobTypeBatchFileProcessing = "batch.file.processing"

// Executor handles batch job execution
type Executor interface {
	Execute(ctx context.Context, job *domain.BatchJob) error
}

// executor implements Executor
type executor struct {
	batchRepo             repository.BatchJobRepository
	fileRepo              repository.FileRepository
	fileOrganizationService service.FileOrganizationService
	progressReporter      progress.ProgressReporter
	throttler             progress.BatchThrottler
	logger                zerolog.Logger
}

// NewExecutor creates a new batch job executor
func NewExecutor(
	batchRepo repository.BatchJobRepository,
	fileRepo repository.FileRepository,
	fileOrgService service.FileOrganizationService,
	progressReporter progress.ProgressReporter,
	throttler progress.BatchThrottler,
	logger zerolog.Logger,
) Executor {
	return &executor{
		batchRepo:             batchRepo,
		fileRepo:              fileRepo,
		fileOrganizationService: fileOrgService,
		progressReporter:      progressReporter,
		throttler:             throttler,
		logger:                logger.With().Str("component", "batch-executor").Logger(),
	}
}

// Execute processes a batch job
func (e *executor) Execute(ctx context.Context, job *domain.BatchJob) error {
	startTime := time.Now()
	e.logger.Info().
		Str("job_id", job.ID).
		Str("batch_name", job.BatchName).
		Int("total_files", job.TotalFiles).
		Msg("Starting batch job execution")

	// Mark job as started
	if err := e.batchRepo.MarkStarted(ctx, job.ID); err != nil {
		e.logger.Error().Err(err).Str("job_id", job.ID).Msg("Failed to mark job as started")
		return fmt.Errorf("mark job started: %w", err)
	}

	// Report job started
	if err := e.progressReporter.ReportJobStarted(job.ID, JobTypeBatchFileProcessing, job.TotalFiles); err != nil {
		e.logger.Warn().Err(err).Msg("Failed to report job started")
		// Non-fatal, continue
	}

	// Get all job items
	items, err := e.batchRepo.GetItemsWithFileDetails(ctx, job.ID)
	if err != nil {
		e.logger.Error().Err(err).Str("job_id", job.ID).Msg("Failed to get job items")
		return e.handleJobFailure(ctx, job, fmt.Sprintf("Failed to get job items: %v", err), 0)
	}

	if len(items) == 0 {
		e.logger.Warn().Str("job_id", job.ID).Msg("No items found for batch job")
		return e.handleJobCompletion(ctx, job, 0, 0, 0, time.Since(startTime))
	}

	// Process each item
	var processedCount, successCount, failedCount int
	var errors []string

	for _, item := range items {
		// Check for cancellation
		select {
		case <-ctx.Done():
			e.logger.Warn().Str("job_id", job.ID).Msg("Job cancelled")
			return e.handleJobCancellation(ctx, job, processedCount)
		default:
			// Continue processing
		}

		// Process the item
		itemStartTime := time.Now()
		err := e.processItem(ctx, item)
		processingTimeMs := time.Since(itemStartTime).Milliseconds()

		if err != nil {
			failedCount++
			errorMsg := fmt.Sprintf("%s: %v", *item.FileName, err)
			errors = append(errors, errorMsg)

			e.logger.Warn().
				Err(err).
				Str("file_hash", item.FileHash).
				Str("file_name", *item.FileName).
				Msg("Failed to process file")

			// Mark item as failed
			if dbErr := e.batchRepo.MarkItemFailed(ctx, item.ID, err.Error(), processingTimeMs); dbErr != nil {
				e.logger.Error().Err(dbErr).Int64("item_id", item.ID).Msg("Failed to mark item as failed")
			}

			// Check if we should continue on error
			if !job.ContinueOnError {
				e.logger.Error().
					Str("job_id", job.ID).
					Msg("Stopping batch job due to error (ContinueOnError=false)")
				return e.handleJobFailure(ctx, job, errorMsg, processedCount)
			}
		} else {
			successCount++
			e.logger.Debug().
				Str("file_hash", item.FileHash).
				Str("file_name", *item.FileName).
				Msg("Successfully processed file")
		}

		processedCount++

		// Update job progress in database
		if err := e.batchRepo.UpdateProgress(ctx, job.ID, processedCount, successCount, failedCount); err != nil {
			e.logger.Warn().Err(err).Msg("Failed to update job progress in database")
			// Non-fatal, continue
		}

		// Report progress
		fileName := "unknown"
		if item.FileName != nil {
			fileName = *item.FileName
		}

		if err := e.progressReporter.ReportProgress(job.ID, processedCount, job.TotalFiles, successCount, failedCount, fileName); err != nil {
			e.logger.Debug().Err(err).Msg("Failed to report progress")
			// Non-fatal, continue
		}

		// Apply throttling
		if e.throttler.ShouldThrottle(processedCount, job.TotalFiles) {
			if err := e.throttler.Throttle(ctx); err != nil {
				// Context cancelled during throttle
				e.logger.Warn().Err(err).Msg("Throttle interrupted")
				return e.handleJobCancellation(ctx, job, processedCount)
			}

			// Log memory usage periodically
			if processedCount%e.getMemoryCheckInterval() == 0 {
				memUsage := e.throttler.GetMemoryUsage()
				e.logger.Debug().
					Uint64("memory_mb", memUsage).
					Int("processed", processedCount).
					Int("total", job.TotalFiles).
					Msg("Memory usage check")
			}
		}
	}

	// Job completed
	duration := time.Since(startTime)
	e.logger.Info().
		Str("job_id", job.ID).
		Int("total", job.TotalFiles).
		Int("succeeded", successCount).
		Int("failed", failedCount).
		Dur("duration", duration).
		Msg("Batch job completed")

	return e.handleJobCompletion(ctx, job, processedCount, successCount, failedCount, duration)
}

// processItem processes a single batch job item
func (e *executor) processItem(ctx context.Context, item *domain.BatchJobItem) error {
	// Mark item as processing
	if err := e.batchRepo.MarkItemProcessing(ctx, item.ID); err != nil {
		return fmt.Errorf("mark item processing: %w", err)
	}

	// Check if file details are available
	if item.FileName == nil {
		return fmt.Errorf("file not found in database")
	}

	// Call file organization service
	result := e.fileOrganizationService.OrganizeFile(ctx, item.FileHash, item.ConfirmedCategory)

	if result.IsFailure() {
		return fmt.Errorf("organize file failed: %s", result.Error())
	}

	// Get organized file result
	organizedFile := result.Value()

	// Mark item as completed
	targetPath := organizedFile.TargetPath
	actualPath := organizedFile.ActualPath
	if actualPath == "" {
		actualPath = targetPath
	}

	processingTimeMs := time.Since(item.CreatedDate).Milliseconds()
	if err := e.batchRepo.MarkItemCompleted(ctx, item.ID, targetPath, actualPath, processingTimeMs); err != nil {
		e.logger.Warn().Err(err).Int64("item_id", item.ID).Msg("Failed to mark item as completed")
		// Non-fatal, file was already organized
	}

	return nil
}

// handleJobCompletion handles successful job completion
func (e *executor) handleJobCompletion(ctx context.Context, job *domain.BatchJob, processed, succeeded, failed int, duration time.Duration) error {
	// Determine final status
	finalStatus := domain.JobStatusCompleted
	if failed > 0 && succeeded == 0 {
		finalStatus = domain.JobStatusFailed
	}

	// Update job in database
	if err := e.batchRepo.MarkCompleted(ctx, job.ID, finalStatus, processed, succeeded, failed); err != nil {
		e.logger.Error().Err(err).Str("job_id", job.ID).Msg("Failed to mark job as completed")
		return fmt.Errorf("mark job completed: %w", err)
	}

	// Report job completed
	if err := e.progressReporter.ReportJobCompleted(job.ID, JobTypeBatchFileProcessing, processed, succeeded, failed, duration); err != nil {
		e.logger.Warn().Err(err).Msg("Failed to report job completed")
		// Non-fatal
	}

	return nil
}

// handleJobFailure handles job failure
func (e *executor) handleJobFailure(ctx context.Context, job *domain.BatchJob, errorMsg string, processed int) error {
	e.logger.Error().
		Str("job_id", job.ID).
		Str("error", errorMsg).
		Int("processed", processed).
		Msg("Batch job failed")

	// Check if job can be retried
	canRetry := job.CanRetry()

	// Mark job as failed (with retry flag)
	if err := e.batchRepo.MarkFailed(ctx, job.ID, errorMsg, canRetry); err != nil {
		e.logger.Error().Err(err).Str("job_id", job.ID).Msg("Failed to mark job as failed")
		return fmt.Errorf("mark job failed: %w", err)
	}

	// Report job failed
	if err := e.progressReporter.ReportJobFailed(job.ID, JobTypeBatchFileProcessing, errorMsg, processed); err != nil {
		e.logger.Warn().Err(err).Msg("Failed to report job failed")
		// Non-fatal
	}

	if canRetry {
		e.logger.Info().
			Str("job_id", job.ID).
			Int("retry_count", job.RetryCount+1).
			Int("max_retries", job.MaxRetries).
			Msg("Job will be retried")
	}

	return fmt.Errorf("job failed: %s", errorMsg)
}

// handleJobCancellation handles job cancellation
func (e *executor) handleJobCancellation(ctx context.Context, job *domain.BatchJob, processed int) error {
	e.logger.Warn().
		Str("job_id", job.ID).
		Int("processed", processed).
		Int("total", job.TotalFiles).
		Msg("Batch job cancelled")

	// Mark job as cancelled
	if err := e.batchRepo.MarkCancelled(ctx, job.ID); err != nil {
		e.logger.Error().Err(err).Str("job_id", job.ID).Msg("Failed to mark job as cancelled")
		return fmt.Errorf("mark job cancelled: %w", err)
	}

	// Report job failed (as cancelled)
	if err := e.progressReporter.ReportJobFailed(job.ID, JobTypeBatchFileProcessing, "Job cancelled", processed); err != nil {
		e.logger.Warn().Err(err).Msg("Failed to report job cancellation")
		// Non-fatal
	}

	return context.Canceled
}

// getMemoryCheckInterval returns the interval for memory usage checks
func (e *executor) getMemoryCheckInterval() int {
	return 5 // Check every 5 files
}
