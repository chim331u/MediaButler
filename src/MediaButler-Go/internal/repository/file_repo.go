package repository

import (
	"context"
	"database/sql"
	"fmt"
	"time"

	"github.com/chim331u/mediabutler-go/internal/db"
	"github.com/chim331u/mediabutler-go/internal/domain"
	"github.com/chim331u/mediabutler-go/pkg/result"
)

// FileRepository defines the interface for TrackedFile data access
type FileRepository interface {
	// Core CRUD operations
	GetByHash(ctx context.Context, hash string) result.Result[*domain.TrackedFile]
	GetByHashIncludeDeleted(ctx context.Context, hash string) result.Result[*domain.TrackedFile]
	Create(ctx context.Context, file *domain.TrackedFile) result.Result[bool]
	Update(ctx context.Context, file *domain.TrackedFile) result.Result[bool]
	SoftDelete(ctx context.Context, hash string, reason *string) result.Result[bool]
	Restore(ctx context.Context, hash string, reason *string) result.Result[bool]

	// Workflow queries
	GetFilesByStatus(ctx context.Context, status domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile]
	GetFilesByStatuses(ctx context.Context, statuses []domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile]
	GetFilesReadyForClassification(ctx context.Context, limit int) result.Result[[]domain.TrackedFile]
	GetFilesAwaitingConfirmation(ctx context.Context) result.Result[[]domain.TrackedFile]
	GetFilesReadyForMoving(ctx context.Context, limit int) result.Result[[]domain.TrackedFile]
	GetFilesWithErrors(ctx context.Context) result.Result[[]domain.TrackedFile]
	GetFilesReadyForRetry(ctx context.Context, limit int) result.Result[[]domain.TrackedFile]

	// Analytics
	GetProcessingStats(ctx context.Context) result.Result[map[domain.FileStatus]int64]
	GetDistinctCategories(ctx context.Context) result.Result[[]string]

	// Lookup
	ExistsByHash(ctx context.Context, hash string) result.Result[bool]
	ExistsByOriginalPath(ctx context.Context, path string) result.Result[bool]
}

// fileRepository implements FileRepository using SQLC generated queries
type fileRepository struct {
	queries *db.Queries
}

// NewFileRepository creates a new FileRepository instance
func NewFileRepository(dbConn *sql.DB) FileRepository {
	return &fileRepository{
		queries: db.New(dbConn),
	}
}

// NewFileRepositoryWithTx creates a repository using an existing transaction
func NewFileRepositoryWithTx(tx *sql.Tx) FileRepository {
	return &fileRepository{
		queries: db.New(tx),
	}
}

// GetByHash retrieves a tracked file by its hash (active records only)
func (r *fileRepository) GetByHash(ctx context.Context, hash string) result.Result[*domain.TrackedFile] {
	dbFile, err := r.queries.GetFileByHash(ctx, hash)
	if err != nil {
		if err == sql.ErrNoRows {
			return result.Failure[*domain.TrackedFile](fmt.Errorf("file not found: %s", hash))
		}
		return result.Failure[*domain.TrackedFile](fmt.Errorf("get file by hash: %w", err))
	}

	domainFile := r.toDomain(&dbFile)
	return result.Success(domainFile)
}

// GetByHashIncludeDeleted retrieves a file including soft-deleted records
func (r *fileRepository) GetByHashIncludeDeleted(ctx context.Context, hash string) result.Result[*domain.TrackedFile] {
	dbFile, err := r.queries.GetFileByHashIncludeDeleted(ctx, hash)
	if err != nil {
		if err == sql.ErrNoRows {
			return result.Failure[*domain.TrackedFile](fmt.Errorf("file not found: %s", hash))
		}
		return result.Failure[*domain.TrackedFile](fmt.Errorf("get file by hash (include deleted): %w", err))
	}

	domainFile := r.toDomain(&dbFile)
	return result.Success(domainFile)
}

// Create inserts a new tracked file
func (r *fileRepository) Create(ctx context.Context, file *domain.TrackedFile) result.Result[bool] {
	if err := file.Validate(); err != nil {
		return result.Failure[bool](fmt.Errorf("validation failed: %w", err))
	}

	err := r.queries.CreateFile(ctx, db.CreateFileParams{
		Hash:           file.Hash,
		FileName:       file.FileName,
		OriginalPath:   file.OriginalPath,
		FileSize:       file.FileSize,
		Status:         int64(file.Status),
		CreatedDate:    file.CreatedDate,
		LastUpdateDate: file.LastUpdateDate,
		IsActive:       file.IsActive,
	})

	if err != nil {
		return result.Failure[bool](fmt.Errorf("create file: %w", err))
	}

	return result.Success(true)
}

