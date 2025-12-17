package service

import (
	"context"
	"errors"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/internal/repository"
	"github.com/lucapaganotti/mediabutler-go/pkg/pagination"
	"github.com/lucapaganotti/mediabutler-go/pkg/result"
)

// mockFileRepository is a mock implementation of FileRepository for testing
type mockFileRepository struct {
	getByHashFunc                      func(ctx context.Context, hash string) result.Result[*domain.TrackedFile]
	getFilesByStatusFunc               func(ctx context.Context, status domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile]
	getFilesAwaitingConfirmationFunc   func(ctx context.Context) result.Result[[]domain.TrackedFile]
	getFilesReadyForClassificationFunc func(ctx context.Context, limit int) result.Result[[]domain.TrackedFile]
	getFilesReadyForMovingFunc         func(ctx context.Context, limit int) result.Result[[]domain.TrackedFile]
	createFunc                         func(ctx context.Context, file *domain.TrackedFile) result.Result[bool]
	updateFunc                         func(ctx context.Context, file *domain.TrackedFile) result.Result[bool]
	existsByHashFunc                   func(ctx context.Context, hash string) result.Result[bool]
	existsByOriginalPathFunc           func(ctx context.Context, path string) result.Result[bool]
	getByHashIncludeDeletedFunc        func(ctx context.Context, hash string) result.Result[*domain.TrackedFile]
	getFilesByStatusesFunc             func(ctx context.Context, statuses []domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile]
	getDistinctCategoriesFunc          func(ctx context.Context) result.Result[[]string]
}

func (m *mockFileRepository) GetByHashIncludeDeleted(ctx context.Context, hash string) result.Result[*domain.TrackedFile] {
	if m.getByHashIncludeDeletedFunc != nil {
		return m.getByHashIncludeDeletedFunc(ctx, hash)
	}
	return result.Failure[*domain.TrackedFile](errors.New("not implemented"))
}

func (m *mockFileRepository) GetFilesByStatuses(ctx context.Context, statuses []domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile] {
	if m.getFilesByStatusesFunc != nil {
		return m.getFilesByStatusesFunc(ctx, statuses, limit, offset)
	}
	return result.Success([]domain.TrackedFile{})
}

func (m *mockFileRepository) GetByHash(ctx context.Context, hash string) result.Result[*domain.TrackedFile] {
	if m.getByHashFunc != nil {
		return m.getByHashFunc(ctx, hash)
	}
	return result.Failure[*domain.TrackedFile](errors.New("not implemented"))
}

func (m *mockFileRepository) GetFilesByStatus(ctx context.Context, status domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile] {
	if m.getFilesByStatusFunc != nil {
		return m.getFilesByStatusFunc(ctx, status, limit, offset)
	}
	return result.Success([]domain.TrackedFile{})
}

func (m *mockFileRepository) GetFilesAwaitingConfirmation(ctx context.Context) result.Result[[]domain.TrackedFile] {
	if m.getFilesAwaitingConfirmationFunc != nil {
		return m.getFilesAwaitingConfirmationFunc(ctx)
	}
	return result.Success([]domain.TrackedFile{})
}

func (m *mockFileRepository) GetFilesReadyForClassification(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	if m.getFilesReadyForClassificationFunc != nil {
		return m.getFilesReadyForClassificationFunc(ctx, limit)
	}
	return result.Success([]domain.TrackedFile{})
}

func (m *mockFileRepository) GetFilesReadyForMoving(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	if m.getFilesReadyForMovingFunc != nil {
		return m.getFilesReadyForMovingFunc(ctx, limit)
	}
	return result.Success([]domain.TrackedFile{})
}

func (m *mockFileRepository) Create(ctx context.Context, file *domain.TrackedFile) result.Result[bool] {
	if m.createFunc != nil {
		return m.createFunc(ctx, file)
	}
	return result.Success(true)
}

func (m *mockFileRepository) Update(ctx context.Context, file *domain.TrackedFile) result.Result[bool] {
	if m.updateFunc != nil {
		return m.updateFunc(ctx, file)
	}
	return result.Success(true)
}

func (m *mockFileRepository) ExistsByHash(ctx context.Context, hash string) result.Result[bool] {
	if m.existsByHashFunc != nil {
		return m.existsByHashFunc(ctx, hash)
	}
	return result.Success(false)
}

func (m *mockFileRepository) ExistsByOriginalPath(ctx context.Context, path string) result.Result[bool] {
	if m.existsByOriginalPathFunc != nil {
		return m.existsByOriginalPathFunc(ctx, path)
	}
	return result.Success(false)
}

func (m *mockFileRepository) GetDistinctCategories(ctx context.Context) result.Result[[]string] {
	if m.getDistinctCategoriesFunc != nil {
		return m.getDistinctCategoriesFunc(ctx)
	}
	return result.Success([]string{"SERIES1", "SERIES2"})
}

func (m *mockFileRepository) GetFilesWithErrors(ctx context.Context) result.Result[[]domain.TrackedFile] {
	return result.Success([]domain.TrackedFile{})
}

