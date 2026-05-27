package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"time"
)

type Server struct {
	config  Config
	db      *sql.DB
	sse     *SSEBroker
	watcher *Watcher
}

func NewServer(config Config, db *sql.DB, sse *SSEBroker, watcher *Watcher) *Server {
	return &Server{
		config:  config,
		db:      db,
		sse:     sse,
		watcher: watcher,
	}
}

// RegisterRoutes registers all API routes to the provided ServeMux.
func (s *Server) RegisterRoutes(mux *http.ServeMux) {
	mux.HandleFunc("/api/files", s.handleFiles)
	mux.HandleFunc("/api/files/", s.handleFileByHashOrAction)
	mux.HandleFunc("/api/files/pending", s.handlePendingFiles)
	mux.HandleFunc("/api/config", s.handleConfig)
	mux.HandleFunc("/api/rescan", s.handleRescan)
	mux.HandleFunc("/api/retrain", s.handleRetrain)
	mux.HandleFunc("/api/categories", s.handleCategories)
	mux.HandleFunc("/api/fs/list", s.handleFSList)
	mux.Handle("/api/events", s.sse)
}

// JSON helpers
func writeJSON(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(data); err != nil {
		slog.Error("Failed to write JSON response", "err", err)
	}
}

func writeError(w http.ResponseWriter, status int, message string) {
	writeJSON(w, status, map[string]string{"error": message})
}

func (s *Server) handleFilesMethod(w http.ResponseWriter, r *http.Request) {
	switch r.Method {
	case http.MethodGet:
		s.getFiles(w, r)
	case http.MethodPost:
		s.registerFile(w, r)
	default:
		w.Header().Set("Allow", "GET, POST")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
	}
}

// Wrapper to match signature in http.HandleFunc
func (s *Server) handleFiles(w http.ResponseWriter, r *http.Request) {
	s.handleFilesMethod(w, r)
}

// Route dispatcher for /api/files/{hash} and sub-actions
func (s *Server) handleFileByHashOrAction(w http.ResponseWriter, r *http.Request) {
	// Path is expected to be "/api/files/{hash}" or "/api/files/{hash}/confirm" or "/api/files/{hash}/moved"
	parts := strings.Split(strings.Trim(r.URL.Path, "/"), "/")
	if len(parts) < 3 {
		writeError(w, http.StatusBadRequest, "Invalid request path")
		return
	}

	hash := parts[2]
	if len(hash) != 64 { // SHA256 hash length is 64 chars
		writeError(w, http.StatusBadRequest, "Invalid SHA256 hash format")
		return
	}

	if len(parts) == 3 {
		switch r.Method {
		case http.MethodGet:
			s.getFileByHash(w, r, hash)
		default:
			w.Header().Set("Allow", "GET")
			writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
		}
		return
	}

	action := parts[3]
	if len(parts) == 4 {
		switch action {
		case "confirm":
			if r.Method != http.MethodPost {
				w.Header().Set("Allow", "POST")
				writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
				return
			}
			s.confirmFileCategory(w, r, hash)
		case "classify":
			if r.Method != http.MethodPost {
				w.Header().Set("Allow", "POST")
				writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
				return
			}
			s.classifyFile(w, r, hash)
		case "move":
			if r.Method != http.MethodPost {
				w.Header().Set("Allow", "POST")
				writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
				return
			}
			s.moveFile(w, r, hash)
		case "moved":
			if r.Method != http.MethodPost {
				w.Header().Set("Allow", "POST")
				writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
				return
			}
			s.markFileAsMoved(w, r, hash)
		default:
			writeError(w, http.StatusNotFound, "Action not found")
		}
		return
	}

	writeError(w, http.StatusNotFound, "Endpoint not found")
}