// Update updates an existing tracked file
func (r *fileRepository) Update(ctx context.Context, file *domain.TrackedFile) result.Result[bool] {
	if err := file.Validate(); err != nil {
		return result.Failure[bool](fmt.Errorf("validation failed: %w", err))
	}

	err := r.queries.UpdateFile(ctx, db.UpdateFileParams{
		FileName:          file.FileName,
		OriginalPath:      file.OriginalPath,
		FileSize:          file.FileSize,
		Status:            int64(file.Status),
		SuggestedCategory: file.SuggestedCategory,
		Confidence:        derefFloat64(file.Confidence, 0.0),
		Category:          file.Category,
		TargetPath:        file.TargetPath,
		ClassifiedAt:      file.ClassifiedAt,
		MovedAt:           file.MovedAt,
		LastError:         file.LastError,
		LastErrorAt:       file.LastErrorAt,
		RetryCount:        int64(file.RetryCount),
		LastUpdateDate:    file.LastUpdateDate,
		Note:              file.Note,
		Hash:              file.Hash,
	})

	if err != nil {
		return result.Failure[bool](fmt.Errorf("update file: %w", err))
	}

	return result.Success(true)
}

// SoftDelete marks a file as inactive
func (r *fileRepository) SoftDelete(ctx context.Context, hash string, reason *string) result.Result[bool] {
	err := r.queries.SoftDeleteFile(ctx, db.SoftDeleteFileParams{
		LastUpdateDate: domain.Now(),
		Note:           reason,
		Hash:           hash,
	})

	if err != nil {
		return result.Failure[bool](fmt.Errorf("soft delete file: %w", err))
	}

	return result.Success(true)
}

// Restore reactivates a soft-deleted file
func (r *fileRepository) Restore(ctx context.Context, hash string, reason *string) result.Result[bool] {
	err := r.queries.RestoreFile(ctx, db.RestoreFileParams{
		LastUpdateDate: domain.Now(),
		Note:           reason,
		Hash:           hash,
	})

	if err != nil {
		return result.Failure[bool](fmt.Errorf("restore file: %w", err))
	}

	return result.Success(true)
}

// GetFilesByStatus retrieves files by status with pagination
func (r *fileRepository) GetFilesByStatus(ctx context.Context, status domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile] {
	dbFiles, err := r.queries.GetFilesByStatus(ctx, db.GetFilesByStatusParams{
		Status: int64(status),
		Limit:  int64(limit),
		Offset: int64(offset),
	})

	if err != nil {
		return result.Failure[[]domain.TrackedFile](fmt.Errorf("get files by status: %w", err))
	}

	files := make([]domain.TrackedFile, len(dbFiles))
	for i, dbFile := range dbFiles {
		files[i] = *r.toDomain(&dbFile)
	}

	return result.Success(files)
}

// GetFilesByStatuses retrieves files by multiple statuses with pagination
func (r *fileRepository) GetFilesByStatuses(ctx context.Context, statuses []domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile] {
	// Convert domain statuses to int64 slice
	statusInts := make([]int64, len(statuses))
	for i, s := range statuses {
		statusInts[i] = int64(s)
	}

	dbFiles, err := r.queries.GetFilesByStatuses(ctx, db.GetFilesByStatusesParams{
		Statuses: statusInts,
		Limit:    int64(limit),
		Offset:   int64(offset),
	})

	if err != nil {
		return result.Failure[[]domain.TrackedFile](fmt.Errorf("get files by statuses: %w", err))
	}

	return result.Success(r.toDomainSlice(dbFiles))
}

// GetFilesReadyForClassification retrieves files ready for ML classification
func (r *fileRepository) GetFilesReadyForClassification(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	dbFiles, err := r.queries.GetFilesReadyForClassification(ctx, int64(limit))
	if err != nil {
		return result.Failure[[]domain.TrackedFile](fmt.Errorf("get files ready for classification: %w", err))
	}

	return result.Success(r.toDomainSlice(dbFiles))
}

// GetFilesAwaitingConfirmation retrieves classified files awaiting user confirmation
func (r *fileRepository) GetFilesAwaitingConfirmation(ctx context.Context) result.Result[[]domain.TrackedFile] {
	dbFiles, err := r.queries.GetFilesAwaitingConfirmation(ctx)
	if err != nil {
		return result.Failure[[]domain.TrackedFile](fmt.Errorf("get files awaiting confirmation: %w", err))
	}

	return result.Success(r.toDomainSlice(dbFiles))
}

// GetFilesReadyForMoving retrieves files ready to be moved
func (r *fileRepository) GetFilesReadyForMoving(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	dbFiles, err := r.queries.GetFilesReadyForMoving(ctx, int64(limit))
	if err != nil {
		return result.Failure[[]domain.TrackedFile](fmt.Errorf("get files ready for moving: %w", err))
	}

	return result.Success(r.toDomainSlice(dbFiles))
}

// GetFilesWithErrors retrieves files in error state
func (r *fileRepository) GetFilesWithErrors(ctx context.Context) result.Result[[]domain.TrackedFile] {
	dbFiles, err := r.queries.GetFilesWithErrors(ctx)
	if err != nil {
		return result.Failure[[]domain.TrackedFile](fmt.Errorf("get files with errors: %w", err))
	}

	return result.Success(r.toDomainSlice(dbFiles))
}

