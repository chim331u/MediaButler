package main

import (
	"time"
)

type FileStatus int

const (
	FileStatusNew           FileStatus = 0 // Just discovered
	FileStatusProcessing    FileStatus = 1 // Being processed
	FileStatusClassified    FileStatus = 2 // ML classification complete
	FileStatusReadyToMove   FileStatus = 3 // Confirmed, ready for organization
	FileStatusMoving        FileStatus = 4 // File move in progress
	FileStatusMoved         FileStatus = 5 // Successfully organized
	FileStatusError         FileStatus = 6 // Processing failed
	FileStatusRetry         FileStatus = 7 // Queued for retry
	FileStatusIgnored       FileStatus = 8 // User marked as ignored
)

func (s FileStatus) String() string {
	switch s {
	case FileStatusNew:
		return "New"
	case FileStatusProcessing:
		return "Processing"
	case FileStatusClassified:
		return "Classified"
	case FileStatusReadyToMove:
		return "ReadyToMove"
	case FileStatusMoving:
		return "Moving"
	case FileStatusMoved:
		return "Moved"
	case FileStatusError:
		return "Error"
	case FileStatusRetry:
		return "Retry"
	case FileStatusIgnored:
		return "Ignored"
	default:
		return "Unknown"
	}
}

// TrackedFile represents the main file entity corresponding to the real TrackedFiles table
type TrackedFile struct {
	Hash              string     `json:"hash"`
	FileName          string     `json:"fileName"`
	OriginalPath      string     `json:"originalPath"`
	FileSize          int64      `json:"fileSize"`
	Status            FileStatus `json:"status"`
	SuggestedCategory *string    `json:"suggestedCategory,omitempty"`
	Confidence        float64    `json:"confidence"`
	Category          *string    `json:"category,omitempty"`
	TargetPath        *string    `json:"targetPath,omitempty"`
	ClassifiedAt      *time.Time `json:"classifiedAt,omitempty"`
	MovedAt           *time.Time `json:"movedAt,omitempty"`
	LastError         *string    `json:"lastError,omitempty"`
	LastErrorAt       *time.Time `json:"lastErrorAt,omitempty"`
	RetryCount        int        `json:"retryCount"`
	CreatedDate       time.Time  `json:"createdDate"`
	LastUpdateDate    time.Time  `json:"lastUpdateDate"`
	Note              *string    `json:"note,omitempty"`
	IsActive          bool       `json:"isActive"`
	MovedToPath       *string    `json:"movedToPath,omitempty"`
}

// UserPreference represents user preferences stored in the database
type UserPreference struct {
	ID             string    `json:"id"`
	UserID         string    `json:"userId"`
	Key            string    `json:"key"`
	Value          string    `json:"value"`
	Category       string    `json:"category"`
	CreatedDate    time.Time `json:"createdDate"`
	LastUpdateDate time.Time `json:"lastUpdateDate"`
	Note           *string   `json:"note,omitempty"`
	IsActive       bool      `json:"isActive"`
}

// AddFileRequest represents the body for creating a new file tracker
type AddFileRequest struct {
	FilePath string `json:"filePath"`
}

// ConfirmCategoryRequest represents the body for confirming a file's category
type ConfirmCategoryRequest struct {
	Category string `json:"category"`
}

// MarkMovedRequest represents the body for marking a file as moved
type MarkMovedRequest struct {
	TargetPath string `json:"targetPath"`
}

// FSItem represents a file or directory item in the filesystem listing
type FSItem struct {
	Name      string `json:"name"`
	Path      string `json:"path"`
	IsDir     bool   `json:"isDir"`
	SizeBytes int64  `json:"sizeBytes"`
}

// UpdateFileRequest represents the body for updating a file's category and status inline
type UpdateFileRequest struct {
	Category string `json:"category"`
	Status   int    `json:"status"`
}

