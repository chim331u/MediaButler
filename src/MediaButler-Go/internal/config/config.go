// Package config provides configuration management using Viper
// Mirrors the appsettings.json structure from the .NET application
package config

import (
	"fmt"
	"time"

	"github.com/spf13/viper"
)

// Config represents the complete application configuration
type Config struct {
	Server        ServerConfig        `mapstructure:"server"`
	Paths         PathsConfig         `mapstructure:"paths"`
	FileDiscovery FileDiscoveryConfig `mapstructure:"file_discovery"`
	ML            MLConfig            `mapstructure:"ml"`
	Database      DatabaseConfig      `mapstructure:"database"`
	Logging       LoggingConfig       `mapstructure:"logging"`
	ARM32         ARM32Config         `mapstructure:"arm32"`
}

// ServerConfig contains HTTP server settings
type ServerConfig struct {
	Host            string        `mapstructure:"host"`
	Port            int           `mapstructure:"port"`
	ReadTimeout     time.Duration `mapstructure:"read_timeout"`
	WriteTimeout    time.Duration `mapstructure:"write_timeout"`
	ShutdownTimeout time.Duration `mapstructure:"shutdown_timeout"`
	CORSAllowedOrigins []string   `mapstructure:"cors_allowed_origins"`
}

// PathsConfig contains file system path settings
type PathsConfig struct {
	MediaLibrary  string `mapstructure:"media_library"`
	WatchFolder   string `mapstructure:"watch_folder"`
	PendingReview string `mapstructure:"pending_review"`
}

// FileDiscoveryConfig contains file monitoring and discovery settings
type FileDiscoveryConfig struct {
	WatchFolders           []string      `mapstructure:"watch_folders"`
	EnableFileSystemWatcher bool          `mapstructure:"enable_file_system_watcher"`
	ScanIntervalMinutes    int           `mapstructure:"scan_interval_minutes"`
	FileExtensions         []string      `mapstructure:"file_extensions"`
	ExcludePatterns        []string      `mapstructure:"exclude_patterns"`
	MinFileSizeMB          int           `mapstructure:"min_file_size_mb"`
	DebounceDelaySeconds   int           `mapstructure:"debounce_delay_seconds"`
	MaxConcurrentScans     int           `mapstructure:"max_concurrent_scans"`
}

// MLConfig contains machine learning service settings
type MLConfig struct {
	ServiceURL              string  `mapstructure:"service_url"`
	Timeout                 time.Duration `mapstructure:"timeout"`
	AutoClassifyThreshold   float64 `mapstructure:"auto_classify_threshold"`
	SuggestionThreshold     float64 `mapstructure:"suggestion_threshold"`
	MaxClassificationTimeMs int     `mapstructure:"max_classification_time_ms"`
	MaxAlternativePredictions int   `mapstructure:"max_alternative_predictions"`
	EnablePredictionCaching bool    `mapstructure:"enable_prediction_caching"`
	MaxRetries              int     `mapstructure:"max_retries"`
}

// DatabaseConfig contains database connection settings
type DatabaseConfig struct {
	Path            string        `mapstructure:"path"`
	MaxOpenConns    int           `mapstructure:"max_open_conns"`
	MaxIdleConns    int           `mapstructure:"max_idle_conns"`
	ConnMaxLifetime time.Duration `mapstructure:"conn_max_lifetime"`
	WALMode         bool          `mapstructure:"wal_mode"`
}

// LoggingConfig contains logging settings
type LoggingConfig struct {
	Level            string `mapstructure:"level"`
	Format           string `mapstructure:"format"` // json or console
	OutputPath       string `mapstructure:"output_path"`
	MaxSizeMB        int    `mapstructure:"max_size_mb"`
	MaxBackups       int    `mapstructure:"max_backups"`
	MaxAgeDays       int    `mapstructure:"max_age_days"`
	EnableCaller     bool   `mapstructure:"enable_caller"`
	EnableStacktrace bool   `mapstructure:"enable_stacktrace"`
}

// ARM32Config contains ARM32-specific optimization settings
type ARM32Config struct {
	MemoryThresholdMB    int `mapstructure:"memory_threshold_mb"`
	AutoGCTriggerMB      int `mapstructure:"auto_gc_trigger_mb"`
	PerformanceThresholdMs int `mapstructure:"performance_threshold_ms"`
	MaxLogFileSizeMB     int `mapstructure:"max_log_file_size_mb"`
	WorkerCount          int `mapstructure:"worker_count"`
	MaxBatchSize         int `mapstructure:"max_batch_size"`
}

// Load loads configuration from file and environment variables
func Load(configPath string) (*Config, error) {
	v := viper.New()

	// Set config file path and type
	v.SetConfigFile(configPath)
	v.SetConfigType("json")

	// Set defaults
	setDefaults(v)

	// Enable environment variable overrides
	v.AutomaticEnv()

	// Read config file
	if err := v.ReadInConfig(); err != nil {
		return nil, fmt.Errorf("failed to read config file: %w", err)
	}

	// Unmarshal into struct
	var config Config
	if err := v.Unmarshal(&config); err != nil {
		return nil, fmt.Errorf("failed to unmarshal config: %w", err)
	}

	// Validate configuration
	if err := config.Validate(); err != nil {
		return nil, fmt.Errorf("invalid configuration: %w", err)
	}

	return &config, nil
}

