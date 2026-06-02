package main

import (
	"bytes"
	"context"
	"crypto/sha256"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"github.com/fsnotify/fsnotify"
)

type Watcher struct {
	config      Config
	db          *sql.DB
	watcher     *fsnotify.Watcher
	activeFiles map[string]time.Time
	mu          sync.Mutex
	sse         *SSEBroker
}

func NewWatcher(cfg Config, db *sql.DB, sse *SSEBroker) (*Watcher, error) {
	fw, err := fsnotify.NewWatcher()
	if err != nil {
		return nil, err
	}

	return &Watcher{
		config:      cfg,
		db:          db,
		watcher:     fw,
		activeFiles: make(map[string]time.Time),
		sse:         sse,
	}, nil
}

// Start starts the recursive file watcher in a background routine.
func (w *Watcher) Start(ctx context.Context) {
	slog.Info("Starting File System Watcher...")

	// 1. Add watch folders and walk recursively
	for _, folder := range w.config.WatchFolders {
		// Ensure directory exists
		if err := os.MkdirAll(folder, 0755); err != nil {
			slog.Error("Failed to create watch folder", "path", folder, "err", err)
			continue
		}

		slog.Info("Watching folder recursively", "path", folder)
		if err := w.addRecursive(folder); err != nil {
			slog.Error("Failed to add recursive watcher for folder", "path", folder, "err", err)
		}
	}

	// 2. Goroutine processing fsnotify events
	go func() {
		defer w.watcher.Close()

		for {
			select {
			case <-ctx.Done():
				slog.Info("Watcher context cancelled. Stopping file watcher.")
				return
			case event, ok := <-w.watcher.Events:
				if !ok {
					return
				}

				slog.Info("Watcher detected event", "op", event.Op.String(), "path", event.Name)

				// We care about creation, modification, and write events
				if event.Has(fsnotify.Create) || event.Has(fsnotify.Write) {
					// If a new directory is created, add it recursively to watch list
					info, err := os.Stat(event.Name)
					if err == nil && info.IsDir() {
						slog.Info("New directory detected. Adding to watch list.", "path", event.Name)
						w.watcher.Add(event.Name)
						continue
					}

					// Trigger debounce / stability checker
					w.triggerStabilityCheck(ctx, event.Name)
				}
			case err, ok := <-w.watcher.Errors:
				if !ok {
					return
				}
				slog.Error("Watcher event error", "err", err)
			}
		}
	}()
}

// addRecursive recursively walks and registers folders to fsnotify
func (w *Watcher) addRecursive(root string) error {
	return filepath.Walk(root, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}
		if info.IsDir() {
			err = w.watcher.Add(path)
			if err != nil {
				return err
			}
		}
		return nil
	})
}

// triggerStabilityCheck starts tracking file updates if not already doing so
func (w *Watcher) triggerStabilityCheck(ctx context.Context, filePath string) {
	// Skip hidden system files, sqlite database locks, temp logs, etc.
	baseName := filepath.Base(filePath)
	if baseName == "" || baseName[0] == '.' || baseName == "mediabutler.db" || baseName == "mediabutler.db-wal" || baseName == "mediabutler.db-shm" {
		return
	}

	w.mu.Lock()
	if _, exists := w.activeFiles[filePath]; exists {
		w.mu.Unlock()
		return
	}
	w.activeFiles[filePath] = time.Now()
	w.mu.Unlock()

	// Spin a lightweight polling routine to wait for writing to finish
	go w.pollFileStability(ctx, filePath)
}

