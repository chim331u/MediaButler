package main

import (
	"database/sql"
	"fmt"
	"log/slog"
	"os"
	"path/filepath"

	_ "modernc.org/sqlite"
)

// InitDB initializes the SQLite connection using pure Go modernc.org/sqlite
// and applies performance optimizations optimized for ARM32 v7.
func InitDB(dbPath string) (*sql.DB, error) {
	// Ensure parent directory exists
	dir := filepath.Dir(dbPath)
	if err := os.MkdirAll(dir, 0755); err != nil {
		return nil, fmt.Errorf("failed to create database directory: %w", err)
	}

	slog.Info("Opening SQLite database", "path", dbPath)
	db, err := sql.Open("sqlite", dbPath)
	if err != nil {
		return nil, fmt.Errorf("failed to open database: %w", err)
	}

	// Apply optimizations for low-resource environments (ARM32 v7, 1GB RAM)
	pragmas := []string{
		"PRAGMA journal_mode=WAL;",      // Write-Ahead Logging for concurrent read/write
		"PRAGMA synchronous=NORMAL;",    // Less disk syncing than FULL, safe enough for WAL
		"PRAGMA cache_size=-2000;",      // ~2MB memory cache limit to conserve RAM
		"PRAGMA busy_timeout=5000;",     // Wait up to 5s if db is locked
		"PRAGMA foreign_keys=ON;",       // Enforce foreign key constraints
	}

	for _, pragma := range pragmas {
		if _, err := db.Exec(pragma); err != nil {
			db.Close()
			return nil, fmt.Errorf("failed to apply pragma '%s': %w", pragma, err)
		}
	}

	// Set connection limits. Since WAL mode supports multiple concurrent readers,
	// we allow up to 5 connections. SQLite automatically serializes writes using busy_timeout.
	db.SetMaxOpenConns(5)
	db.SetMaxIdleConns(2)

	slog.Info("SQLite database initialized successfully with WAL and performance pragmas")
	return db, nil
}

// EnsureSchema creates the database tables if they do not exist.
func EnsureSchema(db *sql.DB) error {
	schema := `
	CREATE TABLE IF NOT EXISTS TrackedFiles (
		Hash varchar(64) NOT NULL CONSTRAINT PK_TrackedFiles PRIMARY KEY,
		FileName varchar(500) NOT NULL,
		OriginalPath varchar(1000) NOT NULL,
		FileSize bigint NOT NULL,
		Status integer NOT NULL DEFAULT 0,
		SuggestedCategory varchar(200) NULL,
		Confidence decimal(5,4) NOT NULL DEFAULT '0.0',
		Category varchar(200) NULL,
		TargetPath varchar(1000) NULL,
		ClassifiedAt datetime NULL,
		MovedAt datetime NULL,
		LastError text NULL,
		LastErrorAt datetime NULL,
		RetryCount integer NOT NULL DEFAULT 0,
		CreatedDate datetime NOT NULL,
		LastUpdateDate datetime NOT NULL,
		Note text NULL,
		IsActive boolean NOT NULL DEFAULT 1,
		MovedToPath TEXT NULL
	);

	CREATE TABLE IF NOT EXISTS UserPreferences (
		Id char(36) NOT NULL CONSTRAINT PK_UserPreferences PRIMARY KEY,
		UserId varchar(100) NOT NULL DEFAULT 'default',
		Key varchar(200) NOT NULL,
		Value text NOT NULL,
		Category varchar(100) NOT NULL,
		CreatedDate datetime NOT NULL,
		LastUpdateDate datetime NOT NULL,
		Note text NULL,
		IsActive boolean NOT NULL DEFAULT 1
	);

	CREATE INDEX IF NOT EXISTS idx_tracked_files_status ON TrackedFiles(Status);
	CREATE INDEX IF NOT EXISTS idx_user_preferences_key ON UserPreferences(Key);
	`
	_, err := db.Exec(schema)
	if err != nil {
		return fmt.Errorf("failed to apply schema: %w", err)
	}
	slog.Info("Database schema verified")
	return nil
}
