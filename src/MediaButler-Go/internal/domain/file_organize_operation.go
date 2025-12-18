package domain

import (
	"fmt"
	"path/filepath"
	"strings"
)

// FileOrganizeOperation represents a single file organization operation
// This is a value object that encapsulates all information needed to move a file
type FileOrganizeOperation struct {
	TrackedFile       *TrackedFile           // The file to be organized
	ConfirmedCategory string                 // User-confirmed or ML-predicted category
	TargetPath        string                 // Generated target path for the file
	CustomTargetPath  *string                // Optional custom target path override
	Metadata          map[string]interface{} // Additional operation metadata
	ValidationError   string                 // Validation error message if invalid
}

// Create creates a new FileOrganizeOperation with validation
func NewFileOrganizeOperation(
	file *TrackedFile,
	confirmedCategory string,
	targetPath string,
) *FileOrganizeOperation {
	return &FileOrganizeOperation{
		TrackedFile:       file,
		ConfirmedCategory: confirmedCategory,
		TargetPath:        targetPath,
		Metadata:          make(map[string]interface{}),
	}
}

// Validate checks if the operation is valid
func (op *FileOrganizeOperation) Validate() bool {
	// Reset validation error
	op.ValidationError = ""

	// Check if file is provided
	if op.TrackedFile == nil {
		op.ValidationError = "Tracked file cannot be nil"
		return false
	}

	// Check if file hash is present
	if op.TrackedFile.Hash == "" {
		op.ValidationError = "File hash cannot be empty"
		return false
	}

	// Check if confirmed category is provided
	if strings.TrimSpace(op.ConfirmedCategory) == "" {
		op.ValidationError = "Confirmed category cannot be empty"
		return false
	}

	// Check if target path is provided (unless custom path is set)
	if op.CustomTargetPath == nil && strings.TrimSpace(op.TargetPath) == "" {
		op.ValidationError = "Target path cannot be empty"
		return false
	}

	// Use custom target path if provided
	effectivePath := op.TargetPath
	if op.CustomTargetPath != nil {
		effectivePath = *op.CustomTargetPath
	}

	// Validate target path format
	if !filepath.IsAbs(effectivePath) {
		op.ValidationError = fmt.Sprintf("Target path must be absolute: %s", effectivePath)
		return false
	}

	// Check for invalid characters in target path
	invalidChars := []string{"<", ">", ":", "\"", "|", "?", "*"}
	for _, char := range invalidChars {
		if strings.Contains(effectivePath, char) {
			op.ValidationError = fmt.Sprintf("Target path contains invalid character '%s': %s", char, effectivePath)
			return false
		}
	}

	// Check if file is in a valid state for organization
	if op.TrackedFile.Status == FileStatusMoved {
		op.ValidationError = "File has already been moved"
		return false
	}

	if op.TrackedFile.Status == FileStatusIgnored {
		op.ValidationError = "File is marked as ignored and cannot be organized"
		return false
	}

	if op.TrackedFile.Status == FileStatusError {
		op.ValidationError = "File is in error state and requires manual intervention"
		return false
	}

	// All validations passed
	return true
}

// GetEffectivePath returns the path to be used (custom or generated)
func (op *FileOrganizeOperation) GetEffectivePath() string {
	if op.CustomTargetPath != nil {
		return *op.CustomTargetPath
	}
	return op.TargetPath
}

// GetSourcePath returns the original file path
func (op *FileOrganizeOperation) GetSourcePath() string {
	return op.TrackedFile.OriginalPath
}

// GetFileName returns the file name
func (op *FileOrganizeOperation) GetFileName() string {
	return op.TrackedFile.FileName
}

// GetFileHash returns the file hash
func (op *FileOrganizeOperation) GetFileHash() string {
	return op.TrackedFile.Hash
}

// SetMetadata sets a metadata value
func (op *FileOrganizeOperation) SetMetadata(key string, value interface{}) {
	if op.Metadata == nil {
		op.Metadata = make(map[string]interface{})
	}
	op.Metadata[key] = value
}

// GetMetadata retrieves a metadata value
func (op *FileOrganizeOperation) GetMetadata(key string) (interface{}, bool) {
	if op.Metadata == nil {
		return nil, false
	}
	val, exists := op.Metadata[key]
	return val, exists
}

// String returns a string representation for logging
func (op *FileOrganizeOperation) String() string {
	return fmt.Sprintf(
		"FileOrganizeOperation{File: %s, Category: %s, Source: %s → Target: %s}",
		op.GetFileName(),
		op.ConfirmedCategory,
		op.GetSourcePath(),
		op.GetEffectivePath(),
	)
}

// ToFileOrganizeOperations converts a slice of BatchJobItems to FileOrganizeOperations
// This is a helper function to create operations from database records
func ToFileOrganizeOperations(items []*BatchJobItem, fileMap map[string]*TrackedFile) []*FileOrganizeOperation {
	operations := make([]*FileOrganizeOperation, 0, len(items))

	for _, item := range items {
		// Lookup the tracked file
		file, exists := fileMap[item.FileHash]
		if !exists {
			// File not found - skip this operation
			continue
		}

		// Create operation
		targetPath := ""
		if item.TargetPath != nil && *item.TargetPath != "" {
			targetPath = *item.TargetPath
		}
		op := NewFileOrganizeOperation(
			file,
			item.ConfirmedCategory,
			targetPath,
		)

		// Set custom target path if provided
		if item.CustomTargetPath != nil {
			op.CustomTargetPath = item.CustomTargetPath
		}

		// Copy metadata
		if item.Metadata != nil {
			op.Metadata = item.Metadata
		}

		operations = append(operations, op)
	}

	return operations
}
