package progress

import (
	"time"

	"github.com/lucapaganotti/mediabutler-go/internal/sse"
	"github.com/rs/zerolog"
)

// ProgressReporter interface for reporting job progress via SSE
type ProgressReporter interface {
	ReportJobStarted(jobID, jobType string, totalFiles int) error
	ReportProgress(jobID string, processed, total, succeeded, failed int, currentFileName string) error
	ReportJobCompleted(jobID, jobType string, total, succeeded, failed int, duration time.Duration) error
	ReportJobFailed(jobID, jobType string, errorMsg string, processed int) error
}

// progressReporter implements ProgressReporter using SSE broker
type progressReporter struct {
	broker *sse.Broker
	logger zerolog.Logger
}

// NewProgressReporter creates a new progress reporter
func NewProgressReporter(broker *sse.Broker, logger zerolog.Logger) ProgressReporter {
	return &progressReporter{
		broker: broker,
		logger: logger.With().Str("component", "progress-reporter").Logger(),
	}
}

// ReportJobStarted broadcasts a batch.started event
func (r *progressReporter) ReportJobStarted(jobID, jobType string, totalFiles int) error {
	event := sse.BatchStartedEvent{
		BatchID:    jobID,
		TotalFiles: totalFiles,
		StartTime:  time.Now(),
	}

	if err := r.broker.Broadcast(sse.EventBatchStarted, event); err != nil {
		r.logger.Error().
			Err(err).
			Str("job_id", jobID).
			Str("job_type", jobType).
			Msg("Failed to broadcast job started event")
		return err
	}

	r.logger.Info().
		Str("job_id", jobID).
		Str("job_type", jobType).
		Int("total_files", totalFiles).
		Msg("Broadcast job started event")

	return nil
}

// ReportProgress broadcasts a batch.progress event
func (r *progressReporter) ReportProgress(jobID string, processed, total, succeeded, failed int, currentFileName string) error {
	event := sse.BatchProgressEvent{
		BatchID:   jobID,
		Processed: processed,
		Total:     total,
		Succeeded: succeeded,
		Failed:    failed,
	}

	if err := r.broker.Broadcast(sse.EventBatchProgress, event); err != nil {
		r.logger.Warn().
			Err(err).
			Str("job_id", jobID).
			Int("processed", processed).
			Int("total", total).
			Msg("Failed to broadcast progress event")
		// Don't return error - progress reporting is non-critical
	}

	r.logger.Debug().
		Str("job_id", jobID).
		Int("processed", processed).
		Int("total", total).
		Int("succeeded", succeeded).
		Int("failed", failed).
		Str("current_file", currentFileName).
		Msg("Broadcast progress event")

	return nil
}

// ReportJobCompleted broadcasts a batch.completed event
func (r *progressReporter) ReportJobCompleted(jobID, jobType string, total, succeeded, failed int, duration time.Duration) error {
	event := sse.BatchCompletedEvent{
		BatchID:       jobID,
		Total:         total,
		Succeeded:     succeeded,
		Failed:        failed,
		CompletedTime: time.Now(),
	}

	if err := r.broker.Broadcast(sse.EventBatchCompleted, event); err != nil {
		r.logger.Error().
			Err(err).
			Str("job_id", jobID).
			Msg("Failed to broadcast job completed event")
		return err
	}

	r.logger.Info().
		Str("job_id", jobID).
		Str("job_type", jobType).
		Int("total", total).
		Int("succeeded", succeeded).
		Int("failed", failed).
		Dur("duration", duration).
		Msg("Broadcast job completed event")

	return nil
}

// ReportJobFailed broadcasts a batch.failed event
func (r *progressReporter) ReportJobFailed(jobID, jobType string, errorMsg string, processed int) error {
	event := sse.BatchCompletedEvent{
		BatchID:       jobID,
		Total:         processed,
		Succeeded:     0,
		Failed:        processed,
		CompletedTime: time.Now(),
	}

	// Broadcast as batch.failed event
	if err := r.broker.Broadcast(sse.EventBatchFailed, event); err != nil {
		r.logger.Error().
			Err(err).
			Str("job_id", jobID).
			Msg("Failed to broadcast job failed event")
		return err
	}

	r.logger.Warn().
		Str("job_id", jobID).
		Str("job_type", jobType).
		Str("error", errorMsg).
		Int("processed", processed).
		Msg("Broadcast job failed event")

	return nil
}

// NoOpProgressReporter is a no-op implementation for testing
type NoOpProgressReporter struct{}

func (n *NoOpProgressReporter) ReportJobStarted(jobID, jobType string, totalFiles int) error {
	return nil
}

func (n *NoOpProgressReporter) ReportProgress(jobID string, processed, total, succeeded, failed int, currentFileName string) error {
	return nil
}

func (n *NoOpProgressReporter) ReportJobCompleted(jobID, jobType string, total, succeeded, failed int, duration time.Duration) error {
	return nil
}

func (n *NoOpProgressReporter) ReportJobFailed(jobID, jobType string, errorMsg string, processed int) error {
	return nil
}
