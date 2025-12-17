package handlers

import (
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"strconv"
	"strings"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/internal/service"
	"github.com/lucapaganotti/mediabutler-go/pkg/pagination"
	"github.com/rs/zerolog/log"
)

// FilesHandler handles file-related endpoints
type FilesHandler struct {
	fileService    service.FileService
	scannerService service.ScannerService
	watchFolders   []string
}

// NewFilesHandler creates a new FilesHandler
func NewFilesHandler(fileService service.FileService, scannerService service.ScannerService, watchFolders []string) *FilesHandler {
	return &FilesHandler{
		fileService:    fileService,
		scannerService: scannerService,
		watchFolders:   watchFolders,
	}
}

// ErrorResponse represents an error response
type ErrorResponse struct {
	Error     string `json:"error"`
	RequestID string `json:"requestId,omitempty"`
}

// TrackedFileResponse mimics api.TrackedFileResponse in .NET
type TrackedFileResponse struct {
	*domain.TrackedFile
	StatusDescription string    `json:"statusDescription"`
	CreatedAt         time.Time `json:"createdAt"`
}

func toFileResponse(f *domain.TrackedFile) *TrackedFileResponse {
	return &TrackedFileResponse{
		TrackedFile:       f,
		StatusDescription: f.Status.String(),
		CreatedAt:         f.CreatedDate,
	}
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

	mappedItems := make([]*TrackedFileResponse, len(result.Value().Items))
	for i := range result.Value().Items {
		mappedItems[i] = toFileResponse(&result.Value().Items[i])
	}

	response := pagination.NewResponse(mappedItems, result.Value().Total, result.Value().Skip, result.Value().Take)
	respondJSON(w, http.StatusOK, response)
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
	// The frontend sends statuses=Status1&statuses=Status2...
	// We need to parse all values from the query
	statusStrings := r.URL.Query()["statuses"]
	// If standard binding was used (status=A,B), split it. But here we expect multiple keys or comma handling.
	// If only one entry found, try splitting by comma just in case
	var finalStatusStrings []string
	if len(statusStrings) == 1 && strings.Contains(statusStrings[0], ",") {
		finalStatusStrings = strings.Split(statusStrings[0], ",")
	} else {
		finalStatusStrings = statusStrings
	}

	if len(finalStatusStrings) == 0 {
		respondError(w, http.StatusBadRequest, "At least one status is required")
		return
	}

	var statuses []domain.FileStatus
	for _, s := range finalStatusStrings {
		// Clean up string
		s = strings.TrimSpace(s)
		if s == "" {
			continue
		}
		status, err := domain.ParseFileStatus(s)
		if err != nil {
			log.Warn().Err(err).Str("status", s).Msg("Skipping invalid status parameter")
			continue
		}
		statuses = append(statuses, status)
	}

	if len(statuses) == 0 {
		respondError(w, http.StatusBadRequest, "No valid statuses provided")
		return
	}

	// Create pagination request
	pageReq := &pagination.Request{
		Skip: skip,
		Take: take,
	}

	// Get files
	result := h.fileService.GetFilesByStatuses(r.Context(), statuses, pageReq)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to get files by statuses")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve files")
		return
	}

	mappedItems := make([]*TrackedFileResponse, len(result.Value().Items))
	for i := range result.Value().Items {
		mappedItems[i] = toFileResponse(&result.Value().Items[i])
	}

	response := pagination.NewResponse(mappedItems, result.Value().Total, result.Value().Skip, result.Value().Take)
	respondJSON(w, http.StatusOK, response)
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

	respondJSON(w, http.StatusOK, toFileResponse(result.Value()))
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

	respondJSON(w, http.StatusCreated, toFileResponse(fileResult))
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

	mappedFiles := make([]*TrackedFileResponse, len(result.Value()))
	for i, f := range result.Value() {
		mappedFiles[i] = toFileResponse(&f)
	}

	respondJSON(w, http.StatusOK, mappedFiles)
}

