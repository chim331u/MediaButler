package main

import (
	"context"
	"database/sql"
	"fmt"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	_ "github.com/mattn/go-sqlite3" // SQLite driver
	"github.com/rs/zerolog"
	"github.com/rs/zerolog/log"

	"github.com/lucapaganotti/mediabutler-go/internal/api"
	"github.com/lucapaganotti/mediabutler-go/internal/api/handlers"
	"github.com/lucapaganotti/mediabutler-go/internal/config"
	"github.com/lucapaganotti/mediabutler-go/internal/jobs/batch"
	"github.com/lucapaganotti/mediabutler-go/internal/jobs/progress"
	"github.com/lucapaganotti/mediabutler-go/internal/repository"
	"github.com/lucapaganotti/mediabutler-go/internal/service"
	"github.com/lucapaganotti/mediabutler-go/internal/sse"
)

const version = "1.0.0"

func main() {
	// Load Configuration
	cfg, err := config.Load("./configs/config.json")
	if err != nil {
		log.Fatal().Err(err).Msg("Failed to load configuration")
	}

	// Initialize Logger
	logger := initLogger(cfg)
	log.Logger = logger

	logger.Info().
		Str("version", version).
		Str("port", fmt.Sprintf("%d", cfg.Server.Port)).
		Msg("Starting MediaButler Go API")

	// Initialize Database
	db, err := initDatabase(cfg)
	if err != nil {
		logger.Fatal().Err(err).Msg("Failed to initialize database")
	}
	defer func() {
		if err := db.Close(); err != nil {
			logger.Error().Err(err).Msg("Failed to close database")
		}
	}()

	logger.Info().Str("path", cfg.Database.Path).Msg("Database initialized")

	// Initialize Repositories
	fileRepo := repository.NewFileRepository(db)
	batchRepo := repository.NewBatchJobRepository(db)
	uow := repository.NewUnitOfWork(db)

	// Initialize Services
	fileService := service.NewFileService(fileRepo, uow)
	mlClient := service.NewMLClient(cfg.ML)
	statsService := service.NewStatsService(fileRepo)
	scannerService := service.NewScannerService(fileService, cfg.FileDiscovery, logger)

	logger.Info().Msg("Services initialized")

	// Initialize SSE Broker (moved up for batch system)
	sseBroker := sse.NewBroker(logger)
	logger.Info().Msg("SSE broker initialized")

	// Initialize Batch Processing Components
	fileOrgService := service.NewFileOrganizationService(fileService, cfg.Paths.MediaLibrary, logger)

	// Progress reporter for SSE updates
	progressReporter := progress.NewProgressReporter(sseBroker, logger)

	// Throttler for ARM32 resource management
	throttlerConfig := progress.DefaultThrottlerConfig()
	throttler := progress.NewBatchThrottler(throttlerConfig, logger)

	// Batch executor
	executor := batch.NewExecutor(batchRepo, fileRepo, fileOrgService, progressReporter, throttler, logger)

	// Batch scheduler
	schedulerConfig := batch.DefaultSchedulerConfig()
	scheduler := batch.NewScheduler(schedulerConfig, batchRepo, executor, logger)

	// Start scheduler
	if err := scheduler.Start(); err != nil {
		logger.Fatal().Err(err).Msg("Failed to start batch scheduler")
	}
	logger.Info().Msg("Batch scheduler started")

	// Initialize FileActionsService
	fileActionsService := service.NewFileActionsService(batchRepo, fileRepo, fileOrgService, scheduler, uow, logger)

	logger.Info().Msg("Batch processing system initialized")

	// Inject SSE broker into services (via type assertion to concrete type method)
	if setter, ok := fileService.(interface{ SetSSEBroker(*sse.Broker) }); ok {
		setter.SetSSEBroker(sseBroker)
		logger.Info().Msg("SSE broker injected into FileService")
	}

	// Initialize Handlers
	healthHandler := handlers.NewHealthHandler(version)
	filesHandler := handlers.NewFilesHandler(fileService, scannerService, cfg.FileDiscovery.WatchFolders)
	processingHandler := handlers.NewProcessingHandler(fileService, statsService)
	fileActionsHandler := handlers.NewFileActionsHandler(fileActionsService)
	sseHandler := handlers.NewSSEHandler(sseBroker, logger)

	// Configure Router
	routerConfig := api.RouterConfig{
		Logger:             logger,
		HealthHandler:      healthHandler,
		FilesHandler:       filesHandler,
		ProcessingHandler:  processingHandler,
		FileActionsHandler: fileActionsHandler,
		SSEHandler:         sseHandler,
		AllowedOrigins:     cfg.Server.CORSAllowedOrigins,
		AllowCredentials:   true,
	}

	router := api.NewRouter(routerConfig)

	logger.Info().Msg("Router configured with all endpoints")

	// Configure HTTP Server
	server := &http.Server{
		Addr:         cfg.GetServerAddress(),
		Handler:      router,
		ReadTimeout:  cfg.Server.ReadTimeout,
		WriteTimeout: cfg.Server.WriteTimeout,
		IdleTimeout:  120 * time.Second,
	}

	// Start Server in Goroutine
	serverErrors := make(chan error, 1)
	go func() {
		logger.Info().
			Str("address", server.Addr).
			Msg("HTTP server starting")

		serverErrors <- server.ListenAndServe()
	}()

	// Wait for interrupt signal or server error
	shutdown := make(chan os.Signal, 1)
	signal.Notify(shutdown, os.Interrupt, syscall.SIGTERM)

	select {
	case err := <-serverErrors:
		logger.Fatal().Err(err).Msg("Server failed to start")

	case sig := <-shutdown:
		logger.Info().
			Str("signal", sig.String()).
			Msg("Shutdown signal received, starting graceful shutdown")

		// Stop batch scheduler first
		logger.Info().Msg("Stopping batch scheduler")
		if err := scheduler.Stop(); err != nil {
			logger.Error().Err(err).Msg("Failed to stop batch scheduler")
		}

		// Create context with timeout for shutdown
		ctx, cancel := context.WithTimeout(context.Background(), cfg.Server.ShutdownTimeout)
		defer cancel()

		// Attempt graceful shutdown
		if err := server.Shutdown(ctx); err != nil {
			logger.Error().Err(err).Msg("Failed to gracefully shutdown server")

			// Force shutdown
			if err := server.Close(); err != nil {
				logger.Error().Err(err).Msg("Failed to force close server")
			}
		}

		logger.Info().Msg("Server stopped")
	}

	// Note: ML client intentionally not used directly in main.go
	// It will be integrated when classification endpoint is added
	_ = mlClient
}

