package repository

import (
	"context"
	"database/sql"
	"testing"
	"time"

	"github.com/chim331u/mediabutler-go/internal/domain"
	_ "github.com/mattn/go-sqlite3"
)

func TestGetFilesByStatuses_Repro(t *testing.T) {
	// Setup in-memory DB
	db, err := sql.Open("sqlite3", ":memory:")
	if err != nil {
		t.Fatalf("failed to open db: %v", err)
	}
	defer db.Close()

	// Create table
	_, err = db.Exec(`
		CREATE TABLE "TrackedFiles" (
			"Hash" varchar(64) NOT NULL PRIMARY KEY,
			"FileName" varchar(500) NOT NULL,
			"OriginalPath" varchar(1000) NOT NULL,
			"FileSize" bigint NOT NULL,
			"Status" integer NOT NULL DEFAULT 0,
			"SuggestedCategory" varchar(200) NULL,
			"Confidence" decimal(5,4) NOT NULL DEFAULT '0.0',
			"Category" varchar(200) NULL,
			"TargetPath" varchar(1000) NULL,
			"ClassifiedAt" datetime NULL,
			"MovedAt" datetime NULL,
			"LastError" text NULL,
			"LastErrorAt" datetime NULL,
			"RetryCount" integer NOT NULL DEFAULT 0,
			"CreatedDate" datetime NOT NULL,
			"LastUpdateDate" datetime NOT NULL,
			"Note" text NULL,
			"IsActive" boolean NOT NULL DEFAULT 1,
			"MovedToPath" TEXT NULL
		);
	`)
	if err != nil {
		t.Fatalf("failed to create table: %v", err)
	}

	repo := NewFileRepository(db)
	ctx := context.Background()

	// Insert test data
	file1 := &domain.TrackedFile{
		Hash:         "0000000000000000000000000000000000000000000000000000000000000001",
		FileName:     "file1.mkv",
		OriginalPath: "/path/file1.mkv",
		FileSize:     1024,
		Status:       domain.FileStatusNew,
		BaseEntity: domain.BaseEntity{
			CreatedDate:    time.Now(),
			LastUpdateDate: time.Now(),
			IsActive:       true,
		},
	}
	if res := repo.Create(ctx, file1); res.IsFailure() {
		t.Fatalf("failed to create file1: %v", res.Error())
	}

	file2 := &domain.TrackedFile{
		Hash:         "0000000000000000000000000000000000000000000000000000000000000002",
		FileName:     "file2.mkv",
		OriginalPath: "/path/file2.mkv",
		FileSize:     2048,
		Status:       domain.FileStatusClassified,
		BaseEntity: domain.BaseEntity{
			CreatedDate:    time.Now(),
			LastUpdateDate: time.Now(),
			IsActive:       true,
		},
	}
	if res := repo.Create(ctx, file2); res.IsFailure() {
		t.Fatalf("failed to create file2: %v", res.Error())
	}

	// Test GetFilesByStatuses with multiple statuses
	statuses := []domain.FileStatus{domain.FileStatusNew, domain.FileStatusClassified}
	res := repo.GetFilesByStatuses(ctx, statuses, 10, 0)

	if res.IsFailure() {
		errMsg := res.Error().Error()
		// We expect this to fail with scan error initially
		t.Logf("Reproduction: Got expected error: %v", errMsg)
		t.Fatalf("Test Failed as expected (Reproduction): %v", errMsg)
	}

	if len(res.Value()) != 2 {
		t.Errorf("expected 2 files, got %d", len(res.Value()))
	}
}