// GET /api/files
func (s *Server) getFiles(w http.ResponseWriter, r *http.Request) {
	// Parse pagination query parameters
	skipStr := r.URL.Query().Get("skip")
	takeStr := r.URL.Query().Get("take")
	statusStr := r.URL.Query().Get("status")
	categoryStr := r.URL.Query().Get("category")

	skip := 0
	take := 20

	if skipStr != "" {
		if val, err := strconv.Atoi(skipStr); err == nil && val >= 0 {
			skip = val
		}
	}
	if takeStr != "" {
		if val, err := strconv.Atoi(takeStr); err == nil && val > 0 {
			take = val
			if take > 100 {
				take = 100
			}
		}
	}

	query := `
		SELECT Hash, FileName, OriginalPath, FileSize, Status, SuggestedCategory, Confidence, Category, TargetPath, ClassifiedAt, MovedAt, LastError, LastErrorAt, RetryCount, CreatedDate, LastUpdateDate, Note, IsActive, MovedToPath 
		FROM TrackedFiles 
		WHERE IsActive = 1
	`
	var args []interface{}

	if statusStr != "" {
		statusVal, err := strconv.Atoi(statusStr)
		if err == nil {
			query += " AND Status = ?"
			args = append(args, statusVal)
		}
	}
	if categoryStr != "" {
		query += " AND Category = ?"
		args = append(args, categoryStr)
	}

	query += " ORDER BY CreatedDate DESC LIMIT ? OFFSET ?"
	args = append(args, take, skip)

	rows, err := s.db.Query(query, args...)
	if err != nil {
		slog.Error("Failed to query files", "err", err)
		writeError(w, http.StatusInternalServerError, "Failed to retrieve files")
		return
	}
	defer rows.Close()

	files := []TrackedFile{}
	for rows.Next() {
		var f TrackedFile
		err := rows.Scan(
			&f.Hash, &f.FileName, &f.OriginalPath, &f.FileSize, &f.Status,
			&f.SuggestedCategory, &f.Confidence, &f.Category, &f.TargetPath,
			&f.ClassifiedAt, &f.MovedAt, &f.LastError, &f.LastErrorAt,
			&f.RetryCount, &f.CreatedDate, &f.LastUpdateDate, &f.Note,
			&f.IsActive, &f.MovedToPath,
		)
		if err != nil {
			slog.Error("Failed to scan row", "err", err)
			writeError(w, http.StatusInternalServerError, "Failed to parse files")
			return
		}
		files = append(files, f)
	}

	writeJSON(w, http.StatusOK, files)
}

// GET /api/files/{hash}
func (s *Server) getFileByHash(w http.ResponseWriter, r *http.Request, hash string) {
	var f TrackedFile
	query := `
		SELECT Hash, FileName, OriginalPath, FileSize, Status, SuggestedCategory, Confidence, Category, TargetPath, ClassifiedAt, MovedAt, LastError, LastErrorAt, RetryCount, CreatedDate, LastUpdateDate, Note, IsActive, MovedToPath 
		FROM TrackedFiles 
		WHERE Hash = ? AND IsActive = 1
	`
	err := s.db.QueryRow(query, hash).Scan(
		&f.Hash, &f.FileName, &f.OriginalPath, &f.FileSize, &f.Status,
		&f.SuggestedCategory, &f.Confidence, &f.Category, &f.TargetPath,
		&f.ClassifiedAt, &f.MovedAt, &f.LastError, &f.LastErrorAt,
		&f.RetryCount, &f.CreatedDate, &f.LastUpdateDate, &f.Note,
		&f.IsActive, &f.MovedToPath,
	)

	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			writeError(w, http.StatusNotFound, "File not found")
		} else {
			slog.Error("Failed to query file by hash", "hash", hash, "err", err)
			writeError(w, http.StatusInternalServerError, "Internal server error")
		}
		return
	}

	writeJSON(w, http.StatusOK, f)
}

