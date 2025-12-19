package classifier

import (
	"math"
	"testing"
)

const epsilon = 1e-6

func floatEquals(a, b float64) bool {
	return math.Abs(a-b) < epsilon
}

// TestNewNaiveBayesModel tests model initialization
func TestNewNaiveBayesModel(t *testing.T) {
	model := NewNaiveBayesModel()

	if model == nil {
		t.Fatal("NewNaiveBayesModel() returned nil")
	}

	if model.Alpha != 1.0 {
		t.Errorf("NewNaiveBayesModel() Alpha = %v, want 1.0", model.Alpha)
	}

	if len(model.Classes) != 0 {
		t.Errorf("NewNaiveBayesModel() Classes = %v, want empty", model.Classes)
	}
}

// TestPredictTopN_ToyDataset tests prediction on a simple dataset
func TestPredictTopN_ToyDataset(t *testing.T) {
	// Create a simple model manually
	model := &NaiveBayesModel{
		Classes: []string{"BREAKING BAD", "THE OFFICE"},
		Priors: map[string]float64{
			"BREAKING BAD": 0.5,
			"THE OFFICE":   0.5,
		},
		Weights: map[string]map[string]float64{
			"BREAKING BAD": {
				"breaking": 0.4,
				"bad":      0.4,
				"series":   0.2,
			},
			"THE OFFICE": {
				"the":    0.3,
				"office": 0.3,
				"series": 0.4,
			},
		},
		IDF:       make(map[string]float64),
		Alpha:     1.0,
		VocabSize: 5,
	}

	// Test document: "breaking bad"
	tfidf := map[string]float64{
		"breaking": 0.5,
		"bad":      0.5,
	}

	predictions := model.PredictTopN(tfidf, 2)

	// Should have 2 predictions
	if len(predictions) != 2 {
		t.Fatalf("PredictTopN() returned %d predictions, want 2", len(predictions))
	}

	// Top prediction should be "BREAKING BAD"
	if predictions[0].Class != "BREAKING BAD" {
		t.Errorf("PredictTopN() top class = %v, want BREAKING BAD", predictions[0].Class)
	}

	// Confidence should be higher for "BREAKING BAD" than "THE OFFICE"
	if predictions[0].Confidence <= predictions[1].Confidence {
		t.Errorf("PredictTopN() BREAKING BAD confidence (%v) should be > THE OFFICE (%v)",
			predictions[0].Confidence, predictions[1].Confidence)
	}

	// Confidences should sum to ~1.0
	totalConfidence := predictions[0].Confidence + predictions[1].Confidence
	if !floatEquals(totalConfidence, 1.0) {
		t.Errorf("PredictTopN() total confidence = %v, want 1.0", totalConfidence)
	}
}

// TestPredict tests single prediction
func TestPredict(t *testing.T) {
	model := &NaiveBayesModel{
		Classes: []string{"CAT", "DOG"},
		Priors: map[string]float64{
			"CAT": 0.6,
			"DOG": 0.4,
		},
		Weights: map[string]map[string]float64{
			"CAT": {
				"meow":    0.7,
				"purr":    0.2,
				"scratch": 0.1,
			},
			"DOG": {
				"bark": 0.6,
				"wag":  0.3,
				"bone": 0.1,
			},
		},
		IDF:       make(map[string]float64),
		Alpha:     1.0,
		VocabSize: 6,
	}

	// Test "meow"
	tfidf := map[string]float64{
		"meow": 1.0,
	}

	class, confidence, err := model.Predict(tfidf)
	if err != nil {
		t.Fatalf("Predict() error = %v", err)
	}

	if class != "CAT" {
		t.Errorf("Predict() class = %v, want CAT", class)
	}

	if confidence <= 0 || confidence > 1 {
		t.Errorf("Predict() confidence = %v, want between 0 and 1", confidence)
	}

	t.Logf("Predicted: %s (confidence: %.4f)", class, confidence)
}

