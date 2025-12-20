package main

import (
	"database/sql"
	"encoding/csv"
	"flag"
	"fmt"
	"os"
	"sort"
	"time"

	"github.com/chim331u/mediabutler-go/pkg/ml/classifier"
	"github.com/chim331u/mediabutler-go/pkg/ml/tfidf"
	"github.com/chim331u/mediabutler-go/pkg/ml/tokenizer"
	"github.com/chim331u/mediabutler-go/pkg/ml/training"
	_ "github.com/mattn/go-sqlite3"
)

// Command-line flags
var (
	dbPath   = flag.String("db", "temp/mediabutler.dev.db", "Path to SQLite database")
	testFile = flag.String("test", "data/test_split.csv", "CSV file with test data")
	topN     = flag.Int("top-n", 1, "Consider prediction correct if true class is in top N predictions")
	verbose  = flag.Bool("verbose", false, "Show detailed per-sample results")
)

// EvaluationResult holds evaluation metrics
type EvaluationResult struct {
	TotalSamples      int
	CorrectPredictions int
	Accuracy          float64
	Top3Accuracy      float64
	AvgConfidence     float64
	AvgPredictionTime time.Duration
	ConfusionMatrix   map[string]map[string]int // true_class -> predicted_class -> count
	PerClassMetrics   map[string]ClassMetrics
}

// ClassMetrics holds per-class evaluation metrics
type ClassMetrics struct {
	TruePositives  int
	FalsePositives int
	FalseNegatives int
	Precision      float64
	Recall         float64
	F1Score        float64
}

// TestSample represents a single test sample
type TestSample struct {
	Filename  string
	TrueClass string
}

func main() {
	flag.Parse()

	// Validate flags
	if *dbPath == "" {
		fmt.Fprintln(os.Stderr, "Error: --db flag is required")
		flag.Usage()
		os.Exit(1)
	}

	if *testFile == "" {
		fmt.Fprintln(os.Stderr, "Error: --test flag is required")
		flag.Usage()
		os.Exit(1)
	}

	fmt.Println("========================================")
	fmt.Println("MediaButler Go API - ML Evaluator")
	fmt.Println("========================================")
	fmt.Println()
	fmt.Printf("Database: %s\n", *dbPath)
	fmt.Printf("Test file: %s\n", *testFile)
	fmt.Printf("Top-N accuracy: %d\n", *topN)
	fmt.Println()

	// Open database
	db, err := sql.Open("sqlite3", *dbPath)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error opening database: %v\n", err)
		os.Exit(1)
	}
	defer db.Close()

	// Load test samples
	testSamples, err := loadTestSamples(*testFile)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error loading test samples: %v\n", err)
		os.Exit(1)
	}

	fmt.Printf("Loaded %d test samples\n\n", len(testSamples))

	// Load model from database
	fmt.Println("🎯 Loading trained model from database...")
	trainer := training.NewIncrementalTrainer(db)
	model, err := trainer.GetCurrentModel()
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error loading model: %v\n", err)
		os.Exit(1)
	}

	fmt.Printf("✅ Model loaded: %d classes, %d vocabulary size\n\n", len(model.GetClasses()), model.VocabSize)

	// Create TF-IDF vectorizer
	fmt.Println("📊 Building TF-IDF vectorizer from database...")
	vectorizer := tfidf.NewTFIDFVectorizer()
	if err := vectorizer.FitFromSQLite(db); err != nil {
		fmt.Fprintf(os.Stderr, "Error building vectorizer: %v\n", err)
		os.Exit(1)
	}

	fmt.Printf("✅ Vectorizer ready: %d tokens, %d documents\n\n", vectorizer.GetVocabularySize(), vectorizer.GetNumDocs())

	// Create tokenizer
	tok := tokenizer.NewItalianTokenizer()

	// Evaluate model
	fmt.Println("🧪 Evaluating model on test set...")
	fmt.Println()

	result := evaluateModel(model, vectorizer, tok, testSamples, *topN, *verbose)

	// Print results
	printEvaluationResults(result)

	// Exit with success/failure based on 90% threshold
	if result.Accuracy >= 0.90 {
		fmt.Println()
		fmt.Println("✅ SUCCESS: Model achieves ≥90% accuracy threshold!")
		fmt.Println("   Recommendation: PROCEED with Go implementation")
		os.Exit(0)
	} else {
		fmt.Println()
		fmt.Printf("❌ BELOW THRESHOLD: Model accuracy %.2f%% < 90%%\n", result.Accuracy*100)
		fmt.Println("   Recommendation: Consider fallback to ONNX export")
		os.Exit(1)
	}
}

