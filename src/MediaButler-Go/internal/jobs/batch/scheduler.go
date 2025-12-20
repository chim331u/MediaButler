package batch

import (
	"context"
	"sync"
	"time"

	"github.com/chim331u/mediabutler-go/internal/repository"
	"github.com/rs/zerolog"
)

// SchedulerConfig holds scheduler configuration
type SchedulerConfig struct {
	WorkerCount      int           // Number of concurrent workers
	PollInterval     time.Duration // Interval for polling database for new jobs
	JobTimeout       time.Duration // Maximum time for job execution
	QueueBufferSize  int           // Size of job queue buffer
	ShutdownTimeout  time.Duration // Time to wait for graceful shutdown
}

// DefaultSchedulerConfig returns default configuration optimized for ARM32
func DefaultSchedulerConfig() SchedulerConfig {
	return SchedulerConfig{
		WorkerCount:      2,                // ARM32: Limited workers
		PollInterval:     5 * time.Second,  // Poll every 5 seconds
		JobTimeout:       30 * time.Minute, // 30 minute max per job
		QueueBufferSize:  100,              // Buffer up to 100 jobs
		ShutdownTimeout:  30 * time.Second, // 30 seconds to shutdown
	}
}

// Scheduler manages batch job execution with worker pool
type Scheduler struct {
	config      SchedulerConfig
	batchRepo   repository.BatchJobRepository
	executor    Executor
	logger      zerolog.Logger

	// Worker pool
	jobQueue    chan string                    // Job IDs to process
	cancelMap   map[string]context.CancelFunc  // Cancellation functions by job ID
	cancelMutex sync.RWMutex                   // Protect cancelMap

	// Lifecycle
	ctx        context.Context
	cancel     context.CancelFunc
	wg         sync.WaitGroup
	started    bool
	startMutex sync.Mutex
}

// NewScheduler creates a new batch job scheduler
func NewScheduler(
	config SchedulerConfig,
	batchRepo repository.BatchJobRepository,
	executor Executor,
	logger zerolog.Logger,
) *Scheduler {
	ctx, cancel := context.WithCancel(context.Background())

	return &Scheduler{
		config:    config,
		batchRepo: batchRepo,
		executor:  executor,
		logger:    logger.With().Str("component", "batch-scheduler").Logger(),
		jobQueue:  make(chan string, config.QueueBufferSize),
		cancelMap: make(map[string]context.CancelFunc),
		ctx:       ctx,
		cancel:    cancel,
		started:   false,
	}
}

// Start starts the scheduler and worker pool
func (s *Scheduler) Start() error {
	s.startMutex.Lock()
	defer s.startMutex.Unlock()

	if s.started {
		return nil
	}

	s.logger.Info().
		Int("worker_count", s.config.WorkerCount).
		Dur("poll_interval", s.config.PollInterval).
		Msg("Starting batch job scheduler")

	// Start workers
	for i := 0; i < s.config.WorkerCount; i++ {
		s.wg.Add(1)
		go s.worker(i)
	}

	// Start job poller
	s.wg.Add(1)
	go s.poller()

	s.started = true
	s.logger.Info().Msg("Batch job scheduler started")

	return nil
}

// Stop stops the scheduler gracefully
func (s *Scheduler) Stop() error {
	s.startMutex.Lock()
	defer s.startMutex.Unlock()

	if !s.started {
		return nil
	}

	s.logger.Info().Msg("Stopping batch job scheduler")

	// Cancel all running jobs
	s.cancelAllJobs()

	// Signal shutdown
	s.cancel()

	// Wait for workers to finish with timeout
	done := make(chan struct{})
	go func() {
		s.wg.Wait()
		close(done)
	}()

	select {
	case <-done:
		s.logger.Info().Msg("Batch job scheduler stopped gracefully")
	case <-time.After(s.config.ShutdownTimeout):
		s.logger.Warn().
			Dur("timeout", s.config.ShutdownTimeout).
			Msg("Scheduler shutdown timeout exceeded")
	}

	s.started = false
	return nil
}

// CancelJob cancels a specific job
func (s *Scheduler) CancelJob(jobID string) error {
	s.cancelMutex.Lock()
	defer s.cancelMutex.Unlock()

	cancelFunc, exists := s.cancelMap[jobID]
	if !exists {
		s.logger.Debug().Str("job_id", jobID).Msg("Job not currently running, marking as cancelled in database")
		// Job not running, mark as cancelled in database
		return s.batchRepo.MarkCancelled(context.Background(), jobID)
	}

	s.logger.Info().Str("job_id", jobID).Msg("Cancelling running job")
	cancelFunc()

	// Remove from map
	delete(s.cancelMap, jobID)

	return nil
}

// poller continuously polls database for queued jobs
func (s *Scheduler) poller() {
	defer s.wg.Done()

	ticker := time.NewTicker(s.config.PollInterval)
	defer ticker.Stop()

	s.logger.Info().
		Dur("interval", s.config.PollInterval).
		Msg("Job poller started")

	for {
		select {
		case <-s.ctx.Done():
			s.logger.Info().Msg("Job poller stopped")
			return

		case <-ticker.C:
			s.pollForJobs()
		}
	}
}

