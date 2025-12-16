package service

import (
	"context"
	"crypto/sha256"
	"fmt"
	"io"
	"os"
	"path/filepath"

	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/internal/repository"
	"github.com/lucapaganotti/mediabutler-go/pkg/pagination"
	"github.com/lucapaganotti/mediabutler-go/pkg/result"
)

// FileService defines business logic for file management
type FileService interface {
	// File Registration
	RegisterFile(ctx context.Context, filePath string) result.Result[*domain.TrackedFile]
	RegisterFileWithHash(ctx context.Context, filePath, hash string, fileSize int64) result.Result[*domain.TrackedFile]

	// File Retrieval
	GetFileByHash(ctx context.Context, hash string) result.Result[*domain.TrackedFile]
	GetFilesByStatus(ctx context.Context, status domain.FileStatus, page *pagination.Request) result.Result[*pagination.Response[domain.TrackedFile]]
	GetPendingFiles(ctx context.Context) result.Result[[]domain.TrackedFile]
	GetReadyForClassification(ctx context.Context, limit int) result.Result[[]domain.TrackedFile]
	GetReadyForMoving(ctx context.Context, limit int) result.Result[[]domain.TrackedFile]

	// File State Transitions
	MarkAsClassified(ctx context.Context, hash string, category string, confidence float64) result.Result[bool]
	ConfirmCategory(ctx context.Context, hash string, category string) result.Result[bool]
	MarkAsMoved(ctx context.Context, hash string, movedToPath string) result.Result[bool]
	MarkAsError(ctx context.Context, hash string, errorMessage string) result.Result[bool]
	MarkAsIgnored(ctx context.Context, hash string, reason *string) result.Result[bool]

	// File Checks
	ExistsByHash(ctx context.Context, hash string) result.Result[bool]
	ExistsByPath(ctx context.Context, path string) result.Result[bool]

	// Batch Operations
	GetAllCategories(ctx context.Context) result.Result[[]string]
}

// fileService implements FileService
type fileService struct {
	repo repository.FileRepository
	uow  repository.UnitOfWork
}

// NewFileService creates a new FileService instance
func NewFileService(repo repository.FileRepository, uow repository.UnitOfWork) FileService {
	return &fileService{
		repo: repo,
		uow:  uow,
	}
}

// RegisterFile registers a new file by calculating its hash
func (s *fileService) RegisterFile(ctx context.Context, filePath string) result.Result[*domain.TrackedFile] {
	// Validate file exists
	fileInfo, err := os.Stat(filePath)
	if err != nil {
		if os.IsNotExist(err) {
			return result.Failure[*domain.TrackedFile](fmt.Errorf("file not found: %s", filePath))
		}
		return result.Failure[*domain.TrackedFile](fmt.Errorf("stat file: %w", err))
	}

	// Calculate SHA256 hash
	hash, err := calculateFileHash(filePath)
	if err != nil {
		return result.Failure[*domain.TrackedFile](fmt.Errorf("calculate hash: %w", err))
	}

	return s.RegisterFileWithHash(ctx, filePath, hash, fileInfo.Size())
}

// RegisterFileWithHash registers a file with a pre-calculated hash
func (s *fileService) RegisterFileWithHash(ctx context.Context, filePath, hash string, fileSize int64) result.Result[*domain.TrackedFile] {
	// Check if file already exists
	existsResult := s.repo.ExistsByHash(ctx, hash)
	if existsResult.IsFailure() {
		return result.Failure[*domain.TrackedFile](existsResult.Error())
	}
	if existsResult.Value() {
		return result.Failure[*domain.TrackedFile](fmt.Errorf("file already registered: %s", hash))
	}

	// Create new tracked file
	fileName := filepath.Base(filePath)
	file := domain.NewTrackedFile(hash, fileName, filePath, fileSize)

	// Validate domain rules
	if err := file.Validate(); err != nil {
		return result.Failure[*domain.TrackedFile](fmt.Errorf("validation failed: %w", err))
	}

	// Save to database
	createResult := s.repo.Create(ctx, file)
	if createResult.IsFailure() {
		return result.Failure[*domain.TrackedFile](createResult.Error())
	}

	return result.Success(file)
}

// GetFileByHash retrieves a tracked file by its hash
func (s *fileService) GetFileByHash(ctx context.Context, hash string) result.Result[*domain.TrackedFile] {
	if len(hash) != 64 {
		return result.Failure[*domain.TrackedFile](fmt.Errorf("invalid hash length: expected 64, got %d", len(hash)))
	}

	return s.repo.GetByHash(ctx, hash)
}

// GetFilesByStatus retrieves files by status with pagination
func (s *fileService) GetFilesByStatus(ctx context.Context, status domain.FileStatus, page *pagination.Request) result.Result[*pagination.Response[domain.TrackedFile]] {
	if page == nil {
		page = pagination.DefaultRequest()
	}

	// Get files
	filesResult := s.repo.GetFilesByStatus(ctx, status, page.Take, page.Skip)
	if filesResult.IsFailure() {
		return result.Failure[*pagination.Response[domain.TrackedFile]](filesResult.Error())
	}

	// Get total count (would need a separate query in real implementation)
	// For now, we'll use a simplified approach
	files := filesResult.Value()
	total := len(files) // Simplified - in production, need actual count query

	response := pagination.NewResponse(files, total, page.Skip, page.Take)
	return result.Success(response)
}

