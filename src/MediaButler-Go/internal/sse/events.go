package sse

import "time"

// EventType constants for SSE events (matching SignalR event types)
const (
	// Scan events
	EventScanStarted   = "scan.started"
	EventScanFound     = "scan.found"
	EventScanCompleted = "scan.completed"

	// Move events
	EventMoveStarted   = "move.started"
	EventMoveProgress  = "move.progress"
	EventMoveCompleted = "move.completed"

	// Training events
	EventTrainingStarted   = "training.started"
	EventTrainingCompleted = "training.completed"

	// Batch events
	EventBatchStarted   = "batch.started"
	EventBatchProgress  = "batch.progress"
	EventBatchCompleted = "batch.completed"
	EventBatchFailed    = "batch.failed"

	// Error events
	EventErrorMoveFailed           = "error.move_failed"
	EventErrorClassificationFailed = "error.classification_failed"

	// Connection events
	EventConnected = "connected"
)

// ScanStartedEvent represents a scan operation start
type ScanStartedEvent struct {
	ScanID    string    `json:"scanId"`
	Path      string    `json:"path"`
	StartTime time.Time `json:"startTime"`
}

// ScanFoundEvent represents files discovered during a scan
type ScanFoundEvent struct {
	ScanID    string `json:"scanId"`
	FileCount int    `json:"fileCount"`
}

// ScanCompletedEvent represents a completed scan operation
type ScanCompletedEvent struct {
	ScanID        string    `json:"scanId"`
	TotalFiles    int       `json:"totalFiles"`
	NewFiles      int       `json:"newFiles"`
	CompletedTime time.Time `json:"completedTime"`
}

// MoveStartedEvent represents the start of a file move operation
type MoveStartedEvent struct {
	FileID   int    `json:"fileId"`
	FileName string `json:"fileName"`
	FromPath string `json:"fromPath"`
	ToPath   string `json:"toPath"`
}

// MoveProgressEvent represents progress during a batch move operation
type MoveProgressEvent struct {
	BatchID         string  `json:"batchId"`
	Completed       int     `json:"completed"`
	Total           int     `json:"total"`
	PercentComplete float64 `json:"percentComplete"`
}

// MoveCompletedEvent represents a completed file move operation
type MoveCompletedEvent struct {
	FileID   int    `json:"fileId"`
	FileName string `json:"fileName"`
	Success  bool   `json:"success"`
}

// TrainingStartedEvent represents the start of ML model training
type TrainingStartedEvent struct {
	TrainingID string    `json:"trainingId"`
	StartTime  time.Time `json:"startTime"`
}

// TrainingCompletedEvent represents completed ML model training
type TrainingCompletedEvent struct {
	TrainingID    string    `json:"trainingId"`
	Success       bool      `json:"success"`
	Accuracy      float64   `json:"accuracy"`
	CompletedTime time.Time `json:"completedTime"`
}

// BatchStartedEvent represents the start of a batch operation
type BatchStartedEvent struct {
	BatchID    string    `json:"batchId"`
	TotalFiles int       `json:"totalFiles"`
	StartTime  time.Time `json:"startTime"`
}

// BatchProgressEvent represents progress during a batch operation
type BatchProgressEvent struct {
	BatchID   string `json:"batchId"`
	Processed int    `json:"processed"`
	Total     int    `json:"total"`
	Succeeded int    `json:"succeeded"`
	Failed    int    `json:"failed"`
}

// BatchCompletedEvent represents a completed batch operation
type BatchCompletedEvent struct {
	BatchID       string    `json:"batchId"`
	Total         int       `json:"total"`
	Succeeded     int       `json:"succeeded"`
	Failed        int       `json:"failed"`
	CompletedTime time.Time `json:"completedTime"`
}

// ErrorEvent represents an error that occurred
type ErrorEvent struct {
	EventType string    `json:"eventType"`
	Message   string    `json:"message"`
	FileID    *string   `json:"fileId,omitempty"`
	Timestamp time.Time `json:"timestamp"`
}

// ConnectedEvent represents a successful SSE connection
type ConnectedEvent struct {
	ClientID string    `json:"clientId"`
	Time     time.Time `json:"time"`
}
