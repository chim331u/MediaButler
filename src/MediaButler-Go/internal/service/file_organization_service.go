package service

import (
	"context"
	"fmt"
	"path/filepath"

	"github.com/lucapaganotti/mediabutler-go/internal/jobs/batch"
	"github.com/lucapaganotti/mediabutler-go/pkg/result"
	"github.com/rs/zerolog"
)

// FileOrganizationService handles file organization logic
type FileOrganizationService interface {
	batch.FileOrganizer // Implements the FileOrganizer interface from batch package
}

// fileOrganizationService implements FileOrganizationService
type fileOrganizationService struct {
	fileService   FileService
	libraryPath   string
	logger        zerolog.Logger
}

// NewFileOrganizationService creates a new FileOrganizationService
func NewFileOrganizationService(
	fileService FileService,
	libraryPath string,
	logger zerolog.Logger,
) FileOrganizationService {
	return &fileOrganizationService{
		fileService: fileService,
		libraryPath: libraryPath,
		logger:      logger.With().Str("component", "file-organization").Logger(),
	}
}

// OrganizeFile organizes a file by moving it to the target location
func (s *fileOrganizationService) OrganizeFile(
	ctx context.Context,
	fileHash string,
	confirmedCategory string,
) result.Result[batch.OrganizedFileResult] {
	s.logger.Info().
		Str("file_hash", fileHash).
		Str("category", confirmedCategory).
		Msg("Organizing file")

	// Get file from database
	fileResult := s.fileService.GetFileByHash(ctx, fileHash)
	if fileResult.IsFailure() {
		s.logger.Error().Err(fileResult.Error()).Str("file_hash", fileHash).Msg("File not found")
		return result.Failure[batch.OrganizedFileResult](fileResult.Error())
	}

	file := fileResult.Value()

	// Validate file can be organized
	if file.Status == "Moved" {
		return result.Failure[batch.OrganizedFileResult](
			fmt.Errorf("file already moved: %s", fileHash),
		)
	}

	if file.Status == "Ignored" {
		return result.Failure[batch.OrganizedFileResult](
			fmt.Errorf("file is ignored: %s", fileHash),
		)
	}

	// Generate target path
	// Format: /library/{CATEGORY}/{filename}
	targetPath := filepath.Join(s.libraryPath, confirmedCategory, file.FileName)

	// TODO: Implement actual file moving logic
	// For now, just return the target path without moving
	// This will be implemented when file moving service is added

	organizedResult := batch.OrganizedFileResult{
		TargetPath: targetPath,
		ActualPath: targetPath,
	}

	s.logger.Info().
		Str("file_hash", fileHash).
		Str("target_path", targetPath).
		Msg("File organization planned (not yet moved)")

	return result.Success(organizedResult)
}
