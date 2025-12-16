package service

import (
	"context"
	"errors"
	"testing"

	"github.com/lucapaganotti/mediabutler-go/internal/domain"
	"github.com/lucapaganotti/mediabutler-go/pkg/result"
)

// mockFileRepositoryForStats extends mockFileRepository with GetProcessingStats
type mockFileRepositoryForStats struct {
	mockFileRepository
	getProcessingStatsFunc func(ctx context.Context) result.Result[map[domain.FileStatus]int64]
}

func (m *mockFileRepositoryForStats) GetProcessingStats(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
	if m.getProcessingStatsFunc != nil {
		return m.getProcessingStatsFunc(ctx)
	}
	return result.Success(map[domain.FileStatus]int64{})
}

// Test GetProcessingStats with typical data
func TestStatsService_GetProcessingStats_Success(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{
		getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
			return result.Success(map[domain.FileStatus]int64{
				domain.FileStatusNew:         15,
				domain.FileStatusClassified:  25,
				domain.FileStatusReadyToMove: 10,
				domain.FileStatusMoved:       100,
				domain.FileStatusError:       5,
				domain.FileStatusIgnored:     3,
			})
		},
	}

	svc := NewStatsService(mockRepo)

	res := svc.GetProcessingStats(context.Background())

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	stats := res.Value()

	// Verify individual stats
	if stats.Pending != 15 {
		t.Errorf("expected Pending=15, got %d", stats.Pending)
	}

	if stats.Classified != 25 {
		t.Errorf("expected Classified=25, got %d", stats.Classified)
	}

	if stats.ReadyToMove != 10 {
		t.Errorf("expected ReadyToMove=10, got %d", stats.ReadyToMove)
	}

	if stats.Moved != 100 {
		t.Errorf("expected Moved=100, got %d", stats.Moved)
	}

	if stats.Failed != 5 {
		t.Errorf("expected Failed=5, got %d", stats.Failed)
	}

	if stats.Ignored != 3 {
		t.Errorf("expected Ignored=3, got %d", stats.Ignored)
	}

	// Verify total
	expectedTotal := int64(15 + 25 + 10 + 100 + 5 + 3)
	if stats.Total != expectedTotal {
		t.Errorf("expected Total=%d, got %d", expectedTotal, stats.Total)
	}
}

// Test GetProcessingStats with empty data
func TestStatsService_GetProcessingStats_EmptyData(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{
		getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
			return result.Success(map[domain.FileStatus]int64{})
		},
	}

	svc := NewStatsService(mockRepo)

	res := svc.GetProcessingStats(context.Background())

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	stats := res.Value()

	// All stats should be zero
	if stats.Total != 0 {
		t.Errorf("expected Total=0, got %d", stats.Total)
	}

	if stats.Pending != 0 {
		t.Errorf("expected Pending=0, got %d", stats.Pending)
	}

	if stats.Moved != 0 {
		t.Errorf("expected Moved=0, got %d", stats.Moved)
	}
}

// Test GetProcessingStats with repository error
func TestStatsService_GetProcessingStats_RepositoryError(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{
		getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
			return result.Failure[map[domain.FileStatus]int64](errors.New("database error"))
		},
	}

	svc := NewStatsService(mockRepo)

	res := svc.GetProcessingStats(context.Background())

	if res.IsSuccess() {
		t.Error("expected failure for repository error, got success")
	}
}

// Test GetProcessingStats with large numbers
func TestStatsService_GetProcessingStats_LargeNumbers(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{
		getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
			return result.Success(map[domain.FileStatus]int64{
				domain.FileStatusNew:         10000,
				domain.FileStatusClassified:  25000,
				domain.FileStatusReadyToMove: 15000,
				domain.FileStatusMoved:       1000000,
				domain.FileStatusError:       500,
				domain.FileStatusIgnored:     100,
			})
		},
	}

	svc := NewStatsService(mockRepo)

	res := svc.GetProcessingStats(context.Background())

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	stats := res.Value()

	expectedTotal := int64(10000 + 25000 + 15000 + 1000000 + 500 + 100)
	if stats.Total != expectedTotal {
		t.Errorf("expected Total=%d, got %d", expectedTotal, stats.Total)
	}

	if stats.Moved != 1000000 {
		t.Errorf("expected Moved=1000000, got %d", stats.Moved)
	}
}