// GetFilesReadyForRetry retrieves files ready for retry after error
func (r *fileRepository) GetFilesReadyForRetry(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	dbFiles, err := r.queries.GetFilesReadyForRetry(ctx, int64(limit))
	if err != nil {
		return result.Failure[[]domain.TrackedFile](fmt.Errorf("get files ready for retry: %w", err))
	}

	return result.Success(r.toDomainSlice(dbFiles))
}

// GetProcessingStats retrieves file counts grouped by status
func (r *fileRepository) GetProcessingStats(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
	stats, err := r.queries.GetProcessingStats(ctx)
	if err != nil {
		return result.Failure[map[domain.FileStatus]int64](fmt.Errorf("get processing stats: %w", err))
	}

	statsMap := make(map[domain.FileStatus]int64)
	for _, stat := range stats {
		status := domain.FileStatus(stat.Status)
		statsMap[status] = stat.Count
	}

	return result.Success(statsMap)
}

// GetDistinctCategories retrieves all unique categories
func (r *fileRepository) GetDistinctCategories(ctx context.Context) result.Result[[]string] {
	categories, err := r.queries.GetDistinctCategories(ctx)
	if err != nil {
		return result.Failure[[]string](fmt.Errorf("get distinct categories: %w", err))
	}

	// Convert []*string to []string, filtering out nulls
	categoriesResult := make([]string, 0, len(categories))
	for _, cat := range categories {
		if cat != nil {
			categoriesResult = append(categoriesResult, *cat)
		}
	}

	return result.Success(categoriesResult)
}

// ExistsByHash checks if a file exists by hash
func (r *fileRepository) ExistsByHash(ctx context.Context, hash string) result.Result[bool] {
	exists, err := r.queries.ExistsByHash(ctx, hash)
	if err != nil {
		return result.Failure[bool](fmt.Errorf("exists by hash: %w", err))
	}

	return result.Success(exists)
}

// ExistsByOriginalPath checks if a file exists by original path
func (r *fileRepository) ExistsByOriginalPath(ctx context.Context, path string) result.Result[bool] {
	exists, err := r.queries.ExistsByOriginalPath(ctx, path)
	if err != nil {
		return result.Failure[bool](fmt.Errorf("exists by original path: %w", err))
	}

	return result.Success(exists)
}

// toDomain converts database model to domain model
func (r *fileRepository) toDomain(dbFile *db.Trackedfile) *domain.TrackedFile {
	return &domain.TrackedFile{
		BaseEntity: domain.BaseEntity{
			ID:             0, // SQLite doesn't have ID for TrackedFiles (Hash is PK)
			CreatedDate:    dbFile.CreatedDate,
			LastUpdateDate: dbFile.LastUpdateDate,
			IsActive:       dbFile.IsActive,
			Note:           dbFile.Note,
		},
		Hash:              dbFile.Hash,
		FileName:          dbFile.FileName,
		OriginalPath:      dbFile.OriginalPath,
		FileSize:          dbFile.FileSize,
		Status:            domain.FileStatus(dbFile.Status),
		SuggestedCategory: dbFile.SuggestedCategory,
		Confidence:        toFloat64Ptr(dbFile.Confidence),
		ClassifiedAt:      dbFile.ClassifiedAt,
		Category:          dbFile.Category,
		TargetPath:        dbFile.TargetPath,
		MovedToPath:       dbFile.MovedToPath,
		MovedAt:           dbFile.MovedAt,
		LastError:         dbFile.LastError,
		LastErrorAt:       dbFile.LastErrorAt,
		RetryCount:        int(dbFile.RetryCount),
	}
}

// toDomainSlice converts a slice of database models to domain models
func (r *fileRepository) toDomainSlice(dbFiles []db.Trackedfile) []domain.TrackedFile {
	files := make([]domain.TrackedFile, len(dbFiles))
	for i, dbFile := range dbFiles {
		files[i] = *r.toDomain(&dbFile)
	}
	return files
}

// Helper functions for null handling

func toNullString(s *string) sql.NullString {
	if s == nil {
		return sql.NullString{Valid: false}
	}
	return sql.NullString{String: *s, Valid: true}
}

func fromNullString(ns sql.NullString) *string {
	if !ns.Valid {
		return nil
	}
	return &ns.String
}

func toNullTime(t *time.Time) sql.NullTime {
	if t == nil {
		return sql.NullTime{Valid: false}
	}
	return sql.NullTime{Time: *t, Valid: true}
}

func fromNullTime(nt sql.NullTime) *time.Time {
	if !nt.Valid {
		return nil
	}
	return &nt.Time
}

func toFloat64Ptr(f float64) *float64 {
	if f == 0.0 {
		return nil
	}
	return &f
}

func derefFloat64(f *float64, defaultValue float64) float64 {
	if f == nil {
		return defaultValue
	}
	return *f
}

func boolToInt(b bool) int64 {
	if b {
		return 1
	}
	return 0
}

func int64ToBool(i int64) bool {
	return i != 0
}
