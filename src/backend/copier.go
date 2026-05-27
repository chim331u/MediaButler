package main

import (
	"database/sql"
	"encoding/json"
	"io"
	"log/slog"
	"os"
	"path/filepath"
	"strings"
	"time"
)

type MoveProgressPayload struct {
	Hash        string  `json:"hash"`
	FileName    string  `json:"fileName"`
	Progress    float64 `json:"progress"`
	BytesCopied int64   `json:"bytesCopied"`
	TotalBytes  int64   `json:"totalBytes"`
}

type MoveCompletedPayload struct {
	Hash       string `json:"hash"`
	FileName   string `json:"fileName"`
	TargetPath string `json:"targetPath"`
}

type MoveErrorPayload struct {
	Hash     string `json:"hash"`
	FileName string `json:"fileName"`
	Error    string `json:"error"`
}

// FindRelatedFiles searches for files with the same prefix in the same folder (e.g. subtitles, nfo)
func FindRelatedFiles(originalPath string) ([]string, error) {
	dir := filepath.Dir(originalPath)
	base := filepath.Base(originalPath)
	ext := filepath.Ext(originalPath)
	prefix := strings.TrimSuffix(base, ext)

	entries, err := os.ReadDir(dir)
	if err != nil {
		return nil, err
	}

	var related []string
	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}
		name := entry.Name()
		if name == base {
			continue
		}
		// If the file starts with the prefix of the video file
		if strings.HasPrefix(name, prefix) {
			related = append(related, filepath.Join(dir, name))
		}
	}
	return related, nil
}

// CopyFileWithProgress copies a file in 1MB chunks and updates progress
func CopyFileWithProgress(src, dst string, progressFunc func(bytesCopied, totalBytes int64)) error {
	srcFile, err := os.Open(src)
	if err != nil {
		return err
	}
	defer srcFile.Close()

	srcInfo, err := srcFile.Stat()
	if err != nil {
		return err
	}
	totalBytes := srcInfo.Size()

	// Ensure destination directory exists
	dstDir := filepath.Dir(dst)
	if err := os.MkdirAll(dstDir, 0755); err != nil {
		return err
	}

	dstFile, err := os.OpenFile(dst, os.O_CREATE|os.O_WRONLY|os.O_TRUNC, srcInfo.Mode())
	if err != nil {
		return err
	}
	defer dstFile.Close()

	buffer := make([]byte, 1024*1024) // 1MB chunk limit for low RAM (QNAP NAS ARM32 v7)
	var totalCopied int64 = 0

	for {
		n, err := srcFile.Read(buffer)
		if n > 0 {
			_, writeErr := dstFile.Write(buffer[:n])
			if writeErr != nil {
				return writeErr
			}
			totalCopied += int64(n)
			if progressFunc != nil {
				progressFunc(totalCopied, totalBytes)
			}
			// DevOps Optimization: Prevent full disk I/O starvation on QNAP NAS (ARM32)
			// A tiny sleep allows the system scheduler and SQLite to breathe.
			time.Sleep(5 * time.Millisecond)
		}
		if err == io.EOF {
			break
		}
		if err != nil {
			return err
		}
	}

	return dstFile.Sync()
}

// MoveFileWithProgress tries fast os.Rename first, and falls back to buffered copy-then-delete if needed
func MoveFileWithProgress(src, dst string, progressFunc func(bytesCopied, totalBytes int64)) error {
	dstDir := filepath.Dir(dst)
	if err := os.MkdirAll(dstDir, 0755); err != nil {
		return err
	}

	// Try Rename first (fast path)
	err := os.Rename(src, dst)
	if err == nil {
		if progressFunc != nil {
			info, err := os.Stat(dst)
			if err == nil {
				progressFunc(info.Size(), info.Size())
			} else {
				progressFunc(1, 1)
			}
		}
		return nil
	}

	slog.Info("os.Rename failed, falling back to buffered copy-then-delete", "src", src, "dst", dst, "reason", err.Error())

	// Fallback to slow copy path
	err = CopyFileWithProgress(src, dst, progressFunc)
	if err != nil {
		os.Remove(dst) // clean up partial file
		return err
	}

	// Copy succeeded, remove original
	return os.Remove(src)
}

// MoveRelatedFiles moves matching related files to the target directory
func MoveRelatedFiles(relatedFiles []string, targetDir string) {
	for _, related := range relatedFiles {
		targetRelatedPath := filepath.Join(targetDir, filepath.Base(related))
		slog.Info("Moving related file", "src", related, "dst", targetRelatedPath)
		err := os.Rename(related, targetRelatedPath)
		if err != nil {
			slog.Warn("Rename related file failed, trying copy-then-delete", "src", related, "err", err)
			err = CopyFileWithProgress(related, targetRelatedPath, nil)
			if err == nil {
				os.Remove(related)
			} else {
				slog.Error("Failed to copy related file", "src", related, "err", err)
			}
		}
	}
}

