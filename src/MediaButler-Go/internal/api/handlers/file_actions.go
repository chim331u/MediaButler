package handlers

import (
	"encoding/json"
	"net/http"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/internal/service"
	"github.com/rs/zerolog/log"
)

// FileActionsHandler handles batch file operation endpoints
type FileActionsHandler struct {
	fileActionsService service.FileActionsService
}

// NewFileActionsHandler creates a new FileActionsHandler
func NewFileActionsHandler(fileActionsService service.FileActionsService) *FileActionsHandler {
	return &FileActionsHandler{
		fileActionsService: fileActionsService,
	}
}

// BatchOrganizeResponse represents the response for organize-batch
type BatchOrganizeResponse struct {
	JobID      string    `json:"jobId"`
	BatchName  string    `json:"batchName"`
	TotalFiles int       `json:"totalFiles"`
	QueuedAt   time.Time `json:"queuedAt"`
}

// BatchStatusResponse represents the response for batch-status
type BatchStatusResponse struct {
	JobID           string               `json:"jobId"`
	BatchName       string               `json:"batchName"`
	Status          string               `json:"status"`
	QueuedAt        time.Time            `json:"queuedAt"`
	StartedAt       *time.Time           `json:"startedAt,omitempty"`
	CompletedAt     *time.Time           `json:"completedAt,omitempty"`
	TotalFiles      int                  `json:"totalFiles"`
	ProcessedFiles  int                  `json:"processedFiles"`
	SuccessfulFiles int                  `json:"successfulFiles"`
	FailedFiles     int                  `json:"failedFiles"`
	Progress        int                  `json:"progress"`
	ContinueOnError bool                 `json:"continueOnError"`
	DryRun          bool                 `json:"dryRun"`
	RetryCount      int                  `json:"retryCount"`
	MaxRetries      int                  `json:"maxRetries"`
	ErrorMessage    *string              `json:"errorMessage,omitempty"`
	Metadata        map[string]interface{} `json:"metadata,omitempty"`
}

// BatchJobListResponse represents the response for listing batch jobs
type BatchJobListResponse struct {
	Jobs       []*BatchStatusResponse `json:"jobs"`
	TotalCount int                    `json:"totalCount"`
}

// OrganizeBatch handles POST /api/v1/file-actions/organize-batch
// Creates a new batch organization job
func (h *FileActionsHandler) OrganizeBatch(w http.ResponseWriter, r *http.Request) {
	var request service.BatchOrganizeRequest
	if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
		log.Error().Err(err).Msg("Failed to decode batch organize request")
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	// Debug logging
	batchName := "Batch Operation"
	if request.BatchName != nil {
		batchName = *request.BatchName
	}
	log.Info().
		Str("batch_name", batchName).
		Int("file_count", len(request.Files)).
		Bool("continue_on_error", request.ContinueOnError).
		Bool("dry_run", request.DryRun).
		Msg("Received batch organize request")

	// Call service
	result := h.fileActionsService.OrganizeBatch(r.Context(), request)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to create batch job")
		respondError(w, http.StatusBadRequest, result.Error().Error())
		return
	}

	jobID := result.Value()

	// Get job details for response
	jobResult := h.fileActionsService.GetBatchStatus(r.Context(), jobID)
	if jobResult.IsFailure() {
		log.Error().Err(jobResult.Error()).Str("job_id", jobID).Msg("Failed to get job details")
		respondError(w, http.StatusInternalServerError, "Job created but failed to retrieve details")
		return
	}

	job := jobResult.Value()
	response := BatchOrganizeResponse{
		JobID:      job.ID,
		BatchName:  job.BatchName,
		TotalFiles: job.TotalFiles,
		QueuedAt:   job.QueuedAt,
	}

	respondJSON(w, http.StatusCreated, response)
}

// GetBatchStatus handles GET /api/v1/file-actions/batch-status/{jobId}
// Retrieves the status of a batch job
func (h *FileActionsHandler) GetBatchStatus(w http.ResponseWriter, r *http.Request) {
	jobID := chi.URLParam(r, "jobId")
	if jobID == "" {
		respondError(w, http.StatusBadRequest, "jobId parameter is required")
		return
	}

	result := h.fileActionsService.GetBatchStatus(r.Context(), jobID)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("job_id", jobID).Msg("Failed to get batch status")
		respondError(w, http.StatusNotFound, "Batch job not found")
		return
	}

	job := result.Value()
	response := toBatchStatusResponse(job)

	respondJSON(w, http.StatusOK, response)
}

