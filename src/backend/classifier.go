package main

import (
	"database/sql"
	"fmt"
	"log/slog"
	"math"
	"path/filepath"
	"regexp"
	"strings"
	"sync"
)

var (
	bracketRegex      = regexp.MustCompile(`(?i)\[.*?\]|\(.*?\)|\{.*?\}`)
	animeEpisodeRegex = regexp.MustCompile(`(?i)\s+-\s+\d+\b`)
	learnMu           sync.Mutex
)

// Precompiled regular expressions for episode and season markers
var seasonEpisodeRegexes = []*regexp.Regexp{
	regexp.MustCompile(`(?i)\bs\d{1,2}e\d{1,3}\b`),
	regexp.MustCompile(`(?i)\b\d{1,2}x\d{2,3}\b`),
	regexp.MustCompile(`(?i)\bep\d{1,3}\b`),
	regexp.MustCompile(`(?i)\bseason\s*\d{1,2}\b`),
	regexp.MustCompile(`(?i)\bepisode\s*\d{1,3}\b`),
}

// O(1) hash map for quick lookups of quality tags and noise words
var noiseWords = map[string]bool{
	"1080p": true, "720p": true, "2160p": true, "4k": true,
	"x264": true, "x265": true, "h264": true, "h265": true, "hevc": true,
	"bluray": true, "web-dl": true, "webdl": true, "hdtv": true,
	"dd51": true, "dd5.1": true, "ac3": true, "aac": true, "dts": true,
	"multi": true, "ita": true, "eng": true, "sub": true, "subs": true,
	"dual": true, "audio": true, "remux": true, "xvid": true, "avi": true,
}

// CleanFilename cleans a filename and extracts the raw TV show/movie title portion.
func CleanFilename(filename string) string {
	// Remove file extension
	ext := filepath.Ext(filename)
	title := strings.TrimSuffix(filename, ext)

	// Remove anything inside brackets [] () {}
	title = bracketRegex.ReplaceAllString(title, " ")

	// Check if there is an anime-style episode number like " - 05" and truncate
	if loc := animeEpisodeRegex.FindStringIndex(title); loc != nil {
		title = title[:loc[0]]
	}

	// Replace delimiters (. _ -) with space
	title = strings.ReplaceAll(title, ".", " ")
	title = strings.ReplaceAll(title, "_", " ")
	title = strings.ReplaceAll(title, "-", " ")

	// Find the earliest index of any season or episode marker to truncate
	earliestMarkerIndex := -1
	for _, re := range seasonEpisodeRegexes {
		loc := re.FindStringIndex(title)
		if loc != nil {
			if earliestMarkerIndex == -1 || loc[0] < earliestMarkerIndex {
				earliestMarkerIndex = loc[0]
			}
		}
	}

	if earliestMarkerIndex != -1 {
		title = title[:earliestMarkerIndex]
	}

	// Lowercase and split into tokens to filter noise words
	words := strings.Fields(strings.ToLower(title))
	var cleanedWords []string

	for _, word := range words {
		// Strip brackets or parentheses around words (for any single characters left)
		cleanedWord := strings.Trim(word, "()[]{}")
		if cleanedWord != "" && !noiseWords[cleanedWord] {
			cleanedWords = append(cleanedWords, cleanedWord)
		}
	}

	return strings.Join(cleanedWords, " ")
}

// tokenize splits a cleaned string into single words/tokens for classification
func tokenize(title string) []string {
	words := strings.Fields(strings.ToLower(title))
	var tokens []string
	for _, w := range words {
		w = strings.Trim(w, `!@#$%^&*()_+{}|:"<>?-=[]\;',./`)
		if len(w) >= 2 { // Skip extremely short words
			tokens = append(tokens, w)
		}
	}
	return tokens
}

// JaroWinkler computes the Jaro-Winkler string similarity between s1 and s2.
func JaroWinkler(s1, s2 string) float64 {
	r1 := []rune(strings.ToLower(strings.TrimSpace(s1)))
	r2 := []rune(strings.ToLower(strings.TrimSpace(s2)))

	len1 := len(r1)
	len2 := len(r2)

	if len1 == 0 && len2 == 0 {
		return 1.0
	}
	if len1 == 0 || len2 == 0 {
		return 0.0
	}

	// Match window
	matchWindow := maxVal(len1, len2)/2 - 1
	if matchWindow < 0 {
		matchWindow = 0
	}

	r1Matches := make([]bool, len1)
	r2Matches := make([]bool, len2)

	matches := 0
	for i := 0; i < len1; i++ {
		start := maxVal(0, i-matchWindow)
		end := minVal(len2-1, i+matchWindow)

		for j := start; j <= end; j++ {
			if r2Matches[j] {
				continue
			}
			if r1[i] == r2[j] {
				r1Matches[i] = true
				r2Matches[j] = true
				matches++
				break
			}
		}
	}

	if matches == 0 {
		return 0.0
	}

	// Transpositions
	transpositions := 0
	k := 0
	for i := 0; i < len1; i++ {
		if !r1Matches[i] {
			continue
		}
		for !r2Matches[k] {
			k++
		}
		if r1[i] != r2[k] {
			transpositions++
		}
		k++
	}

	m := float64(matches)
	t := float64(transpositions) / 2.0

	jaro := (m/float64(len1) + m/float64(len2) + (m-t)/m) / 3.0

	// Winkler prefix adjustment
	prefixLength := 0
	for i := 0; i < minVal(4, minVal(len1, len2)); i++ {
		if r1[i] == r2[i] {
			prefixLength++
		} else {
			break
		}
	}

	p := 0.1
	return jaro + float64(prefixLength)*p*(1.0-jaro)
}

