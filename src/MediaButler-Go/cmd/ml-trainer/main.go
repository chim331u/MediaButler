package main

import (
	"database/sql"
	"encoding/csv"
	"flag"
	"fmt"
	"os"
	"time"

	"github.com/chim331u/mediabutler-go/pkg/ml/training"
	_ "github.com/mattn/go-sqlite3"
)

// Command-line flags
var (
	dbPath     = flag.String("db", "temp/mediabutler.dev.db", "Path to SQLite database")
	importFile = flag.String("import", "", "CSV file to import training data from")
	trainModel = flag.Bool("train", false, "Train/rebuild model from SQLite statistics")
	showStats  = flag.Bool("stats", false, "Show current training statistics")
	applySchema = flag.Bool("apply-schema", false, "Apply ML schema migration")
)

func main() {
	flag.Parse()

	// Validate flags
	if *dbPath == "" {
		fmt.Fprintln(os.Stderr, "Error: --db flag is required")
		flag.Usage()
		os.Exit(1)
	}

	// Open database
	db, err := sql.Open("sqlite3", *dbPath)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error opening database: %v\n", err)
		os.Exit(1)
	}
	defer db.Close()

	// Test connection
	if err := db.Ping(); err != nil {
		fmt.Fprintf(os.Stderr, "Error connecting to database: %v\n", err)
		os.Exit(1)
	}

	fmt.Println("========================================")
	fmt.Println("MediaButler Go API - ML Trainer")
	fmt.Println("========================================")
	fmt.Println()
	fmt.Printf("Database: %s\n", *dbPath)
	fmt.Println()

	// Create trainer instance
	trainer := training.NewIncrementalTrainer(db)

	// Execute requested operations
	executed := false

	// 1. Apply schema if requested
	if *applySchema {
		executed = true
		if err := applyMLSchema(db); err != nil {
			fmt.Fprintf(os.Stderr, "Error applying schema: %v\n", err)
			os.Exit(1)
		}
	}

	// 2. Import CSV if provided
	if *importFile != "" {
		executed = true
		if err := importCSV(trainer, *importFile); err != nil {
			fmt.Fprintf(os.Stderr, "Error importing CSV: %v\n", err)
			os.Exit(1)
		}
	}

	// 3. Train model if requested
	if *trainModel {
		executed = true
		if err := trainModelFromDB(trainer); err != nil {
			fmt.Fprintf(os.Stderr, "Error training model: %v\n", err)
			os.Exit(1)
		}
	}

	// 4. Show stats if requested
	if *showStats {
		executed = true
		if err := showTrainingStats(trainer); err != nil {
			fmt.Fprintf(os.Stderr, "Error getting stats: %v\n", err)
			os.Exit(1)
		}
	}

	// If no operation was specified, show usage
	if !executed {
		fmt.Println("No operation specified. Use --help for usage.")
		fmt.Println()
		fmt.Println("Quick start:")
		fmt.Println("  1. Apply schema:  go run cmd/ml-trainer/main.go --apply-schema")
		fmt.Println("  2. Import data:   go run cmd/ml-trainer/main.go --import data/train_split.csv")
		fmt.Println("  3. Train model:   go run cmd/ml-trainer/main.go --train")
		fmt.Println("  4. Show stats:    go run cmd/ml-trainer/main.go --stats")
		fmt.Println()
		flag.Usage()
		os.Exit(0)
	}

	fmt.Println()
	fmt.Println("✅ All operations completed successfully!")
}

// applyMLSchema applies the ML schema migration
func applyMLSchema(db *sql.DB) error {
	fmt.Println("📋 Applying ML schema migration...")

	// Check if schema is already applied
	var count int
	err := db.QueryRow("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ml_samples'").Scan(&count)
	if err != nil {
		return fmt.Errorf("check existing schema: %w", err)
	}

	if count > 0 {
		fmt.Println("ℹ️  ML schema already applied. Skipping.")
		return nil
	}

	// Read and execute schema file
	schemaPath := "pkg/ml/db/migrations/001_ml_schema.sql"
	schemaSQL, err := os.ReadFile(schemaPath)
	if err != nil {
		return fmt.Errorf("read schema file: %w", err)
	}

	if _, err := db.Exec(string(schemaSQL)); err != nil {
		return fmt.Errorf("execute schema: %w", err)
	}

	fmt.Println("✅ ML schema applied successfully!")

	// Show created tables
	rows, err := db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'ml_%' ORDER BY name")
	if err == nil {
		defer rows.Close()
		fmt.Println("\nTables created:")
		for rows.Next() {
			var tableName string
			if err := rows.Scan(&tableName); err == nil {
				var rowCount int
				db.QueryRow(fmt.Sprintf("SELECT COUNT(*) FROM %s", tableName)).Scan(&rowCount)
				fmt.Printf("  - %s (%d rows)\n", tableName, rowCount)
			}
		}
	}

	return nil
}