// pollForJobs queries database for queued jobs and enqueues them
func (s *Scheduler) pollForJobs() {
	ctx, cancel := context.WithTimeout(s.ctx, 5*time.Second)
	defer cancel()

	// Get queued jobs (respects retry delay)
	jobs, err := s.batchRepo.GetQueued(ctx, 10)
	if err != nil {
		s.logger.Error().Err(err).Msg("Failed to poll for queued jobs")
		return
	}

	if len(jobs) == 0 {
		return
	}

	s.logger.Debug().Int("job_count", len(jobs)).Msg("Found queued jobs")

	// Enqueue jobs
	for _, job := range jobs {
		select {
		case s.jobQueue <- job.ID:
			s.logger.Info().
				Str("job_id", job.ID).
				Str("batch_name", job.BatchName).
				Int("total_files", job.TotalFiles).
				Msg("Enqueued job for processing")

		case <-s.ctx.Done():
			return

		default:
			s.logger.Warn().
				Str("job_id", job.ID).
				Msg("Job queue full, will retry next poll")
		}
	}
}

// worker processes jobs from the queue
func (s *Scheduler) worker(id int) {
	defer s.wg.Done()

	s.logger.Info().Int("worker_id", id).Msg("Worker started")

	for {
		select {
		case <-s.ctx.Done():
			s.logger.Info().Int("worker_id", id).Msg("Worker stopped")
			return

		case jobID := <-s.jobQueue:
			s.processJob(id, jobID)
		}
	}
}

// processJob executes a single job
func (s *Scheduler) processJob(workerID int, jobID string) {
	s.logger.Info().
		Int("worker_id", workerID).
		Str("job_id", jobID).
		Msg("Worker processing job")

	// Create job context with timeout and cancellation
	ctx, cancel := context.WithTimeout(s.ctx, s.config.JobTimeout)
	defer cancel()

	// Register cancellation function
	s.registerCancellation(jobID, cancel)
	defer s.unregisterCancellation(jobID)

	// Get job from database
	job, err := s.batchRepo.GetByID(ctx, jobID)
	if err != nil {
		s.logger.Error().
			Err(err).
			Str("job_id", jobID).
			Msg("Failed to get job from database")
		return
	}

	// Execute job
	startTime := time.Now()
	err = s.executor.Execute(ctx, job)
	duration := time.Since(startTime)

	if err != nil {
		if err == context.Canceled {
			s.logger.Warn().
				Int("worker_id", workerID).
				Str("job_id", jobID).
				Dur("duration", duration).
				Msg("Job cancelled")
		} else if err == context.DeadlineExceeded {
			s.logger.Error().
				Int("worker_id", workerID).
				Str("job_id", jobID).
				Dur("duration", duration).
				Dur("timeout", s.config.JobTimeout).
				Msg("Job timeout exceeded")

			// Mark job as failed
			s.batchRepo.MarkFailed(context.Background(), jobID, "Job timeout exceeded", false)
		} else {
			s.logger.Error().
				Err(err).
				Int("worker_id", workerID).
				Str("job_id", jobID).
				Dur("duration", duration).
				Msg("Job execution failed")
		}
	} else {
		s.logger.Info().
			Int("worker_id", workerID).
			Str("job_id", jobID).
			Dur("duration", duration).
			Msg("Job completed successfully")
	}
}

// registerCancellation registers a cancel function for a job
func (s *Scheduler) registerCancellation(jobID string, cancelFunc context.CancelFunc) {
	s.cancelMutex.Lock()
	defer s.cancelMutex.Unlock()

	s.cancelMap[jobID] = cancelFunc
}

// unregisterCancellation removes a cancel function for a job
func (s *Scheduler) unregisterCancellation(jobID string) {
	s.cancelMutex.Lock()
	defer s.cancelMutex.Unlock()

	delete(s.cancelMap, jobID)
}

// cancelAllJobs cancels all currently running jobs
func (s *Scheduler) cancelAllJobs() {
	s.cancelMutex.Lock()
	defer s.cancelMutex.Unlock()

	s.logger.Info().Int("job_count", len(s.cancelMap)).Msg("Cancelling all running jobs")

	for jobID, cancelFunc := range s.cancelMap {
		s.logger.Debug().Str("job_id", jobID).Msg("Cancelling job")
		cancelFunc()
	}

	// Clear map
	s.cancelMap = make(map[string]context.CancelFunc)
}

// GetStats returns scheduler statistics
func (s *Scheduler) GetStats() map[string]interface{} {
	s.cancelMutex.RLock()
	runningJobs := len(s.cancelMap)
	s.cancelMutex.RUnlock()

	return map[string]interface{}{
		"worker_count":    s.config.WorkerCount,
		"running_jobs":    runningJobs,
		"queue_length":    len(s.jobQueue),
		"queue_capacity":  s.config.QueueBufferSize,
		"poll_interval":   s.config.PollInterval.String(),
		"started":         s.started,
	}
}