// POST /api/files
func (s *Server) registerFile(w http.ResponseWriter, r *http.Request) {
	var req AddFileRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.FilePath == "" {
		writeError(w, http.StatusBadRequest, "Invalid request. FilePath is required.")
		return
	}

	fileName := filepath.Base(req.FilePath)
	
	// Create simulated hash for simple endpoint registration if not provided.
	// 64-character SHA256 simulation
	hash := fmt.Sprintf("%x", time.Now().UnixNano())
	if len(hash) < 64 {
		hash = hash + strings.Repeat("0", 64-len(hash))
	}

	now := time.Now()

	// Predict category!
	suggestedCat, confidence, err := PredictCategory(s.db, fileName)
	status := FileStatusNew
	var suggestedCatPtr *string
	var classifiedAtPtr *time.Time
	if err == nil && suggestedCat != "UNKNOWN" && suggestedCat != "" {
		status = FileStatusClassified
		suggestedCatPtr = &suggestedCat
		classifiedAtPtr = &now
	}

	_, err = s.db.Exec(`
		INSERT INTO TrackedFiles (Hash, OriginalPath, FileName, FileSize, Status, SuggestedCategory, Confidence, ClassifiedAt, CreatedDate, LastUpdateDate, IsActive)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)
	`, hash, req.FilePath, fileName, 0, status, suggestedCatPtr, confidence, classifiedAtPtr, now, now)

	if err != nil {
		slog.Error("Failed to register file", "path", req.FilePath, "err", err)
		writeError(w, http.StatusInternalServerError, "Failed to register file in database")
		return
	}

	slog.Info("Successfully registered new file", "path", req.FilePath, "hash", hash, "suggestedCategory", suggestedCat)

	// Fetch registered file to return it
	s.getFileByHash(w, r, hash)
}

// GET /api/files/pending
func (s *Server) handlePendingFiles(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		w.Header().Set("Allow", "GET")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
		return
	}

	query := `
		SELECT Hash, FileName, OriginalPath, FileSize, Status, SuggestedCategory, Confidence, Category, TargetPath, ClassifiedAt, MovedAt, LastError, LastErrorAt, RetryCount, CreatedDate, LastUpdateDate, Note, IsActive, MovedToPath 
		FROM TrackedFiles 
		WHERE Status = ? AND IsActive = 1
	`
	rows, err := s.db.Query(query, FileStatusClassified)
	if err != nil {
		slog.Error("Failed to query pending files", "err", err)
		writeError(w, http.StatusInternalServerError, "Failed to retrieve pending files")
		return
	}
	defer rows.Close()

	files := []TrackedFile{}
	for rows.Next() {
		var f TrackedFile
		err := rows.Scan(
			&f.Hash, &f.FileName, &f.OriginalPath, &f.FileSize, &f.Status,
			&f.SuggestedCategory, &f.Confidence, &f.Category, &f.TargetPath,
			&f.ClassifiedAt, &f.MovedAt, &f.LastError, &f.LastErrorAt,
			&f.RetryCount, &f.CreatedDate, &f.LastUpdateDate, &f.Note,
			&f.IsActive, &f.MovedToPath,
		)
		if err != nil {
			slog.Error("Failed to scan row", "err", err)
			writeError(w, http.StatusInternalServerError, "Failed to parse files")
			return
		}
		files = append(files, f)
	}

	writeJSON(w, http.StatusOK, files)
}

// POST /api/files/{hash}/confirm
// Confirms category manually, setting status to Confirmed (ReadyToMove) and triggers incremental ML training
func (s *Server) confirmFileCategory(w http.ResponseWriter, r *http.Request, hash string) {
	var req ConfirmCategoryRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.Category == "" {
		writeError(w, http.StatusBadRequest, "Invalid request. Category is required.")
		return
	}

	categoryUpper := strings.ToUpper(strings.TrimSpace(req.Category))

	// 1. Fetch the file to get the exact FileName for incremental training
	var fileName string
	err := s.db.QueryRow("SELECT FileName FROM TrackedFiles WHERE Hash = ? AND IsActive = 1", hash).Scan(&fileName)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			writeError(w, http.StatusNotFound, "File not found")
		} else {
			slog.Error("Failed to check file existence", "hash", hash, "err", err)
			writeError(w, http.StatusInternalServerError, "Internal database error")
		}
		return
	}

	// 2. Perform incremental learning on feedback (UPSERT background)
	LearnClassification(s.db, CleanFilename(fileName), categoryUpper)

	// 3. Update Status to ReadyToMove (Confirmed) in database
	now := time.Now()
	_, err = s.db.Exec(`
		UPDATE TrackedFiles 
		SET Status = ?, Category = ?, LastUpdateDate = ? 
		WHERE Hash = ? AND IsActive = 1
	`, FileStatusReadyToMove, categoryUpper, now, hash)

	if err != nil {
		slog.Error("Failed to confirm file category", "hash", hash, "err", err)
		writeError(w, http.StatusInternalServerError, "Failed to update category in database")
		return
	}

	slog.Info("Confirmed category manually for file", "hash", hash, "category", categoryUpper)

	// 4. Broadcast file.confirmed SSE event
	if s.sse != nil {
		payloadBytes, _ := json.Marshal(map[string]interface{}{
			"hash":     hash,
			"category": categoryUpper,
			"status":   int(FileStatusReadyToMove),
		})
		s.sse.Broadcast("file.confirmed", string(payloadBytes))
	}

	// 5. Respond with 200 OK
	writeJSON(w, http.StatusOK, map[string]string{
		"hash":    hash,
		"status":  "Confirmed",
		"message": "File category confirmed manually",
	})
}

