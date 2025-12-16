package handlers

import (
	"encoding/json"
	"net/http"
	"strconv"
	"strings"

	"github.com/go-chi/chi/v5"
	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/internal/service"
	"github.com/lucapaganotti/mediabutler-go/pkg/pagination"
	"github.com/rs/zerolog/log"
)

// FilesHandler handles file-related endpoints
type FilesHandler struct {
	fileService service.FileService
}

// NewFilesHandler creates a new FilesHandler
func NewFilesHandler(fileService service.FileService) *FilesHandler {
	return &FilesHandler{
		fileService: fileService,
	}
}

// ErrorResponse represents an error response
type ErrorResponse struct {
	Error     string `json:"error"`
	RequestID string `json:"requestId,omitempty"`
}

// GetFiles handles GET /api/files
// Returns paginated list of tracked files
func (h *FilesHandler) GetFiles(w http.ResponseWriter, r *http.Request) {
	// Parse query parameters
	statusStr := r.URL.Query().Get("status")
	skip := parseInt(r.URL.Query().Get("skip"), 0)
	take := parseInt(r.URL.Query().Get("take"), 20)

	// Parse status
	var status domain.FileStatus
	if statusStr != "" {
		var err error
		status, err = domain.ParseFileStatus(statusStr)
		if err != nil {
			respondError(w, http.StatusBadRequest, "Invalid status parameter")
			return
		}
	} else {
		status = domain.FileStatusNew // Default
	}

	// Create pagination request
	pageReq := &pagination.Request{
		Skip: skip,
		Take: take,
	}

	// Get files
	result := h.fileService.GetFilesByStatus(r.Context(), status, pageReq)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to get files")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve files")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}

// GetFilesByStatuses handles GET /api/files/by-statuses
// Returns files filtered by multiple statuses
func (h *FilesHandler) GetFilesByStatuses(w http.ResponseWriter, r *http.Request) {
	// Parse query parameters
	statusesParam := r.URL.Query().Get("statuses")
	if statusesParam == "" {
		respondError(w, http.StatusBadRequest, "statuses parameter is required")
		return
	}

	skip := parseInt(r.URL.Query().Get("skip"), 0)
	take := parseInt(r.URL.Query().Get("take"), 20)

	// Parse multiple statuses
	statusStrings := strings.Split(statusesParam, ",")
	if len(statusStrings) == 0 {
		respondError(w, http.StatusBadRequest, "At least one status is required")
		return
	}

	// For now, we'll use the first status (full multi-status support requires repository enhancement)
	// TODO: Enhance repository to support multiple status filtering
	status, err := domain.ParseFileStatus(strings.TrimSpace(statusStrings[0]))
	if err != nil {
		respondError(w, http.StatusBadRequest, "Invalid status parameter")
		return
	}

	// Create pagination request
	pageReq := &pagination.Request{
		Skip: skip,
		Take: take,
	}

	// Get files
	result := h.fileService.GetFilesByStatus(r.Context(), status, pageReq)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to get files by statuses")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve files")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}

// GetFileByHash handles GET /api/files/{hash}
// Returns a single file by hash
func (h *FilesHandler) GetFileByHash(w http.ResponseWriter, r *http.Request) {
	hash := chi.URLParam(r, "hash")
	if hash == "" {
		respondError(w, http.StatusBadRequest, "hash parameter is required")
		return
	}

	result := h.fileService.GetFileByHash(r.Context(), hash)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("hash", hash).Msg("Failed to get file")
		respondError(w, http.StatusNotFound, "File not found")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}

