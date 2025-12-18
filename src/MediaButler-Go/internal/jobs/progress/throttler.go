package progress

import (
	"context"
	"runtime"
	"time"

	"github.com/rs/zerolog"
)

// BatchThrottler interface for controlling batch processing rate
type BatchThrottler interface {
	ShouldThrottle(processed, total int) bool
	Throttle(ctx context.Context) error
	GetMemoryUsage() uint64
}

// ThrottlerConfig holds throttling configuration
type ThrottlerConfig struct {
	EnableThrottling      bool          // Enable/disable throttling
	DelaySmall            time.Duration // Delay for small batches (11-50 files)
	DelayMedium           time.Duration // Delay for medium batches (51-200 files)
	DelayLarge            time.Duration // Delay for large batches (201-1000 files)
	MemoryThresholdMB     uint64        // Memory threshold for additional throttling
	MemoryCheckInterval   int           // Check memory every N files
	AggressiveGCThreshold uint64        // Trigger GC above this threshold
}

// DefaultThrottlerConfig returns default configuration optimized for ARM32
func DefaultThrottlerConfig() ThrottlerConfig {
	return ThrottlerConfig{
		EnableThrottling:      true,
		DelaySmall:            100 * time.Millisecond,  // 100ms for small batches
		DelayMedium:           200 * time.Millisecond,  // 200ms for medium batches
		DelayLarge:            500 * time.Millisecond,  // 500ms for large batches
		MemoryThresholdMB:     250,                     // 250MB threshold
		MemoryCheckInterval:   5,                       // Every 5 files
		AggressiveGCThreshold: 280,                     // 280MB triggers GC
	}
}

// batchThrottler implements BatchThrottler
type batchThrottler struct {
	config ThrottlerConfig
	logger zerolog.Logger
}

// NewBatchThrottler creates a new batch throttler
func NewBatchThrottler(config ThrottlerConfig, logger zerolog.Logger) BatchThrottler {
	return &batchThrottler{
		config: config,
		logger: logger.With().Str("component", "batch-throttler").Logger(),
	}
}

// ShouldThrottle determines if throttling should be applied
func (t *batchThrottler) ShouldThrottle(processed, total int) bool {
	if !t.config.EnableThrottling {
		return false
	}

	// No throttling for first 10 files (fast startup)
	if processed <= 10 {
		return false
	}

	// Always throttle for large batches
	if total > 200 {
		return true
	}

	// Throttle for medium batches after 10 files
	if total > 50 {
		return true
	}

	// Throttle for small batches after 10 files
	if total > 10 {
		return true
	}

	return false
}

// Throttle applies the configured delay and checks memory
func (t *batchThrottler) Throttle(ctx context.Context) error {
	if !t.config.EnableThrottling {
		return nil
	}

	// Get current memory usage
	memUsageMB := t.GetMemoryUsage()

	// Determine base delay based on batch size
	delay := t.config.DelaySmall

	// Check for aggressive GC threshold
	if memUsageMB > t.config.AggressiveGCThreshold {
		t.logger.Warn().
			Uint64("memory_mb", memUsageMB).
			Uint64("threshold_mb", t.config.AggressiveGCThreshold).
			Msg("Memory usage exceeds aggressive GC threshold, forcing GC")

		// Force GC
		runtime.GC()

		// Apply additional delay
		delay = 2 * time.Second

		t.logger.Info().
			Uint64("memory_after_gc_mb", t.GetMemoryUsage()).
			Msg("GC completed")
	} else if memUsageMB > t.config.MemoryThresholdMB {
		// Memory above threshold but below aggressive threshold
		t.logger.Debug().
			Uint64("memory_mb", memUsageMB).
			Uint64("threshold_mb", t.config.MemoryThresholdMB).
			Msg("Memory usage above threshold, applying additional delay")

		// Apply additional delay
		delay = 1 * time.Second
	}

	// Apply delay
	select {
	case <-time.After(delay):
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

// GetMemoryUsage returns current memory usage in MB
func (t *batchThrottler) GetMemoryUsage() uint64 {
	var m runtime.MemStats
	runtime.ReadMemStats(&m)

	// Return allocated memory in MB
	return m.Alloc / 1024 / 1024
}

// NoOpThrottler is a no-op implementation that never throttles
type NoOpThrottler struct{}

func (n *NoOpThrottler) ShouldThrottle(processed, total int) bool {
	return false
}

func (n *NoOpThrottler) Throttle(ctx context.Context) error {
	return nil
}

func (n *NoOpThrottler) GetMemoryUsage() uint64 {
	var m runtime.MemStats
	runtime.ReadMemStats(&m)
	return m.Alloc / 1024 / 1024
}