// POST /api/files/{hash}/classify
// Triggers classification of a file (e.g. for New files without classification)
func (s *Server) classifyFile(w http.ResponseWriter, r *http.Request, hash string) {
	// 1. Fetch the file to get the exact FileName for classification
	var fileName string
	var currentStatus int
	err := s.db.QueryRow("SELECT FileName, Status FROM TrackedFiles WHERE Hash = ? AND IsActive = 1", hash).Scan(&fileName, &currentStatus)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			writeError(w, http.StatusNotFound, "File not found")
		} else {
			slog.Error("Failed to check file existence", "hash", hash, "err", err)
			writeError(w, http.StatusInternalServerError, "Internal database error")
		}
		return
	}

	// 2. Perform classification
	suggestedCat, confidence, err := PredictCategory(s.db, fileName)
	if err != nil {
		slog.Error("Failed to predict category", "fileName", fileName, "err", err)
		writeError(w, http.StatusInternalServerError, "Classifier error")
		return
	}

	now := time.Now()
	status := currentStatus
	if suggestedCat != "UNKNOWN" && suggestedCat != "" {
		status = int(FileStatusClassified)
	}

	// 3. Update database
	var suggestedCatPtr *string
	if suggestedCat != "" && suggestedCat != "UNKNOWN" {
		suggestedCatPtr = &suggestedCat
	}

	_, err = s.db.Exec(`
		UPDATE TrackedFiles 
		SET Status = ?, SuggestedCategory = ?, Confidence = ?, ClassifiedAt = ?, LastUpdateDate = ? 
		WHERE Hash = ? AND IsActive = 1
	`, status, suggestedCatPtr, confidence, now, now, hash)

	if err != nil {
		slog.Error("Failed to update classified file", "hash", hash, "err", err)
		writeError(w, http.StatusInternalServerError, "Failed to update file in database")
		return
	}

	slog.Info("Successfully classified file", "hash", hash, "category", suggestedCat, "confidence", confidence)

	// 4. Broadcast file.discovered SSE event to reload the frontend view reattivamente!
	if s.sse != nil {
		payloadBytes, _ := json.Marshal(map[string]interface{}{
			"hash":              hash,
			"fileName":          fileName,
			"status":            status,
			"suggestedCategory": suggestedCat,
			"confidence":        confidence,
		})
		s.sse.Broadcast("file.discovered", string(payloadBytes))
	}

	// 5. Respond with 200 OK and updated info
	writeJSON(w, http.StatusOK, map[string]interface{}{
		"hash":              hash,
		"status":            status,
		"suggestedCategory": suggestedCat,
		"confidence":        confidence,
	})
}


// POST /api/files/{hash}/moved
func (s *Server) markFileAsMoved(w http.ResponseWriter, r *http.Request, hash string) {
	var req MarkMovedRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.TargetPath == "" {
		writeError(w, http.StatusBadRequest, "Invalid request. TargetPath is required.")
		return
	}

	now := time.Now()
	res, err := s.db.Exec(`
		UPDATE TrackedFiles 
		SET Status = ?, MovedToPath = ?, TargetPath = ?, MovedAt = ?, LastUpdateDate = ? 
		WHERE Hash = ? AND IsActive = 1
	`, FileStatusMoved, req.TargetPath, req.TargetPath, now, now, hash)

	if err != nil {
		slog.Error("Failed to mark file as moved", "hash", hash, "err", err)
		writeError(w, http.StatusInternalServerError, "Failed to update status to moved")
		return
	}

	rowsAffected, _ := res.RowsAffected()
	if rowsAffected == 0 {
		writeError(w, http.StatusNotFound, "File not found")
		return
	}

	slog.Info("Marked file as moved manually", "hash", hash, "targetPath", req.TargetPath)
	s.getFileByHash(w, r, hash)
}

