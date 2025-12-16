package domain

import (
	"fmt"
	"time"
)

// Now returns the current UTC time
// Extracted as a function for testability
func Now() time.Time {
	return time.Now().UTC()
}

// BaseEntity provides audit trail fields for all domain entities
// Implements soft delete and automatic timestamp management
type BaseEntity struct {
	ID             int64      `db:"id" json:"id"`
	CreatedDate    time.Time  `db:"created_date" json:"createdDate"`
	LastUpdateDate time.Time  `db:"last_update_date" json:"lastUpdateDate"`
	IsActive       bool       `db:"is_active" json:"isActive"`
	Note           *string    `db:"note" json:"note,omitempty"`
}

// MarkAsModified updates the LastUpdateDate to current UTC time
func (e *BaseEntity) MarkAsModified() {
	e.LastUpdateDate = time.Now().UTC()
}

// SoftDelete marks the entity as inactive (soft delete)
func (e *BaseEntity) SoftDelete(reason *string) {
	e.IsActive = false
	e.Note = reason
	e.MarkAsModified()
}

// Restore reactivates a soft-deleted entity
func (e *BaseEntity) Restore(reason *string) {
	e.IsActive = true
	e.Note = reason
	e.MarkAsModified()
}

// TrackedFile represents a media file being tracked by the system
// Corresponds to the .NET TrackedFile entity
type TrackedFile struct {
	BaseEntity

	// File Identification
	Hash         string `db:"hash" json:"hash"`                   // SHA256 hash (primary key)
	FileName     string `db:"file_name" json:"fileName"`          // Original filename
	OriginalPath string `db:"original_path" json:"originalPath"`  // Full path where file was found
	FileSize     int64  `db:"file_size" json:"fileSize"`          // File size in bytes

	// Processing State
	Status FileStatus `db:"status" json:"status"` // Current workflow status

	// ML Classification
	SuggestedCategory *string  `db:"suggested_category" json:"suggestedCategory,omitempty"` // ML prediction
	Confidence        *float64 `db:"confidence" json:"confidence,omitempty"`                 // ML confidence score (0.0-1.0)
	ClassifiedAt      *time.Time `db:"classified_at" json:"classifiedAt,omitempty"`          // When classification completed

	// User Confirmation
	Category *string `db:"category" json:"category,omitempty"` // User-confirmed category

	// File Organization
	TargetPath  *string    `db:"target_path" json:"targetPath,omitempty"`   // Calculated target path
	MovedToPath *string    `db:"moved_to_path" json:"movedToPath,omitempty"` // Actual location after move
	MovedAt     *time.Time `db:"moved_at" json:"movedAt,omitempty"`         // When file was moved

	// Error Handling
	LastError   *string    `db:"last_error" json:"lastError,omitempty"`       // Last error message
	LastErrorAt *time.Time `db:"last_error_at" json:"lastErrorAt,omitempty"` // When last error occurred
	RetryCount  int        `db:"retry_count" json:"retryCount"`               // Number of retry attempts
}

// NewTrackedFile creates a new TrackedFile with default values
func NewTrackedFile(hash, fileName, originalPath string, fileSize int64) *TrackedFile {
	now := time.Now().UTC()
	return &TrackedFile{
		BaseEntity: BaseEntity{
			CreatedDate:    now,
			LastUpdateDate: now,
			IsActive:       true,
		},
		Hash:         hash,
		FileName:     fileName,
		OriginalPath: originalPath,
		FileSize:     fileSize,
		Status:       FileStatusNew,
		RetryCount:   0,
	}
}

// MarkAsClassified updates the file with ML classification results
func (f *TrackedFile) MarkAsClassified(suggestedCategory string, confidence float64) error {
	if f.Status != FileStatusProcessing {
		return fmt.Errorf("cannot classify file in status %s", f.Status)
	}

	now := time.Now().UTC()
	f.SuggestedCategory = &suggestedCategory
	f.Confidence = &confidence
	f.ClassifiedAt = &now
	f.Status = FileStatusClassified
	f.MarkAsModified()

	return nil
}

// ConfirmCategory confirms the category and prepares file for moving
func (f *TrackedFile) ConfirmCategory(category string, targetPath string) error {
	if f.Status != FileStatusClassified {
		return fmt.Errorf("cannot confirm category for file in status %s", f.Status)
	}

	f.Category = &category
	f.TargetPath = &targetPath
	f.Status = FileStatusReadyToMove
	f.MarkAsModified()

	return nil
}