// loadTestSamples loads test samples from CSV file
func loadTestSamples(csvPath string) ([]TestSample, error) {
	file, err := os.Open(csvPath)
	if err != nil {
		return nil, fmt.Errorf("open CSV file: %w", err)
	}
	defer file.Close()

	reader := csv.NewReader(file)

	// Read header
	header, err := reader.Read()
	if err != nil {
		return nil, fmt.Errorf("read CSV header: %w", err)
	}

	// Validate header
	if len(header) != 2 || header[0] != "FileName" || header[1] != "Category" {
		return nil, fmt.Errorf("invalid CSV format: expected header [FileName,Category], got %v", header)
	}

	// Read all records
	records, err := reader.ReadAll()
	if err != nil {
		return nil, fmt.Errorf("read CSV records: %w", err)
	}

	samples := make([]TestSample, 0, len(records))
	for i, record := range records {
		if len(record) != 2 {
			return nil, fmt.Errorf("invalid record at line %d: expected 2 fields, got %d", i+2, len(record))
		}

		samples = append(samples, TestSample{
			Filename:  record[0],
			TrueClass: record[1],
		})
	}

	return samples, nil
}

// evaluateModel evaluates the model on test samples
func evaluateModel(
	model *classifier.NaiveBayesModel,
	vectorizer *tfidf.TFIDFVectorizer,
	tok *tokenizer.ItalianTokenizer,
	testSamples []TestSample,
	topN int,
	verbose bool,
) EvaluationResult {
	result := EvaluationResult{
		TotalSamples:    len(testSamples),
		ConfusionMatrix: make(map[string]map[string]int),
		PerClassMetrics: make(map[string]ClassMetrics),
	}

	var totalConfidence float64
	var totalPredictionTime time.Duration

	correctTop1 := 0
	correctTop3 := 0

	// Initialize confusion matrix
	for _, class := range model.GetClasses() {
		result.ConfusionMatrix[class] = make(map[string]int)
	}

	// Evaluate each sample
	for i, sample := range testSamples {
		// Tokenize
		tokens := tok.Tokenize(sample.Filename)

		// Vectorize
		tfidfVec := vectorizer.Transform(tokens)

		// Predict with timing
		startTime := time.Now()
		predictions := model.PredictTopN(tfidfVec, 3)
		predictionTime := time.Since(startTime)
		totalPredictionTime += predictionTime

		if len(predictions) == 0 {
			fmt.Printf("⚠️  Warning: No predictions for sample %d: %s\n", i+1, sample.Filename)
			continue
		}

		predictedClass := predictions[0].Class
		confidence := predictions[0].Confidence
		totalConfidence += confidence

		// Update confusion matrix
		if result.ConfusionMatrix[sample.TrueClass] == nil {
			result.ConfusionMatrix[sample.TrueClass] = make(map[string]int)
		}
		result.ConfusionMatrix[sample.TrueClass][predictedClass]++

		// Check if correct (top-1)
		isCorrectTop1 := predictedClass == sample.TrueClass
		if isCorrectTop1 {
			correctTop1++
		}

		// Check if correct (top-3)
		isCorrectTop3 := false
		for j := 0; j < len(predictions) && j < 3; j++ {
			if predictions[j].Class == sample.TrueClass {
				isCorrectTop3 = true
				break
			}
		}
		if isCorrectTop3 {
			correctTop3++
		}

		// Verbose output
		if verbose {
			status := "❌"
			if isCorrectTop1 {
				status = "✅"
			} else if isCorrectTop3 {
				status = "🟡"
			}

			fmt.Printf("%s Sample %4d: True=%s, Pred=%s (%.3f) [%s]\n",
				status, i+1, sample.TrueClass, predictedClass, confidence, sample.Filename)

			if !isCorrectTop1 && len(predictions) > 1 {
				fmt.Printf("           Alternatives: ")
				for j := 1; j < len(predictions) && j < 3; j++ {
					fmt.Printf("%s (%.3f) ", predictions[j].Class, predictions[j].Confidence)
				}
				fmt.Println()
			}
		}

		// Progress indicator (every 50 samples)
		if !verbose && (i+1)%50 == 0 {
			fmt.Printf("  Processed %d/%d samples (%.1f%%)\n", i+1, len(testSamples),
				float64(i+1)/float64(len(testSamples))*100)
		}
	}

	// Use correctTop1 for topN=1, correctTop3 for topN=3
	if topN == 1 {
		result.CorrectPredictions = correctTop1
	} else {
		result.CorrectPredictions = correctTop3
	}

	result.Accuracy = float64(result.CorrectPredictions) / float64(result.TotalSamples)
	result.Top3Accuracy = float64(correctTop3) / float64(result.TotalSamples)
	result.AvgConfidence = totalConfidence / float64(result.TotalSamples)
	result.AvgPredictionTime = totalPredictionTime / time.Duration(result.TotalSamples)

	// Calculate per-class metrics
	result.PerClassMetrics = calculatePerClassMetrics(result.ConfusionMatrix, model.GetClasses())

	return result
}

