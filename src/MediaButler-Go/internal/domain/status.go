// Package domain contains core domain entities and business logic
// Following "Simple Made Easy" - domain is independent of infrastructure
package domain

import (
	"database/sql/driver"
	"fmt"
)

// FileStatus represents the processing state of a tracked file
// Maps to the .NET FileStatus enum for compatibility
type FileStatus int

const (
	// New indicates a file has been discovered but not yet processed
	FileStatusNew FileStatus = 0

	// Processing indicates classification is in progress
	FileStatusProcessing FileStatus = 1

	// Classified indicates ML classification is complete, awaiting user confirmation
	FileStatusClassified FileStatus = 2

	// ReadyToMove indicates category confirmed, ready for organization
	FileStatusReadyToMove FileStatus = 3

	// Moving indicates file is currently being moved
	FileStatusMoving FileStatus = 4

	// Moved indicates file has been successfully organized
	FileStatusMoved FileStatus = 5

	// Error indicates processing failed
	FileStatusError FileStatus = 6

	// Retry indicates file is queued for retry after error
	FileStatusRetry FileStatus = 7

	// Ignored indicates file has been marked as ignored by user
	FileStatusIgnored FileStatus = 8
)

// String returns the string representation of FileStatus
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
		return fmt.Sprintf("Unknown(%d)", s)
	}
}

// ParseFileStatus converts a string to FileStatus
func ParseFileStatus(s string) (FileStatus, error) {
	switch s {
	case "New":
		return FileStatusNew, nil
	case "Processing":
		return FileStatusProcessing, nil
	case "Classified":
		return FileStatusClassified, nil
	case "ReadyToMove":
		return FileStatusReadyToMove, nil
	case "Moving":
		return FileStatusMoving, nil
	case "Moved":
		return FileStatusMoved, nil
	case "Error":
		return FileStatusError, nil
	case "Retry":
		return FileStatusRetry, nil
	case "Ignored":
		return FileStatusIgnored, nil
	default:
		return 0, fmt.Errorf("invalid file status: %s", s)
	}
}

// Value implements driver.Valuer for database serialization
func (s FileStatus) Value() (driver.Value, error) {
	return int64(s), nil
}

// Scan implements sql.Scanner for database deserialization
func (s *FileStatus) Scan(value interface{}) error {
	if value == nil {
		*s = FileStatusNew
		return nil
	}

	switch v := value.(type) {
	case int64:
		*s = FileStatus(v)
		return nil
	case int:
		*s = FileStatus(v)
		return nil
	default:
		return fmt.Errorf("cannot scan %T into FileStatus", value)
	}
}

// MarshalJSON implements json.Marshaler
func (s FileStatus) MarshalJSON() ([]byte, error) {
	return []byte(fmt.Sprintf(`"%s"`, s.String())), nil
}

// UnmarshalJSON implements json.Unmarshaler
func (s *FileStatus) UnmarshalJSON(data []byte) error {
	// Remove quotes
	str := string(data)
	if len(str) >= 2 && str[0] == '"' && str[len(str)-1] == '"' {
		str = str[1 : len(str)-1]
	}

	parsed, err := ParseFileStatus(str)
	if err != nil {
		return err
	}

	*s = parsed
	return nil
}

// IsTerminal returns true if the status is a terminal state (Moved, Ignored)
func (s FileStatus) IsTerminal() bool {
	return s == FileStatusMoved || s == FileStatusIgnored
}

// CanTransitionTo checks if transition to another status is valid
func (s FileStatus) CanTransitionTo(target FileStatus) bool {
	// Terminal states cannot transition
	if s.IsTerminal() {
		return false
	}

	// Valid transitions based on workflow
	validTransitions := map[FileStatus][]FileStatus{
		FileStatusNew:         {FileStatusProcessing, FileStatusIgnored},
		FileStatusProcessing:  {FileStatusClassified, FileStatusError, FileStatusIgnored},
		FileStatusClassified:  {FileStatusReadyToMove, FileStatusIgnored},
		FileStatusReadyToMove: {FileStatusMoving, FileStatusIgnored},
		FileStatusMoving:      {FileStatusMoved, FileStatusError},
		FileStatusError:       {FileStatusRetry, FileStatusIgnored},
		FileStatusRetry:       {FileStatusProcessing, FileStatusIgnored},
	}

	allowed, exists := validTransitions[s]
	if !exists {
		return false
	}

	for _, valid := range allowed {
		if valid == target {
			return true
		}
	}

	return false
}