func (m *mockFileRepository) GetFilesReadyForRetry(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
	return result.Success([]domain.TrackedFile{})
}

func (m *mockFileRepository) SoftDelete(ctx context.Context, hash string, reason *string) result.Result[bool] {
	return result.Success(true)
}

func (m *mockFileRepository) Restore(ctx context.Context, hash string, reason *string) result.Result[bool] {
	return result.Success(true)
}

func (m *mockFileRepository) GetProcessingStats(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
	return result.Success(map[domain.FileStatus]int64{})
}

// mockTransaction is a mock implementation of Transaction for testing
type mockTransaction struct {
	filesRepo *mockFileRepository
	commitErr error
}

func (m *mockTransaction) Files() repository.FileRepository {
	return m.filesRepo
}

func (m *mockTransaction) Commit() error {
	return m.commitErr
}

func (m *mockTransaction) Rollback() error {
	return nil
}

// mockUnitOfWork is a mock implementation of UnitOfWork for testing
type mockUnitOfWork struct {
	transaction *mockTransaction
	beginErr    error
}

func (m *mockUnitOfWork) Begin(ctx context.Context) (*repository.Transaction, error) {
	if m.beginErr != nil {
		return nil, m.beginErr
	}
	return &repository.Transaction{}, nil
}

// Helper to create mock transaction that works with WithTransaction
func newMockUnitOfWork(filesRepo *mockFileRepository) repository.UnitOfWork {
	return &mockUnitOfWork{
		transaction: &mockTransaction{
			filesRepo: filesRepo,
		},
	}
}