// MoveFileAsync moves a file asynchronously and broadcasts progress over SSE
func MoveFileAsync(db *sql.DB, sse *SSEBroker, hash string, category string, destBaseFolder string) {
	go func() {
		now := time.Now()
		// 1. Fetch file from database
		var f TrackedFile
		query := `
			SELECT Hash, FileName, OriginalPath, FileSize, Status, SuggestedCategory, Confidence, Category, TargetPath, ClassifiedAt, MovedAt, LastError, LastErrorAt, RetryCount, CreatedDate, LastUpdateDate, Note, IsActive, MovedToPath 
			FROM TrackedFiles 
			WHERE Hash = ? AND IsActive = 1
		`
		err := db.QueryRow(query, hash).Scan(
			&f.Hash, &f.FileName, &f.OriginalPath, &f.FileSize, &f.Status,
			&f.SuggestedCategory, &f.Confidence, &f.Category, &f.TargetPath,
			&f.ClassifiedAt, &f.MovedAt, &f.LastError, &f.LastErrorAt,
			&f.RetryCount, &f.CreatedDate, &f.LastUpdateDate, &f.Note,
			&f.IsActive, &f.MovedToPath,
		)
		if err != nil {
			slog.Error("Async move: file not found in DB", "hash", hash, "err", err)
			return
		}

		// 2. Update status to Moving
		_, err = db.Exec(`
			UPDATE TrackedFiles 
			SET Status = ?, LastUpdateDate = ? 
			WHERE Hash = ?
		`, FileStatusMoving, now, hash)
		if err != nil {
			slog.Error("Async move: failed to update status to Moving", "hash", hash, "err", err)
			return
		}

		// Calculate target paths
		targetDir := filepath.Join(destBaseFolder, category)
		targetPath := filepath.Join(targetDir, f.FileName)

		slog.Info("Starting async move", "hash", hash, "src", f.OriginalPath, "dst", targetPath)

		// 3. Scan for related files
		relatedFiles, rerr := FindRelatedFiles(f.OriginalPath)
		if rerr != nil {
			slog.Warn("Failed to scan for related files", "path", f.OriginalPath, "err", rerr)
		}

		// 4. Perform move with progress callback
		var lastPercent float64 = -1.0
		progressFunc := func(copied, total int64) {
			percent := 0.0
			if total > 0 {
				percent = (float64(copied) / float64(total)) * 100.0
			}
			// Throttle progress updates (0%, 100% and steps of >= 5%)
			if percent == 0 || percent == 100 || percent-lastPercent >= 5.0 {
				lastPercent = percent
				payloadBytes, _ := json.Marshal(MoveProgressPayload{
					Hash:        hash,
					FileName:    f.FileName,
					Progress:    percent,
					BytesCopied: copied,
					TotalBytes:  total,
				})
				sse.Broadcast("file.move.progress", string(payloadBytes))
			}
		}

		err = MoveFileWithProgress(f.OriginalPath, targetPath, progressFunc)
		if err != nil {
			slog.Error("Async move failed", "hash", hash, "src", f.OriginalPath, "dst", targetPath, "err", err)

			errStr := err.Error()
			errorNow := time.Now()
			_, dbErr := db.Exec(`
				UPDATE TrackedFiles 
				SET Status = ?, LastError = ?, LastErrorAt = ?, LastUpdateDate = ? 
				WHERE Hash = ?
			`, FileStatusError, errStr, errorNow, errorNow, hash)
			if dbErr != nil {
				slog.Error("Failed to update failure status in DB", "hash", hash, "err", dbErr)
			}

			payloadBytes, _ := json.Marshal(MoveErrorPayload{
				Hash:     hash,
				FileName: f.FileName,
				Error:    errStr,
			})
			sse.Broadcast("file.move.error", string(payloadBytes))
			return
		}

		// 5. Successfully moved main file, now move related files
		if len(relatedFiles) > 0 {
			MoveRelatedFiles(relatedFiles, targetDir)
		}

		// 6. Update database status to Moved
		movedNow := time.Now()
		_, dbErr := db.Exec(`
			UPDATE TrackedFiles 
			SET Status = ?, MovedToPath = ?, TargetPath = ?, MovedAt = ?, LastUpdateDate = ? 
			WHERE Hash = ?
		`, FileStatusMoved, targetPath, targetPath, movedNow, movedNow, hash)
		if dbErr != nil {
			slog.Error("Failed to update completed status in DB", "hash", hash, "err", dbErr)
		}

		slog.Info("Async move completed successfully", "hash", hash, "targetPath", targetPath)

		payloadBytes, _ := json.Marshal(MoveCompletedPayload{
			Hash:       hash,
			FileName:   f.FileName,
			TargetPath: targetPath,
		})
		sse.Broadcast("file.move.completed", string(payloadBytes))
	}()
}
