package classifier

import (
	"database/sql"
	"fmt"
	"math"
	"sort"
)

// NaiveBayesModel represents a trained Multinomial Naive Bayes classifier
// Compatible with sklearn's MultinomialNB for text classification
type NaiveBayesModel struct {
	// Classes: list of all categories (e.g., ["BREAKING BAD", "THE OFFICE", ...])
	Classes []string

	// Priors: P(class) for each class
	// Formula: P(class) = doc_count(class) / total_docs
	Priors map[string]float64

	// Weights: P(token|class) for each token in each class
	// Formula: P(token|class) = (token_count + α) / (total_tokens_in_class + α × vocab_size)
	// where α is the smoothing parameter (Laplace smoothing)
	Weights map[string]map[string]float64 // class → token → probability

	// IDF: Inverse Document Frequency scores (for TF-IDF weighting)
	IDF map[string]float64

	// Alpha: Laplace smoothing parameter (default: 1.0)
	Alpha float64

	// VocabSize: Total number of unique tokens in vocabulary
	VocabSize int
}

// Prediction represents a classification prediction with confidence
type Prediction struct {
	Class      string  // Predicted class name
	Confidence float64 // Confidence score (0.0 to 1.0)
	LogProb    float64 // Log probability (for debugging)
}

// NewNaiveBayesModel creates a new Naive Bayes model with default parameters
func NewNaiveBayesModel() *NaiveBayesModel {
	return &NaiveBayesModel{
		Classes: []string{},
		Priors:  make(map[string]float64),
		Weights: make(map[string]map[string]float64),
		IDF:     make(map[string]float64),
		Alpha:   1.0, // Laplace smoothing (add-one smoothing)
	}
}

// TrainFromSQLite trains the model from SQLite statistics tables
// Expects tables: ml_class_stats, ml_token_stats, ml_idf_stats
func TrainFromSQLite(db *sql.DB) (*NaiveBayesModel, error) {
	model := NewNaiveBayesModel()

	// Step 1: Load class priors and get total documents
	var totalDocs int
	classDocCounts := make(map[string]int)

	rows, err := db.Query("SELECT class, doc_count FROM ml_class_stats")
	if err != nil {
		return nil, fmt.Errorf("query class stats: %w", err)
	}
	defer rows.Close()

	for rows.Next() {
		var class string
		var docCount int

		if err := rows.Scan(&class, &docCount); err != nil {
			return nil, fmt.Errorf("scan class stats: %w", err)
		}

		model.Classes = append(model.Classes, class)
		classDocCounts[class] = docCount
		totalDocs += docCount
	}

	if err := rows.Err(); err != nil {
		return nil, fmt.Errorf("iterate class stats: %w", err)
	}

	if totalDocs == 0 {
		return nil, fmt.Errorf("no training documents found")
	}

	// Compute priors: P(class) = doc_count(class) / total_docs
	for class, docCount := range classDocCounts {
		model.Priors[class] = float64(docCount) / float64(totalDocs)
	}

	// Step 2: Load token statistics and compute P(token|class)
	// Get total tokens per class
	totalTokensPerClass := make(map[string]int)

	rows2, err := db.Query("SELECT class, SUM(count) as total FROM ml_token_stats GROUP BY class")
	if err != nil {
		return nil, fmt.Errorf("query total tokens per class: %w", err)
	}
	defer rows2.Close()

	for rows2.Next() {
		var class string
		var total int

		if err := rows2.Scan(&class, &total); err != nil {
			return nil, fmt.Errorf("scan total tokens: %w", err)
		}

		totalTokensPerClass[class] = total
	}

	if err := rows2.Err(); err != nil {
		return nil, fmt.Errorf("iterate total tokens: %w", err)
	}

	// Get vocabulary size (distinct tokens across all classes)
	var vocabSize int
	err = db.QueryRow("SELECT COUNT(DISTINCT token) FROM ml_token_stats").Scan(&vocabSize)
	if err != nil {
		return nil, fmt.Errorf("get vocabulary size: %w", err)
	}

	model.VocabSize = vocabSize

	// Load all token-class probabilities
	rows3, err := db.Query("SELECT class, token, count FROM ml_token_stats")
	if err != nil {
		return nil, fmt.Errorf("query token stats: %w", err)
	}
	defer rows3.Close()

	for rows3.Next() {
		var class, token string
		var count int

		if err := rows3.Scan(&class, &token, &count); err != nil {
			return nil, fmt.Errorf("scan token stats: %w", err)
		}

		// Initialize class weights map if needed
		if model.Weights[class] == nil {
			model.Weights[class] = make(map[string]float64)
		}

		// Compute P(token|class) with Laplace smoothing
		// Formula: (token_count + α) / (total_tokens_in_class + α × vocab_size)
		numerator := float64(count) + model.Alpha
		denominator := float64(totalTokensPerClass[class]) + (model.Alpha * float64(model.VocabSize))

		model.Weights[class][token] = numerator / denominator
	}

	if err := rows3.Err(); err != nil {
		return nil, fmt.Errorf("iterate token stats: %w", err)
	}

	// Step 3: Load IDF scores (optional, for TF-IDF weighting)
	rows4, err := db.Query("SELECT token, doc_freq FROM ml_idf_stats")
	if err != nil {
		// IDF is optional, so don't fail if table doesn't exist
		return model, nil
	}
	defer rows4.Close()

	for rows4.Next() {
		var token string
		var docFreq int

		if err := rows4.Scan(&token, &docFreq); err != nil {
			return nil, fmt.Errorf("scan idf stats: %w", err)
		}

		// Compute IDF: log((1 + num_docs) / (1 + doc_freq)) + 1
		idf := math.Log(float64(1+totalDocs)/float64(1+docFreq)) + 1.0
		model.IDF[token] = idf
	}

	if err := rows4.Err(); err != nil {
		return nil, fmt.Errorf("iterate idf stats: %w", err)
	}

	return model, nil
}