// Test RegisterFileWithHash
func TestFileService_RegisterFileWithHash_Success(t *testing.T) {
	mockRepo := &mockFileRepository{
		existsByHashFunc: func(ctx context.Context, hash string) result.Result[bool] {
			return result.Success(false)
		},
		createFunc: func(ctx context.Context, file *domain.TrackedFile) result.Result[bool] {
			return result.Success(true)
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.RegisterFileWithHash(context.Background(), "/path/to/file.mkv", strings.Repeat("a", 64), 1024000)

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	file := res.Value()
	if file.FileName != "file.mkv" {
		t.Errorf("expected fileName 'file.mkv', got '%s'", file.FileName)
	}
}

func TestFileService_RegisterFileWithHash_AlreadyExists(t *testing.T) {
	mockRepo := &mockFileRepository{
		existsByHashFunc: func(ctx context.Context, hash string) result.Result[bool] {
			return result.Success(true)
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.RegisterFileWithHash(context.Background(), "/path/to/file.mkv", strings.Repeat("a", 64), 1024000)

	if res.IsSuccess() {
		t.Error("expected failure for duplicate file, got success")
	}
}

// Test GetFileByHash
func TestFileService_GetFileByHash_Success(t *testing.T) {
	expectedFile := domain.NewTrackedFile(strings.Repeat("a", 64), "test.mkv", "/path/test.mkv", 1024)

	mockRepo := &mockFileRepository{
		getByHashFunc: func(ctx context.Context, hash string) result.Result[*domain.TrackedFile] {
			return result.Success(expectedFile)
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.GetFileByHash(context.Background(), strings.Repeat("a", 64))

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	file := res.Value()
	if file.FileName != "test.mkv" {
		t.Errorf("expected fileName 'test.mkv', got '%s'", file.FileName)
	}
}

func TestFileService_GetFileByHash_InvalidHashLength(t *testing.T) {
	mockRepo := &mockFileRepository{}
	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.GetFileByHash(context.Background(), "short")

	if res.IsSuccess() {
		t.Error("expected failure for invalid hash length, got success")
	}
}

// Test GetFilesByStatus
func TestFileService_GetFilesByStatus_Success(t *testing.T) {
	file1 := domain.NewTrackedFile(strings.Repeat("a", 64), "file1.mkv", "/path/file1.mkv", 1024)
	file2 := domain.NewTrackedFile(strings.Repeat("b", 64), "file2.mkv", "/path/file2.mkv", 2048)

	mockRepo := &mockFileRepository{
		getFilesByStatusFunc: func(ctx context.Context, status domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile] {
			return result.Success([]domain.TrackedFile{*file1, *file2})
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	pageReq := &pagination.Request{Skip: 0, Take: 10}
	res := svc.GetFilesByStatus(context.Background(), domain.FileStatusNew, pageReq)

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	response := res.Value()
	if len(response.Items) != 2 {
		t.Errorf("expected 2 files, got %d", len(response.Items))
	}
}

func TestFileService_GetFilesByStatus_NilPagination(t *testing.T) {
	mockRepo := &mockFileRepository{
		getFilesByStatusFunc: func(ctx context.Context, status domain.FileStatus, limit, offset int) result.Result[[]domain.TrackedFile] {
			if limit != 20 || offset != 0 {
				t.Errorf("expected default pagination (limit=20, offset=0), got limit=%d, offset=%d", limit, offset)
			}
			return result.Success([]domain.TrackedFile{})
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.GetFilesByStatus(context.Background(), domain.FileStatusNew, nil)

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}
}

// Test GetReadyForClassification
func TestFileService_GetReadyForClassification_ValidLimit(t *testing.T) {
	mockRepo := &mockFileRepository{
		getFilesReadyForClassificationFunc: func(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
			if limit != 25 {
				t.Errorf("expected limit 25, got %d", limit)
			}
			return result.Success([]domain.TrackedFile{})
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.GetReadyForClassification(context.Background(), 25)

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}
}

func TestFileService_GetReadyForClassification_InvalidLimit(t *testing.T) {
	mockRepo := &mockFileRepository{
		getFilesReadyForClassificationFunc: func(ctx context.Context, limit int) result.Result[[]domain.TrackedFile] {
			if limit != 50 {
				t.Errorf("expected default limit 50, got %d", limit)
			}
			return result.Success([]domain.TrackedFile{})
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	// Test negative limit
	res := svc.GetReadyForClassification(context.Background(), -1)
	if res.IsFailure() {
		t.Errorf("expected success with default limit, got failure: %v", res.Error())
	}

	// Test limit too large
	res = svc.GetReadyForClassification(context.Background(), 200)
	if res.IsFailure() {
		t.Errorf("expected success with default limit, got failure: %v", res.Error())
	}
}

// Test ExistsByHash
func TestFileService_ExistsByHash_Exists(t *testing.T) {
	mockRepo := &mockFileRepository{
		existsByHashFunc: func(ctx context.Context, hash string) result.Result[bool] {
			return result.Success(true)
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.ExistsByHash(context.Background(), "somehash")

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	if !res.Value() {
		t.Error("expected file to exist")
	}
}

func TestFileService_ExistsByHash_NotExists(t *testing.T) {
	mockRepo := &mockFileRepository{
		existsByHashFunc: func(ctx context.Context, hash string) result.Result[bool] {
			return result.Success(false)
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.ExistsByHash(context.Background(), "somehash")

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	if res.Value() {
		t.Error("expected file to not exist")
	}
}

// Test GetAllCategories
func TestFileService_GetAllCategories_Success(t *testing.T) {
	expectedCategories := []string{"BREAKING BAD", "THE OFFICE", "GAME OF THRONES"}

	mockRepo := &mockFileRepository{
		getDistinctCategoriesFunc: func(ctx context.Context) result.Result[[]string] {
			return result.Success(expectedCategories)
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.GetAllCategories(context.Background())

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	categories := res.Value()
	if len(categories) != 3 {
		t.Errorf("expected 3 categories, got %d", len(categories))
	}
}

// Test calculateFileHash helper function
func TestCalculateFileHash_Success(t *testing.T) {
	// Create a temporary test file
	tmpDir := t.TempDir()
	tmpFile := filepath.Join(tmpDir, "test.txt")

	content := []byte("test content for hashing")
	if err := os.WriteFile(tmpFile, content, 0644); err != nil {
		t.Fatalf("failed to create test file: %v", err)
	}

	hash, err := calculateFileHash(tmpFile)
	if err != nil {
		t.Errorf("expected no error, got: %v", err)
	}

	if len(hash) != 64 {
		t.Errorf("expected hash length 64, got %d", len(hash))
	}

	// Verify hash is deterministic
	hash2, err := calculateFileHash(tmpFile)
	if err != nil {
		t.Errorf("expected no error on second hash, got: %v", err)
	}

	if hash != hash2 {
		t.Error("hash should be deterministic")
	}
}

func TestCalculateFileHash_FileNotFound(t *testing.T) {
	_, err := calculateFileHash("/nonexistent/file.txt")
	if err == nil {
		t.Error("expected error for nonexistent file")
	}
}

// Test RegisterFile (with actual file)
func TestFileService_RegisterFile_Success(t *testing.T) {
	// Create a temporary test file
	tmpDir := t.TempDir()
	tmpFile := filepath.Join(tmpDir, "test.mkv")

	content := []byte("test video content")
	if err := os.WriteFile(tmpFile, content, 0644); err != nil {
		t.Fatalf("failed to create test file: %v", err)
	}

	mockRepo := &mockFileRepository{
		existsByHashFunc: func(ctx context.Context, hash string) result.Result[bool] {
			return result.Success(false)
		},
		createFunc: func(ctx context.Context, file *domain.TrackedFile) result.Result[bool] {
			return result.Success(true)
		},
	}

	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.RegisterFile(context.Background(), tmpFile)

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	file := res.Value()
	if file.FileName != "test.mkv" {
		t.Errorf("expected fileName 'test.mkv', got '%s'", file.FileName)
	}

	if len(file.Hash) != 64 {
		t.Errorf("expected hash length 64, got %d", len(file.Hash))
	}
}

func TestFileService_RegisterFile_FileNotFound(t *testing.T) {
	mockRepo := &mockFileRepository{}
	mockUow := newMockUnitOfWork(mockRepo)
	svc := NewFileService(mockRepo, mockUow)

	res := svc.RegisterFile(context.Background(), "/nonexistent/file.mkv")

	if res.IsSuccess() {
		t.Error("expected failure for nonexistent file, got success")
	}
}