// TestPredictProba tests probability distribution
func TestPredictProba(t *testing.T) {
	model := &NaiveBayesModel{
		Classes: []string{"A", "B", "C"},
		Priors: map[string]float64{
			"A": 0.33,
			"B": 0.33,
			"C": 0.34,
		},
		Weights: map[string]map[string]float64{
			"A": {"foo": 0.8, "bar": 0.2},
			"B": {"foo": 0.5, "bar": 0.5},
			"C": {"foo": 0.2, "bar": 0.8},
		},
		IDF:       make(map[string]float64),
		Alpha:     1.0,
		VocabSize: 2,
	}

	tfidf := map[string]float64{
		"foo": 0.7,
		"bar": 0.3,
	}

	proba := model.PredictProba(tfidf)

	// Should have probabilities for all 3 classes
	if len(proba) != 3 {
		t.Errorf("PredictProba() returned %d classes, want 3", len(proba))
	}

	// Probabilities should sum to ~1.0
	var totalProba float64
	for _, p := range proba {
		totalProba += p
	}

	if !floatEquals(totalProba, 1.0) {
		t.Errorf("PredictProba() total probability = %v, want 1.0", totalProba)
	}

	t.Logf("Probabilities: A=%.4f, B=%.4f, C=%.4f", proba["A"], proba["B"], proba["C"])
}

// TestGetFeatureImportance tests feature importance extraction
func TestGetFeatureImportance(t *testing.T) {
	model := &NaiveBayesModel{
		Classes: []string{"SERIES"},
		Priors: map[string]float64{
			"SERIES": 1.0,
		},
		Weights: map[string]map[string]float64{
			"SERIES": {
				"breaking": 0.5,
				"bad":      0.3,
				"series":   0.2,
			},
		},
		Alpha:     1.0,
		VocabSize: 3,
	}

	importance := model.GetFeatureImportance("SERIES", 3)

	// Should return 3 features
	if len(importance) != 3 {
		t.Fatalf("GetFeatureImportance() returned %d features, want 3", len(importance))
	}

	// Should be sorted by weight (descending)
	if importance[0].Token != "breaking" {
		t.Errorf("GetFeatureImportance() top token = %v, want breaking", importance[0].Token)
	}

	if importance[0].Weight < importance[1].Weight {
		t.Errorf("GetFeatureImportance() not sorted: %v < %v", importance[0].Weight, importance[1].Weight)
	}

	t.Logf("Top features for SERIES:")
	for i, feat := range importance {
		t.Logf("  %d. %s (weight: %.4f)", i+1, feat.Token, feat.Weight)
	}
}

// TestGetFeatureImportance_NonexistentClass tests feature importance for missing class
func TestGetFeatureImportance_NonexistentClass(t *testing.T) {
	model := NewNaiveBayesModel()
	model.Classes = []string{"A"}
	model.Weights = map[string]map[string]float64{
		"A": {"foo": 0.5},
	}

	importance := model.GetFeatureImportance("NONEXISTENT", 10)

	if len(importance) != 0 {
		t.Errorf("GetFeatureImportance() for nonexistent class = %v, want empty", importance)
	}
}

// TestGetters tests getter methods
func TestGetters(t *testing.T) {
	model := &NaiveBayesModel{
		Classes: []string{"A", "B"},
		Priors: map[string]float64{
			"A": 0.6,
			"B": 0.4,
		},
		Weights: map[string]map[string]float64{
			"A": {"foo": 0.5},
		},
	}

	// Test GetClasses
	classes := model.GetClasses()
	if len(classes) != 2 {
		t.Errorf("GetClasses() = %v, want 2 classes", classes)
	}

	// Test GetPrior
	prior, exists := model.GetPrior("A")
	if !exists {
		t.Error("GetPrior('A') not found")
	}
	if !floatEquals(prior, 0.6) {
		t.Errorf("GetPrior('A') = %v, want 0.6", prior)
	}

	// Test GetPrior for nonexistent class
	_, exists = model.GetPrior("C")
	if exists {
		t.Error("GetPrior('C') should not exist")
	}

	// Test GetTokenWeight
	weight, exists := model.GetTokenWeight("A", "foo")
	if !exists {
		t.Error("GetTokenWeight('A', 'foo') not found")
	}
	if !floatEquals(weight, 0.5) {
		t.Errorf("GetTokenWeight('A', 'foo') = %v, want 0.5", weight)
	}

	// Test GetTokenWeight for nonexistent token
	_, exists = model.GetTokenWeight("A", "bar")
	if exists {
		t.Error("GetTokenWeight('A', 'bar') should not exist")
	}
}

// TestPredict_EmptyModel tests prediction with untrained model
func TestPredict_EmptyModel(t *testing.T) {
	model := NewNaiveBayesModel()

	tfidf := map[string]float64{"foo": 1.0}

	_, _, err := model.Predict(tfidf)
	if err == nil {
		t.Error("Predict() on empty model should return error")
	}
}