// MarkAsMoving updates status to indicate file move is in progress
func (f *TrackedFile) MarkAsMoving() error {
	if f.Status != FileStatusReadyToMove {
		return fmt.Errorf("cannot mark as moving from status %s", f.Status)
	}

	f.Status = FileStatusMoving
	f.MarkAsModified()

	return nil
}

// MarkAsMoved updates the file to indicate successful organization
func (f *TrackedFile) MarkAsMoved(movedToPath string) error {
	if f.Status != FileStatusMoving {
		return fmt.Errorf("cannot mark as moved from status %s", f.Status)
	}

	now := time.Now().UTC()
	f.MovedToPath = &movedToPath
	f.MovedAt = &now
	f.Status = FileStatusMoved
	f.MarkAsModified()

	return nil
}

// MarkAsError records an error and updates retry count
func (f *TrackedFile) MarkAsError(errorMessage string) {
	now := time.Now().UTC()
	f.LastError = &errorMessage
	f.LastErrorAt = &now
	f.RetryCount++
	f.Status = FileStatusError
	f.MarkAsModified()
}

// MarkForRetry prepares the file for retry after error
func (f *TrackedFile) MarkForRetry() error {
	if f.Status != FileStatusError {
		return fmt.Errorf("cannot retry file not in error status")
	}

	f.Status = FileStatusRetry
	f.MarkAsModified()

	return nil
}

// MarkAsIgnored marks the file as ignored by user
func (f *TrackedFile) MarkAsIgnored(reason *string) error {
	if f.Status == FileStatusMoved {
		return fmt.Errorf("cannot ignore file that has already been moved")
	}

	f.Status = FileStatusIgnored
	f.Note = reason
	f.MarkAsModified()

	return nil
}

// CanRetry checks if file can be retried based on retry count
func (f *TrackedFile) CanRetry(maxRetries int) bool {
	return f.Status == FileStatusError && f.RetryCount < maxRetries
}

// GetConfidencePercentage returns confidence as a percentage (0-100)
func (f *TrackedFile) GetConfidencePercentage() *float64 {
	if f.Confidence == nil {
		return nil
	}
	percentage := *f.Confidence * 100
	return &percentage
}

// GetConfidenceLevel returns a human-readable confidence level
func (f *TrackedFile) GetConfidenceLevel() string {
	if f.Confidence == nil {
		return "Unknown"
	}

	switch {
	case *f.Confidence >= 0.85:
		return "High"
	case *f.Confidence >= 0.50:
		return "Medium"
	default:
		return "Low"
	}
}

// GetStatusDescription returns a human-readable status description
func (f *TrackedFile) GetStatusDescription() string {
	switch f.Status {
	case FileStatusNew:
		return "Discovered, awaiting classification"
	case FileStatusProcessing:
		return "Classification in progress"
	case FileStatusClassified:
		return "Classified, awaiting confirmation"
	case FileStatusReadyToMove:
		return "Ready to be organized"
	case FileStatusMoving:
		return "Being moved to destination"
	case FileStatusMoved:
		return "Successfully organized"
	case FileStatusError:
		return fmt.Sprintf("Error (retry %d)", f.RetryCount)
	case FileStatusRetry:
		return "Queued for retry"
	case FileStatusIgnored:
		return "Ignored by user"
	default:
		return "Unknown status"
	}
}

// Validate performs domain validation on the TrackedFile
func (f *TrackedFile) Validate() error {
	if f.Hash == "" {
		return fmt.Errorf("hash is required")
	}
	if len(f.Hash) != 64 {
		return fmt.Errorf("hash must be 64 characters (SHA256)")
	}
	if f.FileName == "" {
		return fmt.Errorf("fileName is required")
	}
	if f.OriginalPath == "" {
		return fmt.Errorf("originalPath is required")
	}
	if f.FileSize <= 0 {
		return fmt.Errorf("fileSize must be greater than 0")
	}
	if f.Confidence != nil && (*f.Confidence < 0 || *f.Confidence > 1) {
		return fmt.Errorf("confidence must be between 0 and 1")
	}

	return nil
}