// calculatePerClassMetrics computes precision, recall, F1 for each class
func calculatePerClassMetrics(confusionMatrix map[string]map[string]int, classes []string) map[string]ClassMetrics {
	metrics := make(map[string]ClassMetrics)

	for _, trueClass := range classes {
		metric := ClassMetrics{}

		// True Positives: correctly predicted as this class
		metric.TruePositives = confusionMatrix[trueClass][trueClass]

		// False Negatives: this class predicted as something else
		for _, predictedClass := range classes {
			if predictedClass != trueClass {
				metric.FalseNegatives += confusionMatrix[trueClass][predictedClass]
			}
		}

		// False Positives: other classes predicted as this class
		for _, otherTrueClass := range classes {
			if otherTrueClass != trueClass {
				metric.FalsePositives += confusionMatrix[otherTrueClass][trueClass]
			}
		}

		// Calculate precision, recall, F1
		if metric.TruePositives+metric.FalsePositives > 0 {
			metric.Precision = float64(metric.TruePositives) / float64(metric.TruePositives+metric.FalsePositives)
		}

		if metric.TruePositives+metric.FalseNegatives > 0 {
			metric.Recall = float64(metric.TruePositives) / float64(metric.TruePositives+metric.FalseNegatives)
		}

		if metric.Precision+metric.Recall > 0 {
			metric.F1Score = 2 * metric.Precision * metric.Recall / (metric.Precision + metric.Recall)
		}

		metrics[trueClass] = metric
	}

	return metrics
}

// printEvaluationResults prints formatted evaluation results
func printEvaluationResults(result EvaluationResult) {
	fmt.Println()
	fmt.Println("========================================")
	fmt.Println("EVALUATION RESULTS")
	fmt.Println("========================================")
	fmt.Println()

	fmt.Printf("Total test samples: %d\n", result.TotalSamples)
	fmt.Printf("Correct predictions: %d\n", result.CorrectPredictions)
	fmt.Printf("Accuracy: %.2f%%\n", result.Accuracy*100)
	fmt.Printf("Top-3 Accuracy: %.2f%%\n", result.Top3Accuracy*100)
	fmt.Printf("Average confidence: %.4f\n", result.AvgConfidence)
	fmt.Printf("Average prediction time: %v\n", result.AvgPredictionTime)
	fmt.Println()

	// Performance thresholds
	fmt.Println("Performance Thresholds:")
	accuracyStatus := "❌"
	if result.Accuracy >= 0.90 {
		accuracyStatus = "✅"
	}
	fmt.Printf("  %s Accuracy ≥ 90%%: %.2f%%\n", accuracyStatus, result.Accuracy*100)

	latencyStatus := "❌"
	if result.AvgPredictionTime < 10*time.Millisecond {
		latencyStatus = "✅"
	}
	fmt.Printf("  %s Latency < 10ms: %v\n", latencyStatus, result.AvgPredictionTime)
	fmt.Println()

	// Per-class metrics (top 10 by F1 score)
	fmt.Println("Top 10 Classes by F1 Score:")
	type classF1 struct {
		Class   string
		Metrics ClassMetrics
	}

	classMetrics := make([]classF1, 0, len(result.PerClassMetrics))
	for class, metrics := range result.PerClassMetrics {
		classMetrics = append(classMetrics, classF1{Class: class, Metrics: metrics})
	}

	// Sort by F1 score descending
	sort.Slice(classMetrics, func(i, j int) bool {
		return classMetrics[i].Metrics.F1Score > classMetrics[j].Metrics.F1Score
	})

	fmt.Println()
	fmt.Printf("%-30s %10s %10s %10s\n", "Class", "Precision", "Recall", "F1 Score")
	fmt.Println("----------------------------------------------------------------------")

	for i := 0; i < 10 && i < len(classMetrics); i++ {
		cm := classMetrics[i]
		fmt.Printf("%-30s %9.2f%% %9.2f%% %9.2f%%\n",
			cm.Class,
			cm.Metrics.Precision*100,
			cm.Metrics.Recall*100,
			cm.Metrics.F1Score*100)
	}

	// Bottom 5 classes (lowest F1 scores)
	if len(classMetrics) > 10 {
		fmt.Println()
		fmt.Println("Bottom 5 Classes (Need Attention):")
		fmt.Printf("%-30s %10s %10s %10s\n", "Class", "Precision", "Recall", "F1 Score")
		fmt.Println("----------------------------------------------------------------------")

		start := len(classMetrics) - 5
		if start < 0 {
			start = 0
		}

		for i := len(classMetrics) - 1; i >= start; i-- {
			cm := classMetrics[i]
			fmt.Printf("%-30s %9.2f%% %9.2f%% %9.2f%%\n",
				cm.Class,
				cm.Metrics.Precision*100,
				cm.Metrics.Recall*100,
				cm.Metrics.F1Score*100)
		}
	}
}