// pollFileStability monitors a file until its size stabilizes and is sbloccato
func (w *Watcher) pollFileStability(ctx context.Context, filePath string) {
	slog.Debug("Starting stability check for file", "path", filePath)
	ticker := time.NewTicker(5 * time.Second)
	defer ticker.Stop()

	var lastSize int64 = -1

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			info, err := os.Stat(filePath)
			if err != nil {
				if os.IsNotExist(err) {
					slog.Debug("File disappeared during stability polling", "path", filePath)
					w.mu.Lock()
					delete(w.activeFiles, filePath)
					w.mu.Unlock()
					return
				}
				slog.Error("Failed to check file info", "path", filePath, "err", err)
				continue
			}

			currentSize := info.Size()
			if currentSize == lastSize && currentSize > 0 {
				// Size has not changed since last check. Check if we can open it without lock error.
				if isFileAvailable(filePath) {
					slog.Info("File is stable and unlocked. Processing file.", "path", filePath, "size", currentSize)
					
					w.mu.Lock()
					delete(w.activeFiles, filePath)
					w.mu.Unlock()

					if err := w.processStableFile(filePath, currentSize); err != nil {
						slog.Error("Failed to process stable file", "path", filePath, "err", err)
					}
					return
				}
				slog.Debug("File size stable, but file is still in use/locked. Retrying...", "path", filePath)
			} else {
				slog.Debug("File size is changing or zero-byte. Retrying stability check.", "path", filePath, "previousSize", lastSize, "currentSize", currentSize)
				lastSize = currentSize
			}
		}
	}
}

// isFileAvailable verifies if the file can be opened for exclusive reading/writing without lock issue
func isFileAvailable(filePath string) bool {
	file, err := os.OpenFile(filePath, os.O_RDWR, 0)
	if err != nil {
		return false
	}
	file.Close()
	return true
}

// processStableFile hashes the file, verifies DB status, and registers it
func (w *Watcher) processStableFile(filePath string, fileSize int64) error {
	hash, err := CalculateSHA256(filePath)
	if err != nil {
		return fmt.Errorf("hash calculation failed: %w", err)
	}

	fileName := filepath.Base(filePath)
	now := time.Now()

	// Verify if the hash already exists
	var existingHash string
	err = w.db.QueryRow("SELECT Hash FROM TrackedFiles WHERE Hash = ?", hash).Scan(&existingHash)
	if err == nil {
		slog.Debug("File already tracked, skipping insertion", "hash", hash)
		return nil
	}
	if !errors.Is(err, sql.ErrNoRows) {
		return fmt.Errorf("database query failed: %w", err)
	}

	// Predict category!
	suggestedCat, confidence, err := PredictCategory(w.db, fileName)
	status := FileStatusNew
	var suggestedCatPtr *string
	var classifiedAtPtr *time.Time
	if err == nil && suggestedCat != "UNKNOWN" && suggestedCat != "" {
		status = FileStatusClassified
		suggestedCatPtr = &suggestedCat
		classifiedAtPtr = &now
	}

	// Insert new row into TrackedFiles matching the real schema
	_, err = w.db.Exec(`
		INSERT INTO TrackedFiles (Hash, FileName, OriginalPath, FileSize, Status, SuggestedCategory, Confidence, ClassifiedAt, CreatedDate, LastUpdateDate, IsActive)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)
	`, hash, fileName, filePath, fileSize, status, suggestedCatPtr, confidence, classifiedAtPtr, now, now)

	if err != nil {
		return fmt.Errorf("failed to register file in database: %w", err)
	}

	slog.Info("File successfully registered and classified in database via watcher", "fileName", fileName, "hash", hash, "suggestedCategory", suggestedCat, "confidence", confidence)

	// Broadcast file.discovered event over SSE
	if w.sse != nil {
		var catStr string
		if suggestedCatPtr != nil {
			catStr = *suggestedCatPtr
		}
		payloadBytes, _ := json.Marshal(map[string]interface{}{
			"hash":              hash,
			"fileName":          fileName,
			"originalPath":      filePath,
			"fileSize":          fileSize,
			"status":            int(status),
			"suggestedCategory": catStr,
			"confidence":        confidence,
			"lastUpdateDate":    now,
		})
		w.sse.Broadcast("file.discovered", string(payloadBytes))
	}

	// Trigger asynchronous notification via NotifyHub if configured
	go w.sendNotifyHubNotification(fileName, suggestedCat, confidence)

	return nil
}

// CalculateSHA256 calculates the SHA256 checksum of the specified file in a memory-efficient manner.
func CalculateSHA256(filePath string) (string, error) {
	file, err := os.Open(filePath)
	if err != nil {
		return "", err
	}
	defer file.Close()

	h := sha256.New()
	if _, err := io.Copy(h, file); err != nil {
		return "", err
	}

	return fmt.Sprintf("%x", h.Sum(nil)), nil
}

