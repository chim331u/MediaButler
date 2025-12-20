package service

import (
	"context"

	"github.com/chim331u/mediabutler-go/internal/domain"
	"github.com/chim331u/mediabutler-go/internal/repository"
	"github.com/chim331u/mediabutler-go/pkg/result"
)

// StatsService defines business logic for statistics
type StatsService interface {
	GetProcessingStats(ctx context.Context) result.Result[domain.ProcessingStats]
}

// statsService implements StatsService
type statsService struct {
	repo repository.FileRepository
}

// NewStatsService creates a new StatsService instance
func NewStatsService(repo repository.FileRepository) StatsService {
	return &statsService{
		repo: repo,
	}
}

// GetProcessingStats retrieves current processing statistics
func (s *statsService) GetProcessingStats(ctx context.Context) result.Result[domain.ProcessingStats] {
	// Get raw stats from repository
	statsResult := s.repo.GetProcessingStats(ctx)
	if statsResult.IsFailure() {
		return result.Failure[domain.ProcessingStats](statsResult.Error())
	}

	rawStats := statsResult.Value()

	// Aggregate into domain model
	stats := domain.ProcessingStats{
		Total:       0,
		Pending:     rawStats[domain.FileStatusNew], // Mapping New to Pending for UI
		Classified:  rawStats[domain.FileStatusClassified],
		InReview:    rawStats[domain.FileStatusClassified], // Reusing Classified as InReview conceptually
		ReadyToMove: rawStats[domain.FileStatusReadyToMove],
		Moved:       rawStats[domain.FileStatusMoved],
		Failed:      rawStats[domain.FileStatusError],
		Ignored:     rawStats[domain.FileStatusIgnored],
	}

	// Calculate total (excluding ignored if desired, but usually total tracked)
	for _, count := range rawStats {
		stats.Total += count
	}

	return result.Success(stats)
}