// CancelBatchJob handles POST /api/v1/file-actions/batch-cancel/{jobId}
// Cancels a running or queued batch job
func (h *FileActionsHandler) CancelBatchJob(w http.ResponseWriter, r *http.Request) {
	jobID := chi.URLParam(r, "jobId")
	if jobID == "" {
		respondError(w, http.StatusBadRequest, "jobId parameter is required")
		return
	}

	result := h.fileActionsService.CancelBatchJob(r.Context(), jobID)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("job_id", jobID).Msg("Failed to cancel batch job")
		respondError(w, http.StatusBadRequest, result.Error().Error())
		return
	}

	// Return updated job status
	statusResult := h.fileActionsService.GetBatchStatus(r.Context(), jobID)
	if statusResult.IsFailure() {
		log.Error().Err(statusResult.Error()).Str("job_id", jobID).Msg("Failed to get job status after cancel")
		respondError(w, http.StatusInternalServerError, "Job cancelled but failed to retrieve status")
		return
	}

	job := statusResult.Value()
	response := toBatchStatusResponse(job)

	respondJSON(w, http.StatusOK, response)
}

// ListBatchJobs handles GET /api/v1/file-actions/batch-jobs
// Lists batch jobs with optional filtering
func (h *FileActionsHandler) ListBatchJobs(w http.ResponseWriter, r *http.Request) {
	// Parse query parameters
	statusStr := r.URL.Query().Get("status")
	limit := parseInt(r.URL.Query().Get("limit"), 50)
	offset := parseInt(r.URL.Query().Get("offset"), 0)

	// Parse dates
	var fromDate, toDate *time.Time
	if fromStr := r.URL.Query().Get("fromDate"); fromStr != "" {
		if t, err := time.Parse(time.RFC3339, fromStr); err == nil {
			fromDate = &t
		} else {
			log.Warn().Str("fromDate", fromStr).Msg("Invalid fromDate format, ignoring")
		}
	}
	if toStr := r.URL.Query().Get("toDate"); toStr != "" {
		if t, err := time.Parse(time.RFC3339, toStr); err == nil {
			toDate = &t
		} else {
			log.Warn().Str("toDate", toStr).Msg("Invalid toDate format, ignoring")
		}
	}

	// Build filter
	filter := service.BatchJobFilter{
		Limit:  limit,
		Offset: offset,
	}

	if statusStr != "" {
		status, err := domain.ParseJobStatus(statusStr)
		if err != nil {
			respondError(w, http.StatusBadRequest, "Invalid status parameter")
			return
		}
		filter.Status = &status
	}

	if fromDate != nil {
		filter.FromDate = fromDate
	}
	if toDate != nil {
		filter.ToDate = toDate
	}

	// Call service
	result := h.fileActionsService.ListBatchJobs(r.Context(), filter)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to list batch jobs")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve batch jobs")
		return
	}

	jobs := result.Value()
	responses := make([]*BatchStatusResponse, len(jobs))
	for i, job := range jobs {
		responses[i] = toBatchStatusResponse(job)
	}

	response := BatchJobListResponse{
		Jobs:       responses,
		TotalCount: len(responses), // Simplified - in production, need actual total count
	}

	respondJSON(w, http.StatusOK, response)
}

// ValidateBatch handles POST /api/v1/file-actions/validate-batch
// Validates a batch request without creating a job
func (h *FileActionsHandler) ValidateBatch(w http.ResponseWriter, r *http.Request) {
	var request service.BatchValidationRequest
	if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
		log.Error().Err(err).Msg("Failed to decode validation request")
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	result := h.fileActionsService.ValidateBatch(r.Context(), request)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to validate batch")
		respondError(w, http.StatusInternalServerError, "Validation failed")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}

// toBatchStatusResponse converts a BatchJob to a BatchStatusResponse
func toBatchStatusResponse(job *domain.BatchJob) *BatchStatusResponse {
	response := &BatchStatusResponse{
		JobID:           job.ID,
		BatchName:       job.BatchName,
		Status:          string(job.Status),
		QueuedAt:        job.QueuedAt,
		TotalFiles:      job.TotalFiles,
		ProcessedFiles:  job.ProcessedFiles,
		SuccessfulFiles: job.SuccessfulFiles,
		FailedFiles:     job.FailedFiles,
		Progress:        job.ProgressPercentage(),
		ContinueOnError: job.ContinueOnError,
		DryRun:          job.DryRun,
		RetryCount:      job.RetryCount,
		MaxRetries:      job.MaxRetries,
	}

	if job.StartedAt != nil && !job.StartedAt.IsZero() {
		response.StartedAt = job.StartedAt
	}

	if job.CompletedAt != nil && !job.CompletedAt.IsZero() {
		response.CompletedAt = job.CompletedAt
	}

	if job.ErrorMessage != nil {
		response.ErrorMessage = job.ErrorMessage
	}

	if job.Metadata != nil {
		response.Metadata = job.Metadata
	}

	return response
}