// setDefaults sets default configuration values
func setDefaults(v *viper.Viper) {
	// Server defaults
	v.SetDefault("server.host", "0.0.0.0")
	v.SetDefault("server.port", 5001)
	v.SetDefault("server.read_timeout", "10s")
	v.SetDefault("server.write_timeout", "10s")
	v.SetDefault("server.shutdown_timeout", "30s")
	v.SetDefault("server.cors_allowed_origins", []string{"*"})

	// Paths defaults
	v.SetDefault("paths.media_library", "/library")
	v.SetDefault("paths.watch_folder", "/watch")
	v.SetDefault("paths.pending_review", "/tmp/mediabutler/pending")

	// File discovery defaults
	v.SetDefault("file_discovery.watch_folders", []string{"/watch"})
	v.SetDefault("file_discovery.enable_file_system_watcher", true)
	v.SetDefault("file_discovery.scan_interval_minutes", 10)
	v.SetDefault("file_discovery.file_extensions", []string{".mkv", ".mp4", ".avi", ".m4v", ".wmv"})
	v.SetDefault("file_discovery.exclude_patterns", []string{".*tmp", ".*part", ".*incomplete"})
	v.SetDefault("file_discovery.min_file_size_mb", 1)
	v.SetDefault("file_discovery.debounce_delay_seconds", 8)
	v.SetDefault("file_discovery.max_concurrent_scans", 1)

	// ML defaults
	v.SetDefault("ml.service_url", "http://localhost:5002")
	v.SetDefault("ml.timeout", "5s")
	v.SetDefault("ml.auto_classify_threshold", 0.85)
	v.SetDefault("ml.suggestion_threshold", 0.50)
	v.SetDefault("ml.max_classification_time_ms", 500)
	v.SetDefault("ml.max_alternative_predictions", 3)
	v.SetDefault("ml.enable_prediction_caching", true)
	v.SetDefault("ml.max_retries", 3)

	// Database defaults
	v.SetDefault("database.path", "/data/mediabutler.db")
	v.SetDefault("database.max_open_conns", 1)
	v.SetDefault("database.max_idle_conns", 2)
	v.SetDefault("database.conn_max_lifetime", "0")
	v.SetDefault("database.wal_mode", true)

	// Logging defaults
	v.SetDefault("logging.level", "info")
	v.SetDefault("logging.format", "json")
	v.SetDefault("logging.output_path", "/data/logs/mediabutler.log")
	v.SetDefault("logging.max_size_mb", 50)
	v.SetDefault("logging.max_backups", 7)
	v.SetDefault("logging.max_age_days", 30)
	v.SetDefault("logging.enable_caller", false)
	v.SetDefault("logging.enable_stacktrace", false)

	// ARM32 defaults
	v.SetDefault("arm32.memory_threshold_mb", 200)
	v.SetDefault("arm32.auto_gc_trigger_mb", 150)
	v.SetDefault("arm32.performance_threshold_ms", 1000)
	v.SetDefault("arm32.max_log_file_size_mb", 50)
	v.SetDefault("arm32.worker_count", 2)
	v.SetDefault("arm32.max_batch_size", 50)
}

// Validate performs validation on the configuration
func (c *Config) Validate() error {
	// Server validation
	if c.Server.Port < 1 || c.Server.Port > 65535 {
		return fmt.Errorf("invalid server port: %d", c.Server.Port)
	}

	// Paths validation
	if c.Paths.MediaLibrary == "" {
		return fmt.Errorf("media_library path is required")
	}
	if c.Paths.WatchFolder == "" {
		return fmt.Errorf("watch_folder path is required")
	}

	// File discovery validation
	if len(c.FileDiscovery.WatchFolders) == 0 {
		return fmt.Errorf("at least one watch folder is required")
	}
	if c.FileDiscovery.ScanIntervalMinutes < 1 {
		return fmt.Errorf("scan_interval_minutes must be at least 1")
	}
	if len(c.FileDiscovery.FileExtensions) == 0 {
		return fmt.Errorf("at least one file extension is required")
	}

	// ML validation
	if c.ML.ServiceURL == "" {
		return fmt.Errorf("ml service_url is required")
	}
	if c.ML.AutoClassifyThreshold < 0 || c.ML.AutoClassifyThreshold > 1 {
		return fmt.Errorf("auto_classify_threshold must be between 0 and 1")
	}
	if c.ML.SuggestionThreshold < 0 || c.ML.SuggestionThreshold > 1 {
		return fmt.Errorf("suggestion_threshold must be between 0 and 1")
	}

	// Database validation
	if c.Database.Path == "" {
		return fmt.Errorf("database path is required")
	}

	// ARM32 validation
	if c.ARM32.WorkerCount < 1 {
		return fmt.Errorf("worker_count must be at least 1")
	}
	if c.ARM32.MaxBatchSize < 1 {
		return fmt.Errorf("max_batch_size must be at least 1")
	}

	return nil
}

// GetServerAddress returns the formatted server address
func (c *Config) GetServerAddress() string {
	return fmt.Sprintf("%s:%d", c.Server.Host, c.Server.Port)
}

// IsProduction returns true if running in production mode
func (c *Config) IsProduction() bool {
	return c.Logging.Level == "warn" || c.Logging.Level == "error"
}
