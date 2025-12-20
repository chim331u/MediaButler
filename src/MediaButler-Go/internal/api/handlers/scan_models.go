package handlers

import (
	"time"

	"github.com/chim331u/mediabutler-go/internal/service"
)

// ScanFoldersRequest is the optional request body for POST /api/files/scan
// Matches .NET ScanFoldersRequest model
type ScanFoldersRequest struct {
	// Optional timeout in seconds for the scan operation (default: 300)
	TimeoutSeconds int `json:"timeoutSeconds"`
}

// ScanSpecificFolderRequest is the request body for POST /api/files/scan/folder
// Matches .NET ScanSpecificFolderRequest model
type ScanSpecificFolderRequest struct {
	// Full path to the folder to scan (required)
	FolderPath string `json:"folderPath" validate:"required"`

	// Optional timeout in seconds for the scan operation (default: 300)
	TimeoutSeconds int `json:"timeoutSeconds"`
}

// ScanResultResponse represents the response for folder scan operations
// Matches .NET ScanResult model with exact field names and types
type ScanResultResponse struct {
	// Number of files discovered during the scan
	FilesDiscovered int `json:"filesDiscovered"`

	// Timestamp when the scan operation started
	ScanStartedAt time.Time `json:"scanStartedAt"`

	// Timestamp when the scan operation completed
	ScanCompletedAt time.Time `json:"scanCompletedAt"`

	// Whether file system monitoring is currently enabled
	MonitoringEnabled bool `json:"monitoringEnabled"`

	// List of paths currently being monitored
	MonitoredPaths []string `json:"monitoredPaths"`

	// Specific path that was scanned (for single folder scans)
	// Omitted for full watch folder scans
	ScannedPath *string `json:"scannedPath,omitempty"`

	// Duration of the scan operation in milliseconds
	ScanDurationMs float64 `json:"scanDurationMs"`
}

// toScanResultResponse converts a service.ScanResult to a ScanResultResponse
// with additional monitoring context
func toScanResultResponse(
	scanResult *service.ScanResult,
	monitoringEnabled bool,
	monitoredPaths []string,
	scannedPath *string,
) *ScanResultResponse {
	duration := scanResult.ScanCompletedAt.Sub(scanResult.ScanStartedAt)
	durationMs := float64(duration.Milliseconds())

	return &ScanResultResponse{
		FilesDiscovered:   scanResult.FilesDiscovered,
		ScanStartedAt:     scanResult.ScanStartedAt,
		ScanCompletedAt:   scanResult.ScanCompletedAt,
		MonitoringEnabled: monitoringEnabled,
		MonitoredPaths:    monitoredPaths,
		ScannedPath:       scannedPath,
		ScanDurationMs:    durationMs,
	}
}
