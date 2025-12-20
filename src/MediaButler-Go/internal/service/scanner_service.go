package service

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"sync"
	"time"

	"github.com/chim331u/mediabutler-go/internal/config"
	"github.com/chim331u/mediabutler-go/pkg/result"
	"github.com/rs/zerolog"
)

// ScanResult contains metrics from a folder scan operation
type ScanResult struct {
	FilesDiscovered int
	ScanStartedAt   time.Time
	ScanCompletedAt time.Time
	ScannedFolders  []string
}

// ScannerService defines the interface for file scanning
type ScannerService interface {
	// ScanNow performs a one-time scan of configured watch folders
	ScanNow(ctx context.Context) result.Result[*ScanResult]

	// ScanFolder performs a one-time scan of a specific folder
	ScanFolder(ctx context.Context, folderPath string) result.Result[*ScanResult]

	// IsScanning returns true if a scan is currently in progress
	IsScanning() bool

	// IsMonitoring returns true if file system monitoring is active
	IsMonitoring() bool
}

type scannerService struct {
	fileService  FileService
	config       config.FileDiscoveryConfig
	logger       zerolog.Logger
	isScanning   bool
	isMonitoring bool
	mutex        sync.Mutex
	excludeRegex []*regexp.Regexp
}

// NewScannerService creates a new scanner service
func NewScannerService(fileService FileService, config config.FileDiscoveryConfig, logger zerolog.Logger) ScannerService {
	s := &scannerService{
		fileService: fileService,
		config:      config,
		logger:      logger,
	}

	// Compile regex patterns
	for _, pattern := range config.ExcludePatterns {
		if re, err := regexp.Compile(pattern); err == nil {
			s.excludeRegex = append(s.excludeRegex, re)
		} else {
			logger.Warn().Str("pattern", pattern).Err(err).Msg("Invalid exclude regex pattern")
		}
	}

	return s
}

// ScanNow triggers an immediate scan of configured watch folders
func (s *scannerService) ScanNow(ctx context.Context) result.Result[*ScanResult] {
	s.mutex.Lock()
	if s.isScanning {
		s.mutex.Unlock()
		return result.FailureMsg[*ScanResult]("Scan already in progress")
	}
	s.isScanning = true
	s.mutex.Unlock()

	defer func() {
		s.mutex.Lock()
		s.isScanning = false
		s.mutex.Unlock()
	}()

	startTime := time.Now()
	s.logger.Info().Msg("Starting folder scan")

	filesFound := 0
	scannedFolders := make([]string, 0, len(s.config.WatchFolders))

	for _, folder := range s.config.WatchFolders {
		count, err := s.scanFolderInternal(ctx, folder)
		if err != nil {
			s.logger.Error().Err(err).Str("folder", folder).Msg("Failed to scan folder")
			// Continue scanning other folders even if one fails
			continue
		}
		filesFound += count
		scannedFolders = append(scannedFolders, folder)
	}

	endTime := time.Now()

	s.logger.Info().
		Int("filesFound", filesFound).
		Dur("duration", endTime.Sub(startTime)).
		Msg("Folder scan completed")

	scanResult := &ScanResult{
		FilesDiscovered: filesFound,
		ScanStartedAt:   startTime,
		ScanCompletedAt: endTime,
		ScannedFolders:  scannedFolders,
	}

	return result.Success(scanResult)
}

// ScanFolder triggers an immediate scan of a specific folder
func (s *scannerService) ScanFolder(ctx context.Context, folderPath string) result.Result[*ScanResult] {
	s.mutex.Lock()
	if s.isScanning {
		s.mutex.Unlock()
		return result.FailureMsg[*ScanResult]("Scan already in progress")
	}
	s.isScanning = true
	s.mutex.Unlock()

	defer func() {
		s.mutex.Lock()
		s.isScanning = false
		s.mutex.Unlock()
	}()

	// Validate folder exists
	if _, err := os.Stat(folderPath); os.IsNotExist(err) {
		return result.Failure[*ScanResult](fmt.Errorf("folder not found: %s", folderPath))
	}

	startTime := time.Now()
	s.logger.Info().Str("folder", folderPath).Msg("Starting specific folder scan")

	count, err := s.scanFolderInternal(ctx, folderPath)
	if err != nil {
		s.logger.Error().Err(err).Str("folder", folderPath).Msg("Failed to scan folder")
		return result.Failure[*ScanResult](fmt.Errorf("failed to scan folder: %w", err))
	}

	endTime := time.Now()

	s.logger.Info().
		Str("folder", folderPath).
		Int("filesFound", count).
		Dur("duration", endTime.Sub(startTime)).
		Msg("Specific folder scan completed")

	scanResult := &ScanResult{
		FilesDiscovered: count,
		ScanStartedAt:   startTime,
		ScanCompletedAt: endTime,
		ScannedFolders:  []string{folderPath},
	}

	return result.Success(scanResult)
}

// IsScanning returns true if a scan is currently in progress
func (s *scannerService) IsScanning() bool {
	s.mutex.Lock()
	defer s.mutex.Unlock()
	return s.isScanning
}

// IsMonitoring returns true if file system monitoring is active
func (s *scannerService) IsMonitoring() bool {
	s.mutex.Lock()
	defer s.mutex.Unlock()
	return s.isMonitoring
}

func (s *scannerService) scanFolderInternal(ctx context.Context, root string) (int, error) {
	count := 0
	err := filepath.Walk(root, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}

		if info.IsDir() {
			// Skip hidden directories
			if strings.HasPrefix(info.Name(), ".") && info.Name() != "." {
				return filepath.SkipDir
			}
			return nil
		}

		// Check exclusion patterns
		for _, re := range s.excludeRegex {
			if re.MatchString(info.Name()) {
				return nil
			}
		}

		// Check extension
		ext := strings.ToLower(filepath.Ext(path))
		validExt := false
		for _, valid := range s.config.FileExtensions {
			if strings.ToLower(valid) == ext {
				validExt = true
				break
			}
		}
		if !validExt {
			return nil
		}

		// Check file size
		if info.Size() < int64(s.config.MinFileSizeMB*1024*1024) {
			return nil
		}

		// Register file
		res := s.fileService.RegisterFile(ctx, path)
		if res.IsSuccess() {
			count++
		}

		return nil
	})

	return count, err
}
