package training

import (
	"database/sql"
	"encoding/json"
	"fmt"
	"time"

	"github.com/lucapaganotti/mediabutler-go/pkg/ml/classifier"
	"github.com/lucapaganotti/mediabutler-go/pkg/ml/tokenizer"
)

// IncrementalTrainer handles incremental model training from SQLite
// Supports adding single samples and rebuilding the model efficiently
type IncrementalTrainer struct {
	db         *sql.DB
	tokenizer  *tokenizer.ItalianTokenizer
	modelCache *classifier.NaiveBayesModel // Cached current model
}

// TrainingResult represents the result of a training operation
type TrainingResult struct {
	Success         bool
	NumSamples      int
	NumClasses      int
	TrainingTimeMs  int64
	Message         string
	Error           error
}

// NewIncrementalTrainer creates a new incremental trainer instance
func NewIncrementalTrainer(db *sql.DB) *IncrementalTrainer {
	return &IncrementalTrainer{
		db:        db,
		tokenizer: tokenizer.NewItalianTokenizer(),
	}
}

// AddSample adds a single training sample and updates statistics
// This performs an incremental update without rebuilding the entire model
func (t *IncrementalTrainer) AddSample(filename, class string) error {
	// Step 1: Extract series name and tokenize
	seriesName := t.tokenizer.ExtractSeriesName(filename)
	if seriesName == "" {
		return fmt.Errorf("could not extract series name from filename: %s", filename)
	}

	tokens := t.tokenizer.Tokenize(filename)
	if len(tokens) == 0 {
		return fmt.Errorf("no tokens extracted from filename: %s", filename)
	}

	// Serialize tokens to JSON
	tokensJSON, err := json.Marshal(tokens)
	if err != nil {
		return fmt.Errorf("marshal tokens: %w", err)
	}

	// Step 2: Start transaction for atomic updates
	tx, err := t.db.Begin()
	if err != nil {
		return fmt.Errorf("begin transaction: %w", err)
	}
	defer tx.Rollback() // Will be ignored if Commit succeeds

	// Step 3: Insert into ml_samples
	_, err = tx.Exec(
		`INSERT INTO ml_samples (filename, series_name, class, tokens, created_date)
		 VALUES (?, ?, ?, ?, ?)`,
		filename, seriesName, class, string(tokensJSON), time.Now(),
	)
	if err != nil {
		return fmt.Errorf("insert sample: %w", err)
	}

	// Step 4: Update ml_class_stats
	// Trigger trg_ml_samples_insert_update_stats will handle doc_count
	// We need to update total_tokens manually
	_, err = tx.Exec(
		`UPDATE ml_class_stats
		 SET total_tokens = total_tokens + ?
		 WHERE class = ?`,
		len(tokens), class,
	)
	if err != nil {
		return fmt.Errorf("update class stats: %w", err)
	}

	// Step 5: Update ml_token_stats for each token
	for _, token := range tokens {
		_, err = tx.Exec(
			`INSERT INTO ml_token_stats (token, class, count)
			 VALUES (?, ?, 1)
			 ON CONFLICT(token, class) DO UPDATE SET
			   count = count + 1`,
			token, class,
		)
		if err != nil {
			return fmt.Errorf("update token stats for token '%s': %w", token, err)
		}
	}

	// Step 6: Update ml_idf_stats
	// Track which tokens we've seen in this document (for document frequency)
	seenTokens := make(map[string]bool)
	for _, token := range tokens {
		if !seenTokens[token] {
			_, err = tx.Exec(
				`INSERT INTO ml_idf_stats (token, doc_freq)
				 VALUES (?, 1)
				 ON CONFLICT(token) DO UPDATE SET
				   doc_freq = doc_freq + 1`,
				token,
			)
			if err != nil {
				return fmt.Errorf("update idf stats for token '%s': %w", token, err)
			}
			seenTokens[token] = true
		}
	}

	// Step 7: Commit transaction
	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit transaction: %w", err)
	}

	return nil
}