// RegisterFile handles POST /api/files
// Registers a new file for tracking
func (h *FilesHandler) RegisterFile(w http.ResponseWriter, r *http.Request) {
	var req struct {
		FilePath string `json:"filePath"`
		Hash     string `json:"hash,omitempty"`
		FileSize int64  `json:"fileSize,omitempty"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	if req.FilePath == "" {
		respondError(w, http.StatusBadRequest, "filePath is required")
		return
	}

	var fileResult *domain.TrackedFile
	var resultErr error

	if req.Hash != "" && req.FileSize > 0 {
		// Use pre-calculated hash
		res := h.fileService.RegisterFileWithHash(r.Context(), req.FilePath, req.Hash, req.FileSize)
		if res.IsFailure() {
			resultErr = res.Error()
		} else {
			fileResult = res.Value()
		}
	} else {
		// Calculate hash from file
		res := h.fileService.RegisterFile(r.Context(), req.FilePath)
		if res.IsFailure() {
			resultErr = res.Error()
		} else {
			fileResult = res.Value()
		}
	}

	if resultErr != nil {
		log.Error().Err(resultErr).Str("path", req.FilePath).Msg("Failed to register file")
		respondError(w, http.StatusInternalServerError, "Failed to register file")
		return
	}

	respondJSON(w, http.StatusCreated, fileResult)
}

// GetPendingFiles handles GET /api/files/pending
// Returns files awaiting user confirmation
func (h *FilesHandler) GetPendingFiles(w http.ResponseWriter, r *http.Request) {
	result := h.fileService.GetPendingFiles(r.Context())
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to get pending files")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve pending files")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}

// ConfirmFile handles POST /api/files/{hash}/confirm
// Confirms a file's category
func (h *FilesHandler) ConfirmFile(w http.ResponseWriter, r *http.Request) {
	hash := chi.URLParam(r, "hash")
	if hash == "" {
		respondError(w, http.StatusBadRequest, "hash parameter is required")
		return
	}

	var req struct {
		Category string `json:"category"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	if req.Category == "" {
		respondError(w, http.StatusBadRequest, "category is required")
		return
	}

	result := h.fileService.ConfirmCategory(r.Context(), hash, req.Category)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("hash", hash).Msg("Failed to confirm file")
		respondError(w, http.StatusInternalServerError, "Failed to confirm file category")
		return
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"success": true,
		"message": "File category confirmed",
	})
}

// MarkFileAsMoved handles POST /api/files/{hash}/moved
// Marks a file as successfully moved
func (h *FilesHandler) MarkFileAsMoved(w http.ResponseWriter, r *http.Request) {
	hash := chi.URLParam(r, "hash")
	if hash == "" {
		respondError(w, http.StatusBadRequest, "hash parameter is required")
		return
	}

	var req struct {
		MovedToPath string `json:"movedToPath"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	if req.MovedToPath == "" {
		respondError(w, http.StatusBadRequest, "movedToPath is required")
		return
	}

	result := h.fileService.MarkAsMoved(r.Context(), hash, req.MovedToPath)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("hash", hash).Msg("Failed to mark file as moved")
		respondError(w, http.StatusInternalServerError, "Failed to mark file as moved")
		return
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"success": true,
		"message": "File marked as moved",
	})
}

// DeleteFile handles DELETE /api/files/{hash}
// Soft deletes a tracked file
func (h *FilesHandler) DeleteFile(w http.ResponseWriter, r *http.Request) {
	hash := chi.URLParam(r, "hash")
	if hash == "" {
		respondError(w, http.StatusBadRequest, "hash parameter is required")
		return
	}

	// Soft delete is handled via MarkAsIgnored
	reason := "User deleted via API"
	result := h.fileService.MarkAsIgnored(r.Context(), hash, &reason)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Str("hash", hash).Msg("Failed to delete file")
		respondError(w, http.StatusInternalServerError, "Failed to delete file")
		return
	}

	w.WriteHeader(http.StatusNoContent)
}

// GetCategories handles GET /api/files/categories
// Returns all distinct categories
func (h *FilesHandler) GetCategories(w http.ResponseWriter, r *http.Request) {
	result := h.fileService.GetAllCategories(r.Context())
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to get categories")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve categories")
		return
	}

	respondJSON(w, http.StatusOK, result.Value())
}

// ScanFolder handles POST /api/files/scan
// Triggers a scan of configured watch folders
func (h *FilesHandler) ScanFolder(w http.ResponseWriter, r *http.Request) {
	// TODO: Implement folder scanning via background job
	// For now, return not implemented
	respondError(w, http.StatusNotImplemented, "Folder scanning not yet implemented")
}

// Helper functions

func parseInt(s string, defaultValue int) int {
	if s == "" {
		return defaultValue
	}
	val, err := strconv.Atoi(s)
	if err != nil {
		return defaultValue
	}
	return val
}

func respondJSON(w http.ResponseWriter, statusCode int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(statusCode)
	json.NewEncoder(w).Encode(data)
}

func respondError(w http.ResponseWriter, statusCode int, message string) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(statusCode)
	json.NewEncoder(w).Encode(ErrorResponse{
		Error: message,
	})
}
