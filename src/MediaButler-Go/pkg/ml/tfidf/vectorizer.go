package tfidf

import (
	"database/sql"
	"fmt"
	"math"
)

// TFIDFVectorizer implements TF-IDF (Term Frequency-Inverse Document Frequency) vectorization
// Compatible with sklearn's TfidfVectorizer formula
type TFIDFVectorizer struct {
	// IDF scores: token → IDF value
	// Formula: IDF(token) = log((1 + num_docs) / (1 + doc_freq(token))) + 1
	idf map[string]float64

	// Total number of documents used to compute IDF
	numDocs int

	// Vocabulary: all unique tokens seen during fit
	vocabulary map[string]bool
}

// NewTFIDFVectorizer creates a new TF-IDF vectorizer instance
func NewTFIDFVectorizer() *TFIDFVectorizer {
	return &TFIDFVectorizer{
		idf:        make(map[string]float64),
		vocabulary: make(map[string]bool),
		numDocs:    0,
	}
}

// ComputeTF calculates term frequency for a list of tokens
// Formula: TF(token) = count(token) / total_tokens
func ComputeTF(tokens []string) map[string]float64 {
	if len(tokens) == 0 {
		return make(map[string]float64)
	}

	// Count token occurrences
	counts := make(map[string]int)
	for _, token := range tokens {
		counts[token]++
	}

	// Calculate TF: term_count / total_tokens
	tf := make(map[string]float64)
	totalTokens := float64(len(tokens))

	for token, count := range counts {
		tf[token] = float64(count) / totalTokens
	}

	return tf
}

// FitFromSQLite computes IDF scores from the ml_idf_stats table
// This reads document frequency statistics from SQLite
func (v *TFIDFVectorizer) FitFromSQLite(db *sql.DB) error {
	// Query to get total number of documents
	var numDocs int
	err := db.QueryRow("SELECT SUM(doc_count) FROM ml_class_stats").Scan(&numDocs)
	if err != nil {
		return fmt.Errorf("get total documents: %w", err)
	}

	if numDocs == 0 {
		return fmt.Errorf("no documents found in database")
	}

	v.numDocs = numDocs

	// Query to get document frequency for each token
	rows, err := db.Query("SELECT token, doc_freq FROM ml_idf_stats")
	if err != nil {
		return fmt.Errorf("query idf stats: %w", err)
	}
	defer rows.Close()

	// Compute IDF for each token
	// Formula: IDF(token) = log((1 + num_docs) / (1 + doc_freq)) + 1
	for rows.Next() {
		var token string
		var docFreq int

		if err := rows.Scan(&token, &docFreq); err != nil {
			return fmt.Errorf("scan idf row: %w", err)
		}

		// sklearn-compatible IDF formula
		idf := math.Log(float64(1+v.numDocs)/float64(1+docFreq)) + 1.0
		v.idf[token] = idf
		v.vocabulary[token] = true
	}

	if err := rows.Err(); err != nil {
		return fmt.Errorf("iterate idf rows: %w", err)
	}

	return nil
}

// FitFromDocuments computes IDF scores from a list of document token lists
// This is used for training without SQLite (in-memory computation)
func (v *TFIDFVectorizer) FitFromDocuments(documents [][]string) error {
	if len(documents) == 0 {
		return fmt.Errorf("no documents provided")
	}

	v.numDocs = len(documents)

	// Count document frequency for each token
	docFreq := make(map[string]int)

	for _, doc := range documents {
		// Use set to count each token only once per document
		seen := make(map[string]bool)
		for _, token := range doc {
			if !seen[token] {
				docFreq[token]++
				seen[token] = true
			}
		}
	}

	// Compute IDF for each token
	// Formula: IDF(token) = log((1 + num_docs) / (1 + doc_freq)) + 1
	for token, freq := range docFreq {
		idf := math.Log(float64(1+v.numDocs)/float64(1+freq)) + 1.0
		v.idf[token] = idf
		v.vocabulary[token] = true
	}

	return nil
}

// Transform converts tokens to TF-IDF weighted vector
// Returns a sparse representation (map of token → TF-IDF score)
func (v *TFIDFVectorizer) Transform(tokens []string) map[string]float64 {
	if len(tokens) == 0 {
		return make(map[string]float64)
	}

	// Compute TF (term frequency)
	tf := ComputeTF(tokens)

	// Compute TF-IDF: TF(token) × IDF(token)
	tfidf := make(map[string]float64)

	for token, tfValue := range tf {
		// Get IDF value (default to 0 if token not in vocabulary)
		idfValue, exists := v.idf[token]
		if !exists {
			// Unknown token: use smoothed IDF
			// This handles tokens not seen during training
			idfValue = math.Log(float64(1+v.numDocs)/1.0) + 1.0
		}

		// TF-IDF = TF × IDF
		tfidf[token] = tfValue * idfValue
	}

	return tfidf
}

// FitTransform is a convenience method that fits and transforms in one step
func (v *TFIDFVectorizer) FitTransform(documents [][]string) ([]map[string]float64, error) {
	// Fit IDF from documents
	if err := v.FitFromDocuments(documents); err != nil {
		return nil, err
	}

	// Transform all documents
	results := make([]map[string]float64, len(documents))
	for i, doc := range documents {
		results[i] = v.Transform(doc)
	}

	return results, nil
}

// GetIDF returns the IDF score for a specific token
func (v *TFIDFVectorizer) GetIDF(token string) (float64, bool) {
	idf, exists := v.idf[token]
	return idf, exists
}

// GetVocabularySize returns the number of unique tokens in vocabulary
func (v *TFIDFVectorizer) GetVocabularySize() int {
	return len(v.vocabulary)
}

// GetNumDocs returns the number of documents used to compute IDF
func (v *TFIDFVectorizer) GetNumDocs() int {
	return v.numDocs
}

// Vocabulary returns all tokens in the vocabulary
func (v *TFIDFVectorizer) Vocabulary() []string {
	tokens := make([]string, 0, len(v.vocabulary))
	for token := range v.vocabulary {
		tokens = append(tokens, token)
	}
	return tokens
}

// L2Normalize applies L2 normalization to a TF-IDF vector
// This is commonly used in text classification to make vectors unit length
func L2Normalize(tfidf map[string]float64) map[string]float64 {
	if len(tfidf) == 0 {
		return tfidf
	}

	// Compute L2 norm (Euclidean length)
	var sumSquares float64
	for _, value := range tfidf {
		sumSquares += value * value
	}

	norm := math.Sqrt(sumSquares)
	if norm == 0 {
		return tfidf
	}

	// Normalize each value by dividing by the norm
	normalized := make(map[string]float64)
	for token, value := range tfidf {
		normalized[token] = value / norm
	}

	return normalized
}

// CosineSimilarity computes cosine similarity between two TF-IDF vectors
// Returns a value between 0 (no similarity) and 1 (identical)
func CosineSimilarity(vec1, vec2 map[string]float64) float64 {
	if len(vec1) == 0 || len(vec2) == 0 {
		return 0.0
	}

	// Compute dot product and magnitudes
	var dotProduct, mag1, mag2 float64

	for token, value1 := range vec1 {
		mag1 += value1 * value1

		if value2, exists := vec2[token]; exists {
			dotProduct += value1 * value2
		}
	}

	for _, value2 := range vec2 {
		mag2 += value2 * value2
	}

	// Avoid division by zero
	magnitude := math.Sqrt(mag1) * math.Sqrt(mag2)
	if magnitude == 0 {
		return 0.0
	}

	return dotProduct / magnitude
}