// ManualScan walks through all watched folders and manually triggers stability checks for any discovered files.
func (w *Watcher) ManualScan(ctx context.Context) {
	slog.Info("Manual watch folder scan triggered...")
	if w.sse != nil {
		w.sse.Broadcast("rescan.started", `{}`)
	}

	for _, folder := range w.config.WatchFolders {
		// Ensure folder exists before walking
		if _, err := os.Stat(folder); os.IsNotExist(err) {
			continue
		}
		
		err := filepath.Walk(folder, func(path string, info os.FileInfo, err error) error {
			if err != nil {
				return nil // Skip walk errors
			}
			if !info.IsDir() {
				w.triggerStabilityCheck(ctx, path)
			}
			return nil
		})
		if err != nil {
			slog.Error("Failed manual walk scan on folder", "path", folder, "err", err)
		}
	}
	slog.Info("Manual watch folder scan completed.")
	if w.sse != nil {
		w.sse.Broadcast("rescan.completed", `{}`)
	}
}

// sendNotifyHubNotification fetches credentials from database and sends HTTP POST to NotifyHub
func (w *Watcher) sendNotifyHubNotification(fileName string, category string, confidence float64) {
	// Query local database for active NotifyHub preferences (safe and fast in WAL mode)
	var channel, apiKey, url string
	
	err := w.db.QueryRow("SELECT Value FROM UserPreferences WHERE Key = 'notifyhub_channel' AND Category = 'notifyhub' AND IsActive = 1").Scan(&channel)
	if err != nil || channel == "none" || channel == "" {
		slog.Debug("NotifyHub notifications are disabled or not configured", "channel", channel)
		return // Notifications disabled
	}

	_ = w.db.QueryRow("SELECT Value FROM UserPreferences WHERE Key = 'notifyhub_apikey' AND Category = 'notifyhub' AND IsActive = 1").Scan(&apiKey)
	_ = w.db.QueryRow("SELECT Value FROM UserPreferences WHERE Key = 'notifyhub_url' AND Category = 'notifyhub' AND IsActive = 1").Scan(&url)

	if url == "" {
		url = "http://localhost:30180" // Fallback url
	}

	slog.Info("Preparing NotifyHub notification", "channel", channel, "url", url)

	// Compose message text (HTML for Telegram, Markdown for Discord/Email)
	var message string
	if channel == "telegram" {
		message = fmt.Sprintf("📂 <b>Nuovo File Rilevato</b>\nNome: <code>%s</code>\nCategoria suggerita: <b>%s</b> (Confidenza: <code>%.2f%%</code>)", 
			fileName, category, confidence * 100)
	} else {
		message = fmt.Sprintf("📂 **Nuovo File Rilevato**\nNome: `%s`\nCategoria suggerita: **%s** (Confidenza: `%.2f%%`)", 
			fileName, category, confidence * 100)
	}

	// Build request payload
	payload := map[string]string{
		"channel": channel,
		"message": message,
	}
	jsonBytes, err := json.Marshal(payload)
	if err != nil {
		slog.Error("Failed to marshal NotifyHub payload", "err", err)
		return
	}

	// Build and authenticate HTTP request
	reqUrl := fmt.Sprintf("%s/api/notify", strings.TrimRight(url, "/"))
	req, err := http.NewRequest("POST", reqUrl, bytes.NewBuffer(jsonBytes))
	if err != nil {
		slog.Error("Failed to create HTTP request for NotifyHub", "err", err)
		return
	}

	req.Header.Set("Content-Type", "application/json")
	if apiKey != "" {
		req.Header.Set("X-API-Key", apiKey)
	}

	// Run standard client request with timeout to avoid hanging connections
	client := &http.Client{Timeout: 10 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		slog.Error("Failed to send HTTP notification request to NotifyHub", "url", reqUrl, "err", err)
		return
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		slog.Error("NotifyHub returned error status", "status", resp.Status, "response", string(bodyBytes))
	} else {
		slog.Info("Notification successfully dispatched through NotifyHub", "fileName", fileName, "channel", channel)
	}
}