// importCSV imports training data from a CSV file
func importCSV(trainer *training.IncrementalTrainer, csvPath string) error {
	startTime := time.Now()

	fmt.Printf("📥 Importing training data from: %s\n", csvPath)

	// Open CSV file
	file, err := os.Open(csvPath)
	if err != nil {
		return fmt.Errorf("open CSV file: %w", err)
	}
	defer file.Close()

	// Create CSV reader
	reader := csv.NewReader(file)

	// Read header
	header, err := reader.Read()
	if err != nil {
		return fmt.Errorf("read CSV header: %w", err)
	}

	// Validate header
	if len(header) != 2 || header[0] != "FileName" || header[1] != "Category" {
		return fmt.Errorf("invalid CSV format: expected header [FileName,Category], got %v", header)
	}

	// Read all records
	records, err := reader.ReadAll()
	if err != nil {
		return fmt.Errorf("read CSV records: %w", err)
	}

	fmt.Printf("Found %d training samples in CSV\n", len(records))

	// Convert to TrainingSample slice
	samples := make([]training.TrainingSample, 0, len(records))
	for i, record := range records {
		if len(record) != 2 {
			return fmt.Errorf("invalid record at line %d: expected 2 fields, got %d", i+2, len(record))
		}

		samples = append(samples, training.TrainingSample{
			Filename: record[0],
			Class:    record[1],
		})
	}

	// Import samples in batches
	batchSize := 100
	totalBatches := (len(samples) + batchSize - 1) / batchSize

	fmt.Printf("Importing in %d batches of %d samples...\n\n", totalBatches, batchSize)

	for i := 0; i < len(samples); i += batchSize {
		end := i + batchSize
		if end > len(samples) {
			end = len(samples)
		}

		batch := samples[i:end]
		batchNum := (i / batchSize) + 1

		fmt.Printf("  Batch %d/%d: Importing samples %d-%d...", batchNum, totalBatches, i+1, end)

		if err := trainer.AddSamples(batch); err != nil {
			fmt.Println(" ❌")
			return fmt.Errorf("import batch %d: %w", batchNum, err)
		}

		fmt.Println(" ✅")
	}

	elapsed := time.Since(startTime)
	fmt.Printf("\n✅ Imported %d samples in %v (%.2f samples/sec)\n", len(samples), elapsed, float64(len(samples))/elapsed.Seconds())

	return nil
}

// trainModelFromDB trains the model from SQLite statistics
func trainModelFromDB(trainer *training.IncrementalTrainer) error {
	fmt.Println("🎯 Training Naive Bayes model from SQLite statistics...")
	fmt.Println()

	model, err := trainer.RebuildModel()
	if err != nil {
		return fmt.Errorf("rebuild model: %w", err)
	}

	fmt.Println()
	fmt.Println("✅ Model training complete!")
	fmt.Println()
	fmt.Println("Model summary:")
	fmt.Printf("  - Classes: %d\n", len(model.GetClasses()))
	fmt.Printf("  - Vocabulary size: %d tokens\n", model.VocabSize)
	fmt.Printf("  - Smoothing (alpha): %.2f\n", model.Alpha)

	// Show top 5 classes by prior probability
	fmt.Println("\nTop 5 classes by prior probability:")
	type classPrior struct {
		Class string
		Prior float64
	}
	priors := make([]classPrior, 0, len(model.GetClasses()))
	for _, class := range model.GetClasses() {
		if prior, exists := model.GetPrior(class); exists {
			priors = append(priors, classPrior{Class: class, Prior: prior})
		}
	}

	// Sort by prior descending
	for i := 0; i < len(priors); i++ {
		for j := i + 1; j < len(priors); j++ {
			if priors[j].Prior > priors[i].Prior {
				priors[i], priors[j] = priors[j], priors[i]
			}
		}
	}

	// Show top 5
	for i := 0; i < 5 && i < len(priors); i++ {
		fmt.Printf("  %d. %s (prior: %.4f)\n", i+1, priors[i].Class, priors[i].Prior)
	}

	return nil
}

// showTrainingStats displays current training statistics
func showTrainingStats(trainer *training.IncrementalTrainer) error {
	fmt.Println("📊 Training Statistics")
	fmt.Println()

	stats, err := trainer.GetTrainingStats()
	if err != nil {
		return fmt.Errorf("get training stats: %w", err)
	}

	fmt.Printf("Total samples: %d\n", stats.TotalSamples)
	fmt.Printf("Number of classes: %d\n", stats.NumClasses)
	fmt.Printf("Vocabulary size: %d unique tokens\n", stats.VocabularySize)
	fmt.Println()

	if len(stats.TopClasses) > 0 {
		fmt.Println("Top 10 classes by document count:")
		for i, classStat := range stats.TopClasses {
			percentage := float64(classStat.DocCount) / float64(stats.TotalSamples) * 100
			fmt.Printf("  %2d. %-30s %4d samples (%.1f%%)\n",
				i+1, classStat.Class, classStat.DocCount, percentage)
		}
	}

	return nil
}
