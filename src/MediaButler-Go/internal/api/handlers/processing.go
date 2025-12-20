package handlers

import (
	"bytes"
	"encoding/json"
	"io"
	"net/http"

	"github.com/chim331u/mediabutler-go/internal/service"
	"github.com/go-chi/chi/v5"
	"github.com/rs/zerolog/log"
)

// ProcessingHandler handles processing-related endpoints
type ProcessingHandler struct {
	fileService  service.FileService
	statsService service.StatsService
	mlClient     service.MLClient
}

// NewProcessingHandler creates a new ProcessingHandler
func NewProcessingHandler(fileService service.FileService, statsService service.StatsService, mlClient service.MLClient) *ProcessingHandler {
	return &ProcessingHandler{
		fileService:  fileService,
		statsService: statsService,
		mlClient:     mlClient,
	}
}

// GetProcessingStats handles GET /api/stats/processing
// Returns processing statistics
func (h *ProcessingHandler) GetProcessingStats(w http.ResponseWriter, r *http.Request) {
	result := h.statsService.GetProcessingStats(r.Context())
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to get processing stats")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve processing statistics")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}

// GetQueueStatus handles GET /api/processing/queue/status
// Returns current queue status
func (h *ProcessingHandler) GetQueueStatus(w http.ResponseWriter, r *http.Request) {
	// Get files in various processing stages
	readyForClassification := h.fileService.GetReadyForClassification(r.Context(), 100)
	readyForMoving := h.fileService.GetReadyForMoving(r.Context(), 100)

	var classificationCount, movingCount int
	if readyForClassification.IsSuccess() {
		classificationCount = len(readyForClassification.Value())
	}
	if readyForMoving.IsSuccess() {
		movingCount = len(readyForMoving.Value())
	}

	queueStatus := map[string]interface{}{
		"readyForClassification": classificationCount,
		"readyForMoving":         movingCount,
		"activeWorkers":          0, // TODO: Implement when background workers are added
		"status":                 "operational",
	}

	respondJSON(w, http.StatusOK, queueStatus)
}

// IgnoreFile handles POST /api/v1/file-actions/ignore/{hash}
// Marks a file as ignored
func (h *ProcessingHandler) IgnoreFile(w http.ResponseWriter, r *http.Request) {
	hash := chi.URLParam(r, "hash")
	if hash == "" {
		respondError(w, http.StatusBadRequest, "hash parameter is required")
		return
	}

	// Optional reason from request body
	var req struct {
		Reason *string `json:"reason,omitempty"`
	}

	// Try to decode body, but don't fail if empty
	_ = r.Body.Close()                               // Ensure body is closed
	r.Body = http.MaxBytesReader(w, r.Body, 1048576) // 1MB limit
	if r.ContentLength > 0 {
		if err := decodeJSON(r, &req); err != nil {
			// Log error but continue with nil reason
			log.Warn().Err(err).Msg("Failed to decode ignore reason, continuing without reason")
		}
	}

	result := h.fileService.MarkAsIgnored(r.Context(), hash, req.Reason)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("hash", hash).Msg("Failed to ignore file")

		// Check if file not found
		existsResult := h.fileService.ExistsByHash(r.Context(), hash)
		if existsResult.IsSuccess() && !existsResult.Value() {
			respondError(w, http.StatusNotFound, "File not found")
			return
		}

		respondError(w, http.StatusInternalServerError, "Failed to ignore file")
		return
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"success": true,
		"message": "File marked as ignored",
	})
}

// Helper function to decode JSON
func decodeJSON(r *http.Request, v interface{}) error {
	decoder := json.NewDecoder(r.Body)
	return decoder.Decode(v)
}

// MLEvaluationRequest matches the frontend definition
type MLEvaluationRequest struct {
	FilterByCategory  *string `json:"filterByCategory"`
	ForceReEvaluation bool    `json:"forceReEvaluation"`
}

// MLEvaluationResponse matches the frontend definition
type MLEvaluationResponse struct {
	Success                        bool   `json:"success"`
	TotalFilesQueued               int    `json:"totalFilesQueued"`
	Message                        string `json:"message"`
	QueuedAt                       string `json:"queuedAt"` // Simplification for JSON marshaling
	EstimatedProcessingTimeMinutes int    `json:"estimatedProcessingTimeMinutes"`
}

// QueueForMLEvaluation handles POST /api/processing/ml-evaluation/queue
// Queues files for ML evaluation
func (h *ProcessingHandler) QueueForMLEvaluation(w http.ResponseWriter, r *http.Request) {
	// Debug: Read body first to log on error
	bodyBytes, err := io.ReadAll(r.Body)
	if err != nil {
		log.Error().Err(err).Msg("Failed to read request body")
		respondError(w, http.StatusInternalServerError, "Failed to read request body")
		return
	}
	r.Body.Close() // Close original body

	// Re-create body for decoder
	r.Body = io.NopCloser(bytes.NewBuffer(bodyBytes))

	var req MLEvaluationRequest
	if err := decodeJSON(r, &req); err != nil {
		log.Warn().
			Err(err).
			Str("body", string(bodyBytes)).
			Msg("Failed to decode MLEvaluationRequest")
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	log.Info().
		Str("filterByCategory", getStringPtr(req.FilterByCategory)).
		Bool("forceReEvaluation", req.ForceReEvaluation).
		Msg("Received queue ML evaluation request")

	// Mock response logic
	response := MLEvaluationResponse{
		Success:                        true,
		TotalFilesQueued:               0, // Mocked 0 for now
		Message:                        "ML evaluation queued (Mocked)",
		QueuedAt:                       "2025-12-19T20:25:00Z", // Mocked timestamp
		EstimatedProcessingTimeMinutes: 1,
	}

	respondJSON(w, http.StatusOK, response)
}

func getStringPtr(s *string) string {
	if s == nil {
		return ""
	}
	return *s
}

// CategorizeFile handles POST /api/processing/ml/categorize
// Categorizes a filename using the ML service
func (h *ProcessingHandler) CategorizeFile(w http.ResponseWriter, r *http.Request) {
	var req struct {
		Filename string `json:"filename"`
	}
	if err := decodeJSON(r, &req); err != nil {
		log.Warn().Err(err).Msg("Failed to decode categorize request")
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	if req.Filename == "" {
		respondError(w, http.StatusBadRequest, "filename is required")
		return
	}

	result := h.mlClient.Classify(r.Context(), req.Filename)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("filename", req.Filename).Msg("Failed to categorize file")
		respondError(w, http.StatusInternalServerError, "Failed to categorize file")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}