// GET or POST /api/config
func (s *Server) handleConfig(w http.ResponseWriter, r *http.Request) {
	switch r.Method {
	case http.MethodGet:
		writeJSON(w, http.StatusOK, map[string]interface{}{
			"databasePath": s.config.DatabasePath,
			"watchFolders": s.config.WatchFolders,
			"destFolder":   s.config.DestFolder,
			"mlThreshold":  s.config.MLThreshold,
			"logLevel":     programLevel.Level().String(),
		})
	case http.MethodPost:
		var req struct {
			LogLevel string `json:"logLevel"`
		}
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeError(w, http.StatusBadRequest, "Invalid request body")
			return
		}

		levelStr := strings.ToUpper(strings.TrimSpace(req.LogLevel))
		var newLevel slog.Level
		switch levelStr {
		case "DEBUG":
			newLevel = slog.LevelDebug
		case "INFO":
			newLevel = slog.LevelInfo
		case "WARN":
			newLevel = slog.LevelWarn
		case "ERROR":
			newLevel = slog.LevelError
		default:
			writeError(w, http.StatusBadRequest, fmt.Sprintf("Invalid log level '%s'. Supported: DEBUG, INFO, WARN, ERROR", req.LogLevel))
			return
		}

		programLevel.Set(newLevel)
		slog.Info("Log level dynamically updated", "newLevel", levelStr)

		writeJSON(w, http.StatusOK, map[string]interface{}{
			"databasePath": s.config.DatabasePath,
			"watchFolders": s.config.WatchFolders,
			"destFolder":   s.config.DestFolder,
			"mlThreshold":  s.config.MLThreshold,
			"logLevel":     programLevel.Level().String(),
		})
	default:
		w.Header().Set("Allow", "GET, POST")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
	}
}

// POST /api/rescan
func (s *Server) handleRescan(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.Header().Set("Allow", "POST")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
		return
	}

	if s.watcher == nil {
		writeError(w, http.StatusInternalServerError, "Watcher not initialized")
		return
	}

	// Trigger manual scan in background goroutine to prevent blocking REST request
	go s.watcher.ManualScan(context.Background())

	writeJSON(w, http.StatusAccepted, map[string]string{
		"message": "Manual watch folder rescan triggered successfully in background",
	})
}

// POST /api/retrain
func (s *Server) handleRetrain(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.Header().Set("Allow", "POST")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
		return
	}

	// Retrain Naive Bayes model based on existing history files in the database
	err := RetrainModel(s.db)
	if err != nil {
		slog.Error("Failed to retrain statistical classifier", "err", err)
		writeError(w, http.StatusInternalServerError, fmt.Sprintf("Failed to retrain classifier: %v", err))
		return
	}

	writeJSON(w, http.StatusOK, map[string]string{
		"message": "Naive Bayes classifier model retrained successfully based on database history",
	})
}

// GET /api/categories
// Returns a distinct list of confirmed categories present in database, ordered alphabetically
func (s *Server) handleCategories(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		w.Header().Set("Allow", "GET")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
		return
	}

	rows, err := s.db.Query("SELECT DISTINCT Category FROM TrackedFiles WHERE Category IS NOT NULL AND Category != '' ORDER BY Category ASC")
	if err != nil {
		slog.Error("Failed to fetch distinct categories from DB", "err", err)
		writeError(w, http.StatusInternalServerError, "Database error")
		return
	}
	defer rows.Close()

	categories := []string{}
	for rows.Next() {
		var cat string
		if err := rows.Scan(&cat); err == nil {
			categories = append(categories, cat)
		}
	}

	writeJSON(w, http.StatusOK, categories)
}