// Test GetProcessingStats with only one status having data
func TestStatsService_GetProcessingStats_SingleStatusData(t *testing.T) {
	testCases := []struct {
		name         string
		status       domain.FileStatus
		count        int64
		expectedField string
	}{
		{"Only New files", domain.FileStatusNew, 50, "Pending"},
		{"Only Classified files", domain.FileStatusClassified, 30, "Classified"},
		{"Only ReadyToMove files", domain.FileStatusReadyToMove, 20, "ReadyToMove"},
		{"Only Moved files", domain.FileStatusMoved, 100, "Moved"},
		{"Only Error files", domain.FileStatusError, 10, "Failed"},
		{"Only Ignored files", domain.FileStatusIgnored, 5, "Ignored"},
	}

	for _, tc := range testCases {
		t.Run(tc.name, func(t *testing.T) {
			mockRepo := &mockFileRepositoryForStats{
				getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
					return result.Success(map[domain.FileStatus]int64{
						tc.status: tc.count,
					})
				},
			}

			svc := NewStatsService(mockRepo)

			res := svc.GetProcessingStats(context.Background())

			if res.IsFailure() {
				t.Errorf("expected success, got failure: %v", res.Error())
			}

			stats := res.Value()

			if stats.Total != tc.count {
				t.Errorf("expected Total=%d, got %d", tc.count, stats.Total)
			}

			// Verify the specific field has the expected value
			switch tc.expectedField {
			case "Pending":
				if stats.Pending != tc.count {
					t.Errorf("expected Pending=%d, got %d", tc.count, stats.Pending)
				}
			case "Classified":
				if stats.Classified != tc.count {
					t.Errorf("expected Classified=%d, got %d", tc.count, stats.Classified)
				}
			case "ReadyToMove":
				if stats.ReadyToMove != tc.count {
					t.Errorf("expected ReadyToMove=%d, got %d", tc.count, stats.ReadyToMove)
				}
			case "Moved":
				if stats.Moved != tc.count {
					t.Errorf("expected Moved=%d, got %d", tc.count, stats.Moved)
				}
			case "Failed":
				if stats.Failed != tc.count {
					t.Errorf("expected Failed=%d, got %d", tc.count, stats.Failed)
				}
			case "Ignored":
				if stats.Ignored != tc.count {
					t.Errorf("expected Ignored=%d, got %d", tc.count, stats.Ignored)
				}
			}
		})
	}
}

// Test GetProcessingStats verifies InReview is mapped from Classified
func TestStatsService_GetProcessingStats_InReviewMapping(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{
		getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
			return result.Success(map[domain.FileStatus]int64{
				domain.FileStatusClassified: 42,
			})
		},
	}

	svc := NewStatsService(mockRepo)

	res := svc.GetProcessingStats(context.Background())

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	stats := res.Value()

	// InReview should equal Classified (as per implementation)
	if stats.InReview != 42 {
		t.Errorf("expected InReview=42 (mapped from Classified), got %d", stats.InReview)
	}

	if stats.Classified != 42 {
		t.Errorf("expected Classified=42, got %d", stats.Classified)
	}
}

// Test GetProcessingStats with context cancellation
func TestStatsService_GetProcessingStats_ContextCancellation(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{
		getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
			// Check if context is cancelled
			select {
			case <-ctx.Done():
				return result.Failure[map[domain.FileStatus]int64](ctx.Err())
			default:
				return result.Success(map[domain.FileStatus]int64{})
			}
		},
	}

	svc := NewStatsService(mockRepo)

	// Create cancelled context
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	res := svc.GetProcessingStats(ctx)

	if res.IsSuccess() {
		t.Error("expected failure for cancelled context, got success")
	}
}

// Test GetProcessingStats with all statuses at zero
func TestStatsService_GetProcessingStats_AllZeros(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{
		getProcessingStatsFunc: func(ctx context.Context) result.Result[map[domain.FileStatus]int64] {
			return result.Success(map[domain.FileStatus]int64{
				domain.FileStatusNew:         0,
				domain.FileStatusClassified:  0,
				domain.FileStatusReadyToMove: 0,
				domain.FileStatusMoved:       0,
				domain.FileStatusError:       0,
				domain.FileStatusIgnored:     0,
			})
		},
	}

	svc := NewStatsService(mockRepo)

	res := svc.GetProcessingStats(context.Background())

	if res.IsFailure() {
		t.Errorf("expected success, got failure: %v", res.Error())
	}

	stats := res.Value()

	if stats.Total != 0 {
		t.Errorf("expected Total=0, got %d", stats.Total)
	}

	if stats.Pending != 0 {
		t.Errorf("expected Pending=0, got %d", stats.Pending)
	}
}

// Test NewStatsService creates valid instance
func TestNewStatsService_CreatesValidInstance(t *testing.T) {
	mockRepo := &mockFileRepositoryForStats{}

	svc := NewStatsService(mockRepo)

	if svc == nil {
		t.Error("expected non-nil service")
	}
}
