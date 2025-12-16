package api

import (
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/lucapaganotti/mediabutler-go/internal/api/handlers"
	apimiddleware "github.com/lucapaganotti/mediabutler-go/internal/api/middleware"
	"github.com/rs/zerolog"
)

// RouterConfig holds configuration for the router
type RouterConfig struct {
	Logger            zerolog.Logger
	HealthHandler     *handlers.HealthHandler
	FilesHandler      *handlers.FilesHandler
	ProcessingHandler *handlers.ProcessingHandler
	AllowedOrigins    []string
	AllowCredentials  bool
}

// NewRouter creates a new Chi router with all endpoints configured
func NewRouter(cfg RouterConfig) *chi.Mux {
	r := chi.NewRouter()

	// Global middleware
	r.Use(middleware.RequestID)                  // Add request ID to context
	r.Use(apimiddleware.Recovery())             // Recover from panics
	r.Use(apimiddleware.DefaultLogger())        // Log requests with zerolog
	r.Use(middleware.Timeout(60 * time.Second)) // Request timeout

	// CORS middleware
	if len(cfg.AllowedOrigins) > 0 {
		r.Use(apimiddleware.CORSWithConfig(cfg.AllowedOrigins, cfg.AllowCredentials))
	} else {
		r.Use(apimiddleware.CORS())
	}

	// Health endpoints (no /api prefix for standard health checks)
	r.Get("/health", cfg.HealthHandler.Health)
	r.Get("/health/ready", cfg.HealthHandler.Ready)
	r.Get("/health/detailed", cfg.HealthHandler.Detailed)

	// API routes under /api prefix
	r.Route("/api", func(r chi.Router) {
		// Health endpoints (also available under /api for consistency)
		r.Get("/health", cfg.HealthHandler.Health)
		r.Route("/health", func(r chi.Router) {
			r.Get("/ready", cfg.HealthHandler.Ready)
			r.Get("/detailed", cfg.HealthHandler.Detailed)
		})

		// File management endpoints
		r.Route("/files", func(r chi.Router) {
			r.Get("/", cfg.FilesHandler.GetFiles)                      // GET /api/files
			r.Get("/by-statuses", cfg.FilesHandler.GetFilesByStatuses) // GET /api/files/by-statuses
			r.Get("/pending", cfg.FilesHandler.GetPendingFiles)        // GET /api/files/pending
			r.Get("/categories", cfg.FilesHandler.GetCategories)       // GET /api/files/categories
			r.Post("/", cfg.FilesHandler.RegisterFile)                 // POST /api/files
			r.Post("/scan", cfg.FilesHandler.ScanFolder)               // POST /api/files/scan

			// File-specific operations (by hash)
			r.Route("/{hash}", func(r chi.Router) {
				r.Get("/", cfg.FilesHandler.GetFileByHash)        // GET /api/files/{hash}
				r.Delete("/", cfg.FilesHandler.DeleteFile)        // DELETE /api/files/{hash}
				r.Post("/confirm", cfg.FilesHandler.ConfirmFile)  // POST /api/files/{hash}/confirm
				r.Post("/moved", cfg.FilesHandler.MarkFileAsMoved) // POST /api/files/{hash}/moved
			})
		})

		// Processing & stats endpoints
		r.Route("/stats", func(r chi.Router) {
			r.Get("/processing", cfg.ProcessingHandler.GetProcessingStats) // GET /api/stats/processing
		})

		r.Route("/processing", func(r chi.Router) {
			r.Route("/queue", func(r chi.Router) {
				r.Get("/status", cfg.ProcessingHandler.GetQueueStatus) // GET /api/processing/queue/status
			})
		})

		// File actions (v1 API)
		r.Route("/v1/file-actions", func(r chi.Router) {
			r.Post("/ignore/{hash}", cfg.ProcessingHandler.IgnoreFile) // POST /api/v1/file-actions/ignore/{hash}
		})
	})

	return r
}