// TestPredictTopN_EmptyTFIDF tests prediction with empty input
func TestPredictTopN_EmptyTFIDF(t *testing.T) {
	model := &NaiveBayesModel{
		Classes: []string{"A", "B"},
		Priors: map[string]float64{
			"A": 0.5,
			"B": 0.5,
		},
		Weights: map[string]map[string]float64{
			"A": {"foo": 0.5},
			"B": {"bar": 0.5},
		},
		Alpha:     1.0,
		VocabSize: 2,
	}

	// Empty TF-IDF should still return predictions based on priors
	tfidf := map[string]float64{}

	predictions := model.PredictTopN(tfidf, 2)

	if len(predictions) != 2 {
		t.Errorf("PredictTopN() with empty TF-IDF returned %d predictions, want 2", len(predictions))
	}

	// With equal priors and no evidence, confidence should be roughly equal
	if math.Abs(predictions[0].Confidence-predictions[1].Confidence) > 0.1 {
		t.Logf("PredictTopN() with empty TF-IDF: A=%.4f, B=%.4f",
			predictions[0].Confidence, predictions[1].Confidence)
	}
}

// TestMultiClassPrediction tests prediction with multiple classes
func TestMultiClassPrediction(t *testing.T) {
	// Simulate 5 TV series
	model := &NaiveBayesModel{
		Classes: []string{
			"BREAKING BAD",
			"THE OFFICE",
			"GAME OF THRONES",
			"ONE PIECE",
			"ATTACK ON TITAN",
		},
		Priors: map[string]float64{
			"BREAKING BAD":    0.2,
			"THE OFFICE":      0.2,
			"GAME OF THRONES": 0.2,
			"ONE PIECE":       0.2,
			"ATTACK ON TITAN": 0.2,
		},
		Weights: map[string]map[string]float64{
			"BREAKING BAD": {
				"breaking": 0.4,
				"bad":      0.4,
				"heisenberg": 0.2,
			},
			"THE OFFICE": {
				"the":    0.3,
				"office": 0.4,
				"dundermifflin": 0.3,
			},
			"GAME OF THRONES": {
				"game":    0.3,
				"thrones": 0.3,
				"westeros": 0.4,
			},
			"ONE PIECE": {
				"one":   0.4,
				"piece": 0.4,
				"luffy": 0.2,
			},
			"ATTACK ON TITAN": {
				"attack": 0.3,
				"titan":  0.4,
				"eren":   0.3,
			},
		},
		IDF:       make(map[string]float64),
		Alpha:     1.0,
		VocabSize: 15,
	}

	// Test: "breaking bad"
	tfidf := map[string]float64{
		"breaking": 0.5,
		"bad":      0.5,
	}

	predictions := model.PredictTopN(tfidf, 3)

	// Top prediction should be "BREAKING BAD"
	if predictions[0].Class != "BREAKING BAD" {
		t.Errorf("Predict() top class = %v, want BREAKING BAD", predictions[0].Class)
	}

	// Should return top 3
	if len(predictions) != 3 {
		t.Errorf("PredictTopN(3) returned %d predictions, want 3", len(predictions))
	}

	t.Logf("Top 3 predictions for 'breaking bad':")
	for i, pred := range predictions {
		t.Logf("  %d. %s (%.4f)", i+1, pred.Class, pred.Confidence)
	}
}

// BenchmarkPredict benchmarks prediction performance
func BenchmarkPredict(b *testing.B) {
	model := &NaiveBayesModel{
		Classes: []string{"A", "B", "C"},
		Priors: map[string]float64{
			"A": 0.33,
			"B": 0.33,
			"C": 0.34,
		},
		Weights: map[string]map[string]float64{
			"A": {"foo": 0.5, "bar": 0.3, "baz": 0.2},
			"B": {"foo": 0.3, "bar": 0.5, "baz": 0.2},
			"C": {"foo": 0.2, "bar": 0.2, "baz": 0.6},
		},
		IDF:       make(map[string]float64),
		Alpha:     1.0,
		VocabSize: 3,
	}

	tfidf := map[string]float64{
		"foo": 0.5,
		"bar": 0.3,
		"baz": 0.2,
	}

	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		_, _, _ = model.Predict(tfidf)
	}
}