// GetReadyForClassification handles GET /api/files/ready-for-classification
// Returns files ready for ML classification
func (h *FilesHandler) GetReadyForClassification(w http.ResponseWriter, r *http.Request) {
	limit := parseInt(r.URL.Query().Get("limit"), 50)

	result := h.fileService.GetReadyForClassification(r.Context(), limit)
	if result.IsFailure() {
		log.Error().Err(result.Error()).Msg("Failed to get files ready for classification")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve files")
		return
	}

	mappedFiles := make([]*TrackedFileResponse, len(result.Value()))
	for i, f := range result.Value() {
		mappedFiles[i] = toFileResponse(&f)
	}

	respondJSON(w, http.StatusOK, mappedFiles)
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

	// Fetch updated file
	fileResult := h.fileService.GetFileByHash(r.Context(), hash)
	if fileResult.IsFailure() {
		log.Error().Err(fileResult.Error()).Str("hash", hash).Msg("Failed to retrieve file after confirmation")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve updated file")
		return
	}

	respondJSON(w, http.StatusOK, toFileResponse(fileResult.Value()))
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

	// Fetch updated file
	fileResult := h.fileService.GetFileByHash(r.Context(), hash)
	if fileResult.IsFailure() {
		log.Error().Err(fileResult.Error()).Str("hash", hash).Msg("Failed to retrieve file after move")
		respondError(w, http.StatusInternalServerError, "Failed to retrieve updated file")
		return
	}

	respondJSON(w, http.StatusOK, toFileResponse(fileResult.Value()))
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

// ScanFolders handles POST /api/files/scan
// Triggers a scan of configured watch folders and returns detailed results
func (h *FilesHandler) ScanFolders(w http.ResponseWriter, r *http.Request) {
	// Optional request body parsing (for future timeout support)
	var req ScanFoldersRequest
	if r.Body != nil && r.ContentLength > 0 {
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			// Ignore parse errors for optional body
			log.Debug().Err(err).Msg("Failed to parse scan request body")
		}
	}

	// Trigger scan
	result := h.scannerService.ScanNow(r.Context())
	if result.IsFailure() {
		log.Warn().Err(result.Error()).Msg("Scan trigger failed")

		// Check if scan already in progress (409 Conflict)
		if strings.Contains(result.Error().Error(), "already in progress") {
			respondError(w, http.StatusConflict, result.Error().Error())
			return
		}

		respondError(w, http.StatusInternalServerError,
			fmt.Sprintf("Folder scan failed: %s", result.Error().Error()))
		return
	}

	// Convert to response model
	scanResult := result.Value()
	response := toScanResultResponse(
		scanResult,
		h.scannerService.IsMonitoring(),
		h.watchFolders,
		nil, // scannedPath is nil for full scan
	)

	respondJSON(w, http.StatusOK, response)
}

// ScanSpecificFolder handles POST /api/files/scan/folder
// Triggers a scan of a specific folder path and returns detailed results
func (h *FilesHandler) ScanSpecificFolder(w http.ResponseWriter, r *http.Request) {
	var req ScanSpecificFolderRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "Invalid request body")
		return
	}

	// Validate folder path
	if strings.TrimSpace(req.FolderPath) == "" {
		respondError(w, http.StatusBadRequest, "Folder path cannot be empty")
		return
	}

	// Check if folder exists
	if _, err := os.Stat(req.FolderPath); os.IsNotExist(err) {
		respondError(w, http.StatusNotFound,
			fmt.Sprintf("Folder not found: %s", req.FolderPath))
		return
	}

	// Trigger scan
	result := h.scannerService.ScanFolder(r.Context(), req.FolderPath)
	if result.IsFailure() {
		log.Warn().Err(result.Error()).Str("path", req.FolderPath).
			Msg("Specific folder scan failed")

		if strings.Contains(result.Error().Error(), "already in progress") {
			respondError(w, http.StatusConflict, result.Error().Error())
			return
		}

		respondError(w, http.StatusInternalServerError,
			fmt.Sprintf("Folder scan failed: %s", result.Error().Error()))
		return
	}

	// Convert to response model
	scanResult := result.Value()
	scannedPath := req.FolderPath
	response := toScanResultResponse(
		scanResult,
		h.scannerService.IsMonitoring(),
		[]string{req.FolderPath}, // Only this path was scanned
		&scannedPath,
	)

	respondJSON(w, http.StatusOK, response)
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