// Predict classifies a TF-IDF vector and returns the most likely class with confidence
func (m *NaiveBayesModel) Predict(tfidf map[string]float64) (string, float64, error) {
	if len(m.Classes) == 0 {
		return "", 0.0, fmt.Errorf("model not trained (no classes)")
	}

	// Get all predictions
	predictions := m.PredictTopN(tfidf, len(m.Classes))
	if len(predictions) == 0 {
		return "", 0.0, fmt.Errorf("no predictions generated")
	}

	// Return top prediction
	top := predictions[0]
	return top.Class, top.Confidence, nil
}

// PredictTopN returns the top N most likely classes with confidence scores
func (m *NaiveBayesModel) PredictTopN(tfidf map[string]float64, n int) []Prediction {
	if len(m.Classes) == 0 {
		return []Prediction{}
	}

	// Compute log probabilities for each class
	// Formula: log P(class|doc) = log P(class) + Σ tfidf(token) × log P(token|class)
	logProbs := make(map[string]float64)

	for _, class := range m.Classes {
		// Start with log prior: log P(class)
		logProb := math.Log(m.Priors[class])

		// Add weighted token contributions
		for token, tfidfValue := range tfidf {
			// Get P(token|class), use smoothing for unseen tokens
			tokenProb, exists := m.Weights[class][token]
			if !exists {
				// Unseen token: use smoothed probability
				// P(unseen|class) = α / (total_tokens_in_class + α × vocab_size)
				// For simplicity, use a small constant
				tokenProb = m.Alpha / (float64(m.VocabSize) * m.Alpha)
			}

			// Add weighted contribution: tfidf(token) × log P(token|class)
			if tokenProb > 0 {
				logProb += tfidfValue * math.Log(tokenProb)
			}
		}

		logProbs[class] = logProb
	}

	// Convert log probabilities to probabilities and normalize
	predictions := make([]Prediction, 0, len(m.Classes))

	// Find max log prob for numerical stability (log-sum-exp trick)
	maxLogProb := math.Inf(-1)
	for _, logProb := range logProbs {
		if logProb > maxLogProb {
			maxLogProb = logProb
		}
	}

	// Compute exp(logProb - maxLogProb) and sum
	var sumProbs float64
	for class, logProb := range logProbs {
		prob := math.Exp(logProb - maxLogProb)
		predictions = append(predictions, Prediction{
			Class:      class,
			Confidence: prob, // Will normalize after sum
			LogProb:    logProb,
		})
		sumProbs += prob
	}

	// Normalize to get confidence scores (0 to 1)
	for i := range predictions {
		predictions[i].Confidence /= sumProbs
	}

	// Sort by confidence (descending)
	sort.Slice(predictions, func(i, j int) bool {
		return predictions[i].Confidence > predictions[j].Confidence
	})

	// Return top N
	if n > len(predictions) {
		n = len(predictions)
	}

	return predictions[:n]
}

// PredictProba returns probability distribution over all classes
func (m *NaiveBayesModel) PredictProba(tfidf map[string]float64) map[string]float64 {
	predictions := m.PredictTopN(tfidf, len(m.Classes))

	proba := make(map[string]float64)
	for _, pred := range predictions {
		proba[pred.Class] = pred.Confidence
	}

	return proba
}

// GetFeatureImportance returns the most important tokens for a given class
// Useful for understanding what the model learned
func (m *NaiveBayesModel) GetFeatureImportance(class string, topN int) []TokenImportance {
	weights, exists := m.Weights[class]
	if !exists {
		return []TokenImportance{}
	}

	// Collect all token weights
	importance := make([]TokenImportance, 0, len(weights))
	for token, weight := range weights {
		importance = append(importance, TokenImportance{
			Token:  token,
			Weight: weight,
		})
	}

	// Sort by weight (descending)
	sort.Slice(importance, func(i, j int) bool {
		return importance[i].Weight > importance[j].Weight
	})

	// Return top N
	if topN > len(importance) {
		topN = len(importance)
	}

	return importance[:topN]
}

// TokenImportance represents the importance of a token for a class
type TokenImportance struct {
	Token  string
	Weight float64
}

// GetClasses returns all classes in the model
func (m *NaiveBayesModel) GetClasses() []string {
	return m.Classes
}

// GetPrior returns the prior probability for a class
func (m *NaiveBayesModel) GetPrior(class string) (float64, bool) {
	prior, exists := m.Priors[class]
	return prior, exists
}

// GetTokenWeight returns P(token|class) for a specific token and class
func (m *NaiveBayesModel) GetTokenWeight(class, token string) (float64, bool) {
	classWeights, exists := m.Weights[class]
	if !exists {
		return 0.0, false
	}

	weight, exists := classWeights[token]
	return weight, exists
}