// POST /api/files/{hash}/move
// Triggers the actual async file copy/move organization to destination paths
func (s *Server) moveFile(w http.ResponseWriter, r *http.Request, hash string) {
	if r.Method != http.MethodPost {
		w.Header().Set("Allow", "POST")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
		return
	}

	// 1. Fetch file from database and verify it is in ReadyToMove (3) or Error (6) state
	var category string
	var status FileStatus
	err := s.db.QueryRow("SELECT Status, Category FROM TrackedFiles WHERE Hash = ? AND IsActive = 1", hash).Scan(&status, &category)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			writeError(w, http.StatusNotFound, "File not found")
		} else {
			slog.Error("Failed to fetch file for move", "hash", hash, "err", err)
			writeError(w, http.StatusInternalServerError, "Database error")
		}
		return
	}

	if status != FileStatusReadyToMove && status != FileStatusError {
		writeError(w, http.StatusBadRequest, "File must be Confirmed (ReadyToMove) or in Error state to initiate movement.")
		return
	}

	if category == "" {
		writeError(w, http.StatusBadRequest, "File category must be confirmed before initiating movement.")
		return
	}

	slog.Info("Initiating async file move", "hash", hash, "category", category)

	// 2. Launch asynchronous move and stream progress via SSE
	MoveFileAsync(s.db, s.sse, hash, category, s.config.DestFolder)

	// 3. Respond with 202 Accepted
	writeJSON(w, http.StatusAccepted, map[string]string{
		"hash":    hash,
		"status":  "Moving",
		"message": "File organization initiated asynchronously",
	})
}

// GET /api/fs/list
func (s *Server) handleFSList(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		w.Header().Set("Allow", "GET")
		writeError(w, http.StatusMethodNotAllowed, "Method not allowed")
		return
	}

	pathParam := r.URL.Query().Get("path")
	if pathParam == "" {
		writeError(w, http.StatusBadRequest, "Missing 'path' query parameter")
		return
	}

	showHidden := false
	if r.URL.Query().Get("showHidden") == "true" {
		showHidden = true
	}

	cleanPath := filepath.Clean(pathParam)
	absReqPath, err := filepath.Abs(cleanPath)
	if err != nil {
		writeError(w, http.StatusBadRequest, "Invalid path")
		return
	}

	// Security check: must reside within (or be identical to) watch folders or dest folder
	allowed := false
	for _, folder := range s.config.WatchFolders {
		absFolder, err := filepath.Abs(filepath.Clean(folder))
		if err != nil {
			continue
		}
		if absReqPath == absFolder || strings.HasPrefix(absReqPath, absFolder+string(filepath.Separator)) {
			allowed = true
			break
		}
	}

	if !allowed {
		absDest, err := filepath.Abs(filepath.Clean(s.config.DestFolder))
		if err == nil {
			if absReqPath == absDest || strings.HasPrefix(absReqPath, absDest+string(filepath.Separator)) {
				allowed = true
			}
		}
	}

	if !allowed {
		writeError(w, http.StatusForbidden, "Forbidden: Access denied to this path")
		return
	}

	entries, err := os.ReadDir(absReqPath)
	if err != nil {
		if os.IsNotExist(err) {
			writeError(w, http.StatusNotFound, "Directory not found")
		} else {
			slog.Error("Failed to read directory", "path", absReqPath, "err", err)
			writeError(w, http.StatusInternalServerError, "Failed to read directory")
		}
		return
	}

	items := []FSItem{}
	for _, entry := range entries {
		name := entry.Name()

		// If showHidden is false, filter out hidden files starting with . or @
		if !showHidden {
			if strings.HasPrefix(name, ".") || strings.HasPrefix(name, "@") {
				continue
			}
		}

		var sizeBytes int64 = 0
		if !entry.IsDir() {
			info, err := entry.Info()
			if err == nil {
				sizeBytes = info.Size()
			}
		}

		items = append(items, FSItem{
			Name:      name,
			Path:      filepath.Join(absReqPath, name),
			IsDir:     entry.IsDir(),
			SizeBytes: sizeBytes,
		})
	}

	// Sort: directories first (alphabetical case-insensitive), then files (alphabetical case-insensitive)
	sort.Slice(items, func(i, j int) bool {
		if items[i].IsDir && !items[j].IsDir {
			return true
		}
		if !items[i].IsDir && items[j].IsDir {
			return false
		}
		return strings.ToLower(items[i].Name) < strings.ToLower(items[j].Name)
	})

	writeJSON(w, http.StatusOK, items)
}