// AddSamples adds multiple training samples in a single transaction
// More efficient than calling AddSample repeatedly
func (t *IncrementalTrainer) AddSamples(samples []TrainingSample) error {
	if len(samples) == 0 {
		return fmt.Errorf("no samples provided")
	}

	// Start transaction
	tx, err := t.db.Begin()
	if err != nil {
		return fmt.Errorf("begin transaction: %w", err)
	}
	defer tx.Rollback()

	// Prepare statements for efficiency
	stmtSample, err := tx.Prepare(
		`INSERT INTO ml_samples (filename, series_name, class, tokens, created_date)
		 VALUES (?, ?, ?, ?, ?)`,
	)
	if err != nil {
		return fmt.Errorf("prepare sample statement: %w", err)
	}
	defer stmtSample.Close()

	stmtClass, err := tx.Prepare(
		`INSERT INTO ml_class_stats (class, doc_count, total_tokens)
		 VALUES (?, 1, ?)
		 ON CONFLICT(class) DO UPDATE SET
		   doc_count = doc_count + 1,
		   total_tokens = total_tokens + ?`,
	)
	if err != nil {
		return fmt.Errorf("prepare class statement: %w", err)
	}
	defer stmtClass.Close()

	stmtToken, err := tx.Prepare(
		`INSERT INTO ml_token_stats (token, class, count)
		 VALUES (?, ?, 1)
		 ON CONFLICT(token, class) DO UPDATE SET
		   count = count + 1`,
	)
	if err != nil {
		return fmt.Errorf("prepare token statement: %w", err)
	}
	defer stmtToken.Close()

	stmtIDF, err := tx.Prepare(
		`INSERT INTO ml_idf_stats (token, doc_freq)
		 VALUES (?, 1)
		 ON CONFLICT(token) DO UPDATE SET
		   doc_freq = doc_freq + 1`,
	)
	if err != nil {
		return fmt.Errorf("prepare idf statement: %w", err)
	}
	defer stmtIDF.Close()

	// Process each sample
	for i, sample := range samples {
		// Extract series name and tokenize
		seriesName := t.tokenizer.ExtractSeriesName(sample.Filename)
		if seriesName == "" {
			return fmt.Errorf("sample %d: could not extract series name from %s", i, sample.Filename)
		}

		tokens := t.tokenizer.Tokenize(sample.Filename)
		if len(tokens) == 0 {
			return fmt.Errorf("sample %d: no tokens extracted from %s", i, sample.Filename)
		}

		// Serialize tokens
		tokensJSON, err := json.Marshal(tokens)
		if err != nil {
			return fmt.Errorf("sample %d: marshal tokens: %w", i, err)
		}

		// Insert sample
		_, err = stmtSample.Exec(sample.Filename, seriesName, sample.Class, string(tokensJSON), time.Now())
		if err != nil {
			return fmt.Errorf("sample %d: insert sample: %w", i, err)
		}

		// Update class stats
		tokenCount := len(tokens)
		_, err = stmtClass.Exec(sample.Class, tokenCount, tokenCount)
		if err != nil {
			return fmt.Errorf("sample %d: update class stats: %w", i, err)
		}

		// Update token stats
		for _, token := range tokens {
			_, err = stmtToken.Exec(token, sample.Class)
			if err != nil {
				return fmt.Errorf("sample %d: update token stats: %w", i, err)
			}
		}

		// Update IDF stats (track unique tokens per document)
		seenTokens := make(map[string]bool)
		for _, token := range tokens {
			if !seenTokens[token] {
				_, err = stmtIDF.Exec(token)
				if err != nil {
					return fmt.Errorf("sample %d: update idf stats: %w", i, err)
				}
				seenTokens[token] = true
			}
		}
	}

	// Commit transaction
	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit transaction: %w", err)
	}

	return nil
}

// RebuildModel rebuilds the Naive Bayes model from current SQLite statistics
// This loads all statistics and constructs a new model in memory
func (t *IncrementalTrainer) RebuildModel() (*classifier.NaiveBayesModel, error) {
	startTime := time.Now()

	// Train model from SQLite
	model, err := classifier.TrainFromSQLite(t.db)
	if err != nil {
		return nil, fmt.Errorf("train from sqlite: %w", err)
	}

	// Cache the model
	t.modelCache = model

	elapsedMs := time.Since(startTime).Milliseconds()

	// Log training time
	fmt.Printf("Model rebuilt in %d ms\n", elapsedMs)
	fmt.Printf("  Classes: %d\n", len(model.GetClasses()))
	fmt.Printf("  Vocabulary: %d tokens\n", model.VocabSize)

	return model, nil
}

// TrainFromCSV imports training data from a CSV file and trains the model
// CSV format: FileName,Category
func (t *IncrementalTrainer) TrainFromCSV(csvPath string) (*TrainingResult, error) {
	startTime := time.Now()

	// TODO: Implement CSV parsing and bulk import
	// For now, return placeholder
	result := &TrainingResult{
		Success:        false,
		Message:        "CSV import not yet implemented",
		TrainingTimeMs: time.Since(startTime).Milliseconds(),
	}

	return result, fmt.Errorf("not implemented")
}

// GetCurrentModel returns the cached model, or rebuilds if not cached
func (t *IncrementalTrainer) GetCurrentModel() (*classifier.NaiveBayesModel, error) {
	if t.modelCache != nil {
		return t.modelCache, nil
	}

	return t.RebuildModel()
}

// GetTrainingStats returns statistics about the current training data
func (t *IncrementalTrainer) GetTrainingStats() (*TrainingStats, error) {
	stats := &TrainingStats{}

	// Get total samples
	err := t.db.QueryRow("SELECT COUNT(*) FROM ml_samples").Scan(&stats.TotalSamples)
	if err != nil {
		return nil, fmt.Errorf("get total samples: %w", err)
	}

	// Get number of classes
	err = t.db.QueryRow("SELECT COUNT(*) FROM ml_class_stats").Scan(&stats.NumClasses)
	if err != nil {
		return nil, fmt.Errorf("get num classes: %w", err)
	}

	// Get vocabulary size
	err = t.db.QueryRow("SELECT COUNT(DISTINCT token) FROM ml_token_stats").Scan(&stats.VocabularySize)
	if err != nil {
		return nil, fmt.Errorf("get vocabulary size: %w", err)
	}

	// Get class distribution
	rows, err := t.db.Query(`
		SELECT class, doc_count
		FROM ml_class_stats
		ORDER BY doc_count DESC
		LIMIT 10
	`)
	if err != nil {
		return nil, fmt.Errorf("get class distribution: %w", err)
	}
	defer rows.Close()

	for rows.Next() {
		var class string
		var docCount int

		if err := rows.Scan(&class, &docCount); err != nil {
			return nil, fmt.Errorf("scan class distribution: %w", err)
		}

		stats.TopClasses = append(stats.TopClasses, ClassStat{
			Class:    class,
			DocCount: docCount,
		})
	}

	if err := rows.Err(); err != nil {
		return nil, fmt.Errorf("iterate class distribution: %w", err)
	}

	return stats, nil
}

// TrainingSample represents a single training sample
type TrainingSample struct {
	Filename string
	Class    string
}

// TrainingStats contains statistics about the training data
type TrainingStats struct {
	TotalSamples   int
	NumClasses     int
	VocabularySize int
	TopClasses     []ClassStat
}

// ClassStat represents statistics for a single class
type ClassStat struct {
	Class    string
	DocCount int
}