// initLogger initializes zerolog based on configuration
func initLogger(cfg *config.Config) zerolog.Logger {
	// Set global log level
	level, err := zerolog.ParseLevel(cfg.Logging.Level)
	if err != nil {
		level = zerolog.InfoLevel
	}
	zerolog.SetGlobalLevel(level)

	// Configure output format
	var logger zerolog.Logger
	if cfg.Logging.Format == "console" {
		// Pretty console output for development
		output := zerolog.ConsoleWriter{
			Out:        os.Stdout,
			TimeFormat: time.RFC3339,
		}
		logger = zerolog.New(output).With().Timestamp().Logger()
	} else {
		// JSON output for production
		logger = zerolog.New(os.Stdout).With().Timestamp().Logger()
	}

	// Add caller information if enabled
	if cfg.Logging.EnableCaller {
		logger = logger.With().Caller().Logger()
	}

	return logger
}

// initDatabase initializes SQLite database connection with configuration
func initDatabase(cfg *config.Config) (*sql.DB, error) {
	// Build connection string with SQLite pragmas
	dsn := cfg.Database.Path

	// Add WAL mode if enabled (recommended for concurrent access)
	if cfg.Database.WALMode {
		dsn += "?_journal_mode=WAL"
	}

	// Open database connection
	db, err := sql.Open("sqlite3", dsn)
	if err != nil {
		return nil, fmt.Errorf("open database: %w", err)
	}

	// Configure connection pool
	db.SetMaxOpenConns(cfg.Database.MaxOpenConns)
	db.SetMaxIdleConns(cfg.Database.MaxIdleConns)
	if cfg.Database.ConnMaxLifetime > 0 {
		db.SetConnMaxLifetime(cfg.Database.ConnMaxLifetime)
	}

	// Verify connection
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	if err := db.PingContext(ctx); err != nil {
		return nil, fmt.Errorf("ping database: %w", err)
	}

	return db, nil
}