// GetPendingFiles retrieves files awaiting user confirmation
func (s *fileService) GetPendingFiles(ctx context.Context) result.Result[[]domain.TrackedFile] {
	return s.repo.GetFilesAwaitingConfirmation(ctx)
}

// GetReadyForClassification retrieves files ready for ML classification
func (s *fileService) GetReadyForClassification(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	if limit <= 0 || limit > 100 {
		limit = 50 // Default limit
	}
	return s.repo.GetFilesReadyForClassification(ctx, limit)
}

// GetReadyForMoving retrieves files ready to be moved
func (s *fileService) GetReadyForMoving(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	if limit <= 0 || limit > 100 {
		limit = 50
	}
	return s.repo.GetFilesReadyForMoving(ctx, limit)
}

// MarkAsClassified updates a file with ML classification results
func (s *fileService) MarkAsClassified(ctx context.Context, hash string, category string, confidence float64) result.Result[bool] {
	err := repository.WithTransaction(ctx, s.uow, func(tx *repository.Transaction) error {
		// Get file
		fileResult := tx.Files().GetByHash(ctx, hash)
		if fileResult.IsFailure() {
			return fileResult.Error()
		}

		file := fileResult.Value()

		// Update with classification
		if err := file.MarkAsClassified(category, confidence); err != nil {
			return err
		}

		// Save changes
		updateResult := tx.Files().Update(ctx, file)
		if updateResult.IsFailure() {
			return updateResult.Error()
		}

		return nil
	})

	if err != nil {
		return result.Failure[bool](err)
	}

	return result.Success(true)
}

// ConfirmCategory confirms a file's category and prepares it for moving
func (s *fileService) ConfirmCategory(ctx context.Context, hash string, category string) result.Result[bool] {
	err := repository.WithTransaction(ctx, s.uow, func(tx *repository.Transaction) error {
		// Get file
		fileResult := tx.Files().GetByHash(ctx, hash)
		if fileResult.IsFailure() {
			return fileResult.Error()
		}

		file := fileResult.Value()

		// Generate target path (simplified - in production, use PathGenerationService)
		targetPath := filepath.Join("/library", category, file.FileName)

		// Confirm category
		if err := file.ConfirmCategory(category, targetPath); err != nil {
			return err
		}

		// Save changes
		updateResult := tx.Files().Update(ctx, file)
		return updateResult.Error()
	})

	if err != nil {
		return result.Failure[bool](err)
	}

	return result.Success(true)
}

// MarkAsMoved marks a file as successfully moved
func (s *fileService) MarkAsMoved(ctx context.Context, hash string, movedToPath string) result.Result[bool] {
	err := repository.WithTransaction(ctx, s.uow, func(tx *repository.Transaction) error {
		fileResult := tx.Files().GetByHash(ctx, hash)
		if fileResult.IsFailure() {
			return fileResult.Error()
		}

		file := fileResult.Value()

		if err := file.MarkAsMoved(movedToPath); err != nil {
			return err
		}

		updateResult := tx.Files().Update(ctx, file)
		return updateResult.Error()
	})

	if err != nil {
		return result.Failure[bool](err)
	}

	return result.Success(true)
}

// MarkAsError marks a file as having an error
func (s *fileService) MarkAsError(ctx context.Context, hash string, errorMessage string) result.Result[bool] {
	err := repository.WithTransaction(ctx, s.uow, func(tx *repository.Transaction) error {
		fileResult := tx.Files().GetByHash(ctx, hash)
		if fileResult.IsFailure() {
			return fileResult.Error()
		}

		file := fileResult.Value()
		file.MarkAsError(errorMessage)

		updateResult := tx.Files().Update(ctx, file)
		return updateResult.Error()
	})

	if err != nil {
		return result.Failure[bool](err)
	}

	return result.Success(true)
}

// MarkAsIgnored marks a file as ignored by the user
func (s *fileService) MarkAsIgnored(ctx context.Context, hash string, reason *string) result.Result[bool] {
	err := repository.WithTransaction(ctx, s.uow, func(tx *repository.Transaction) error {
		fileResult := tx.Files().GetByHash(ctx, hash)
		if fileResult.IsFailure() {
			return fileResult.Error()
		}

		file := fileResult.Value()

		if err := file.MarkAsIgnored(reason); err != nil {
			return err
		}

		updateResult := tx.Files().Update(ctx, file)
		return updateResult.Error()
	})

	if err != nil {
		return result.Failure[bool](err)
	}

	return result.Success(true)
}

// ExistsByHash checks if a file exists by hash
func (s *fileService) ExistsByHash(ctx context.Context, hash string) result.Result[bool] {
	return s.repo.ExistsByHash(ctx, hash)
}

// ExistsByPath checks if a file exists by original path
func (s *fileService) ExistsByPath(ctx context.Context, path string) result.Result[bool] {
	return s.repo.ExistsByOriginalPath(ctx, path)
}

// GetAllCategories retrieves all distinct categories
func (s *fileService) GetAllCategories(ctx context.Context) result.Result[[]string] {
	return s.repo.GetDistinctCategories(ctx)
}

// calculateFileHash computes the SHA256 hash of a file
func calculateFileHash(filePath string) (string, error) {
	file, err := os.Open(filePath)
	if err != nil {
		return "", fmt.Errorf("open file: %w", err)
	}
	defer file.Close()

	hash := sha256.New()
	if _, err := io.Copy(hash, file); err != nil {
		return "", fmt.Errorf("hash file: %w", err)
	}

	return fmt.Sprintf("%x", hash.Sum(nil)), nil
}