func minVal(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func maxVal(a, b int) int {
	if a > b {
		return a
	}
	return b
}

// EnsureModelSchema ensures the model_word_frequencies table is initialized
func EnsureModelSchema(db *sql.DB) error {
	schema := `
	CREATE TABLE IF NOT EXISTS model_word_frequencies (
		Word TEXT NOT NULL,
		Category TEXT NOT NULL,
		Count INTEGER NOT NULL DEFAULT 1,
		PRIMARY KEY (Word, Category)
	);
	CREATE INDEX IF NOT EXISTS idx_model_word_frequencies_word ON model_word_frequencies(Word);
	`
	_, err := db.Exec(schema)
	return err
}

// GetKnownCategories retrieves all distinct categories currently stored in database.
func GetKnownCategories(db *sql.DB) ([]string, error) {
	rows, err := db.Query("SELECT DISTINCT Category FROM TrackedFiles WHERE Category IS NOT NULL AND Category != '' AND IsActive = 1")
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var categories []string
	for rows.Next() {
		var cat string
		if err := rows.Scan(&cat); err == nil {
			categories = append(categories, cat)
		}
	}
	return categories, nil
}

// ClassifyNaiveBayes predicts the category of a cleaned title based on SQLite count statistics
func ClassifyNaiveBayes(db *sql.DB, cleanedTitle string) (string, float64, error) {
	tokens := tokenize(cleanedTitle)
	if len(tokens) == 0 {
		return "", 0.0, nil
	}

	// 1. Get word sum counts per category
	categorySums := make(map[string]int64)
	var totalWords int64 = 0
	rows, err := db.Query("SELECT Category, SUM(Count) FROM model_word_frequencies GROUP BY Category")
	if err != nil {
		return "", 0.0, err
	}
	defer rows.Close()

	for rows.Next() {
		var cat string
		var sum int64
		if err := rows.Scan(&cat, &sum); err != nil {
			return "", 0.0, err
		}
		categorySums[cat] = sum
		totalWords += sum
	}

	if len(categorySums) == 0 {
		return "", 0.0, nil // No training data yet
	}

	// 2. Get Vocabulary Size |V|
	var vocabSize int64 = 0
	err = db.QueryRow("SELECT COUNT(DISTINCT Word) FROM model_word_frequencies").Scan(&vocabSize)
	if err != nil {
		return "", 0.0, err
	}
	if vocabSize == 0 {
		vocabSize = 1
	}

	// 3. Get Frequencies for query tokens via IN clause
	placeholders := make([]string, len(tokens))
	args := make([]interface{}, len(tokens))
	for i, token := range tokens {
		placeholders[i] = "?"
		args[i] = token
	}

	query := fmt.Sprintf(
		"SELECT Word, Category, Count FROM model_word_frequencies WHERE Word IN (%s)",
		strings.Join(placeholders, ","),
	)

	freqRows, err := db.Query(query, args...)
	if err != nil {
		return "", 0.0, err
	}
	defer freqRows.Close()

	freqs := make(map[string]map[string]int64)
	for freqRows.Next() {
		var word string
		var cat string
		var count int64
		if err := freqRows.Scan(&word, &cat, &count); err != nil {
			return "", 0.0, err
		}
		if freqs[word] == nil {
			freqs[word] = make(map[string]int64)
		}
		freqs[word][cat] = count
	}

	// 4. Calculate posterior probabilities in log-space
	bestCategory := ""
	bestLogProb := -1000000.0
	categoryScores := make(map[string]float64)

	var alpha float64 = 1.0 // Laplace smoothing

	for cat, catSum := range categorySums {
		// Prior probability P(C)
		priorProb := float64(catSum) / float64(totalWords)
		logProb := math.Log(priorProb)

		for _, token := range tokens {
			count := int64(0)
			if tokenFreqs, ok := freqs[token]; ok {
				if c, exists := tokenFreqs[cat]; exists {
					count = c
				}
			}

			// P(W_i | C) = (count + alpha) / (catSum + alpha * vocabSize)
			condProb := (float64(count) + alpha) / (float64(catSum) + alpha*float64(vocabSize))
			logProb += math.Log(condProb)
		}

		categoryScores[cat] = logProb

		if logProb > bestLogProb || bestCategory == "" {
			bestLogProb = logProb
			bestCategory = cat
		}
	}

	// Softmax to extract a normalized probability/confidence score
	var sumExp float64 = 0.0
	for _, logP := range categoryScores {
		sumExp += math.Exp(logP - bestLogProb)
	}

	confidence := 1.0 / sumExp

	return bestCategory, confidence, nil
}

// PredictCategory combines fuzzy Jaro-Winkler with Naive Bayes to classify a file name
func PredictCategory(db *sql.DB, filename string) (string, float64, error) {
	cleaned := CleanFilename(filename)
	if cleaned == "" {
		return "UNKNOWN", 0.0, nil
	}

	// 1. Try Jaro-Winkler on known TV show folders
	knownCats, err := GetKnownCategories(db)
	if err == nil && len(knownCats) > 0 {
		bestCat := ""
		bestScore := 0.0

		for _, cat := range knownCats {
			score := JaroWinkler(cleaned, cat)
			if score > bestScore {
				bestScore = score
				bestCat = cat
			}
		}

		// High confidence match threshold (0.88)
		if bestScore >= 0.88 {
			slog.Debug("Fuzzy classification matched successfully", "filename", filename, "category", bestCat, "score", bestScore)
			return bestCat, bestScore, nil
		}
	}

	// 2. Fall back to Naive Bayes statistics
	cat, conf, err := ClassifyNaiveBayes(db, cleaned)
	if err == nil && cat != "" {
		slog.Debug("Naive Bayes classification matched successfully", "filename", filename, "category", cat, "confidence", conf)
		return cat, conf, nil
	}

	return "UNKNOWN", 0.0, nil
}

// LearnClassification updates the Naive Bayes token frequency weights on confirming user category (async)
func LearnClassification(db *sql.DB, cleanedTitle string, confirmedCategory string) {
	tokens := tokenize(cleanedTitle)
	if len(tokens) == 0 || confirmedCategory == "" {
		return
	}

	// Increment weights in non-blocking background transaction
	go func() {
		learnMu.Lock()
		defer learnMu.Unlock()

		tx, err := db.Begin()
		if err != nil {
			slog.Error("Failed to begin training transaction", "err", err)
			return
		}
		defer tx.Rollback()

		for _, token := range tokens {
			_, err = tx.Exec(`
				INSERT INTO model_word_frequencies (Word, Category, Count)
				VALUES (?, ?, 1)
				ON CONFLICT(Word, Category) DO UPDATE SET Count = Count + 1
			`, token, confirmedCategory)
			if err != nil {
				slog.Error("Failed to update token weight", "token", token, "category", confirmedCategory, "err", err)
				return
			}
		}

		if err := tx.Commit(); err != nil {
			slog.Error("Failed to commit training transaction", "err", err)
		} else {
			slog.Info("Successfully reinforced ML model weights", "category", confirmedCategory, "tokens", len(tokens))
		}
	}()
}

// RetrainModel resets the token frequency counts and re-learns from all confirmed historical files in database.
func RetrainModel(db *sql.DB) error {
	learnMu.Lock()
	defer learnMu.Unlock()

	tx, err := db.Begin()
	if err != nil {
		return fmt.Errorf("failed to start transaction: %w", err)
	}
	defer tx.Rollback()

	// 1. Reset current model weights
	_, err = tx.Exec("DELETE FROM model_word_frequencies")
	if err != nil {
		return fmt.Errorf("failed to reset frequencies: %w", err)
	}

	// 2. Fetch all successfully moved files with confirmed categories
	rows, err := tx.Query("SELECT FileName, Category FROM TrackedFiles WHERE Status = 5 AND Category IS NOT NULL AND Category != ''")
	if err != nil {
		return fmt.Errorf("failed to query history files: %w", err)
	}
	defer rows.Close()

	var fileCount int = 0
	var totalTokens int = 0

	for rows.Next() {
		var fileName string
		var category string
		if err := rows.Scan(&fileName, &category); err != nil {
			return fmt.Errorf("failed to scan row: %w", err)
		}

		cleaned := CleanFilename(fileName)
		tokens := tokenize(cleaned)
		if len(tokens) == 0 {
			continue
		}

		for _, token := range tokens {
			_, err = tx.Exec(`
				INSERT INTO model_word_frequencies (Word, Category, Count)
				VALUES (?, ?, 1)
				ON CONFLICT(Word, Category) DO UPDATE SET Count = Count + 1
			`, token, category)
			if err != nil {
				return fmt.Errorf("failed to upsert token '%s': %w", token, err)
			}
			totalTokens++
		}
		fileCount++
	}

	if err := tx.Commit(); err != nil {
		return fmt.Errorf("failed to commit retrain transaction: %w", err)
	}

	slog.Info("Naive Bayes statistical model retrained successfully", "filesAnalyzed", fileCount, "tokensRegistered", totalTokens)
	return nil
}
