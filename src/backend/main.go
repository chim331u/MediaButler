package main

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"
)

func main() {
	// 1. Load Configurations
	cfg := LoadConfig()

	// 2. Setup Structured JSON logging (slog) directly to stdout/stderr for Dozzle compatibility
	logHandler := slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{
		Level: cfg.LogLevel,
	})
	logger := slog.New(logHandler)
	slog.SetDefault(logger)

	slog.Info("Starting MediaButler API Server (Go)", "port", cfg.Port, "env_db_path", cfg.DatabasePath)

	// 3. Initialize SQLite Connection (pure Go SQLite driver, no CGO)
	db, err := InitDB(cfg.DatabasePath)
	if err != nil {
		slog.Error("Database initialization failed", "err", err)
		os.Exit(1)
	}
	defer func() {
		slog.Info("Closing SQLite database connection")
		if err := db.Close(); err != nil {
			slog.Error("Failed to close database", "err", err)
		}
	}()

	// 4. Ensure schema/tables are created
	if err := EnsureSchema(db); err != nil {
		slog.Error("Database schema verification failed", "err", err)
		os.Exit(1)
	}
	if err := EnsureModelSchema(db); err != nil {
		slog.Error("Model word frequency schema verification failed", "err", err)
		os.Exit(1)
	}

	// 5. Initialize Server, SSE Broker & Register Routes
	sseBroker := NewSSEBroker()
	
	// Start File Watcher
	watcherCtx, cancelWatcher := context.WithCancel(context.Background())
	defer cancelWatcher()

	watcher, err := NewWatcher(cfg, db, sseBroker)
	if err != nil {
		slog.Error("Failed to initialize file watcher", "err", err)
	} else {
		watcher.Start(watcherCtx)
	}

	server := NewServer(cfg, db, sseBroker, watcher)
	mux := http.NewServeMux()
	server.RegisterRoutes(mux)
	RegisterStaticRoutes(mux)

	// Add a root handler or basic health check
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(`{"status":"healthy","service":"mediabutler-api"}`))
	})

	// Add CORS and logging middleware
	handler := logRequest(mux)

	httpServer := &http.Server{
		Addr:         ":" + cfg.Port,
		Handler:      handler,
		ReadTimeout:  15 * time.Second,
		WriteTimeout: 15 * time.Second,
		IdleTimeout:  60 * time.Second,
	}

	// 6. Graceful Shutdown Management
	shutdownChan := make(chan os.Signal, 1)
	signal.Notify(shutdownChan, os.Interrupt, syscall.SIGTERM)

	go func() {
		slog.Info(fmt.Sprintf("HTTP Server listening on port %s", cfg.Port))
		if err := httpServer.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			slog.Error("HTTP Server error", "err", err)
			os.Exit(1)
		}
	}()

	// Wait for termination signal
	sig := <-shutdownChan
	slog.Info("Shutdown signal received, shutting down gracefully", "signal", sig.String())

	// Shutdown timeout context
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	if err := httpServer.Shutdown(ctx); err != nil {
		slog.Error("Server forced to shutdown", "err", err)
		os.Exit(1)
	}

	slog.Info("MediaButler API Server stopped cleanly")
}

// Simple request logger middleware to write structured logs directly to stdout
func logRequest(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		next.ServeHTTP(w, r)
		slog.Info("HTTP Request",
			"method", r.Method,
			"path", r.URL.Path,
			"remote_ip", r.RemoteAddr,
			"latency_ms", time.Since(start).Milliseconds(),
		)
	})
}
