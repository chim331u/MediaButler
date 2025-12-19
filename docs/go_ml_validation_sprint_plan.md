# Go ML Validation Sprint Plan
## 3-Week Proof-of-Concept for Naive Bayes + SQLite Solution

**Objective**: Validate accuracy claims and technical feasibility of replacing ML.NET with custom Go Naive Bayes classifier

**Success Criteria**:
- ✅ Accuracy ≥ 90% on test set (vs ML.NET baseline ≥ 95%)
- ✅ Latency p95 < 10ms on ARM32
- ✅ Memory usage < 5MB
- ✅ Incremental training < 10ms per sample

**Decision Gate**: End of Week 3 → Go/No-Go for full implementation

---

## Week 1: Foundation & Baseline

### Day 1-2: Data Preparation & Baseline Metrics

#### **Task 1.1: Extract ML.NET Training Data**
```bash
# Export existing training samples from .NET database
sqlite3 temp/mediabutler.dev.db \
  "SELECT FileName, Category FROM TrackedFiles
   WHERE Category IS NOT NULL AND Status = 4" \
  > data/training_samples.csv
```

**Deliverables**:
- [ ] `data/training_samples.csv` (all confirmed samples)
- [ ] `data/train_split.csv` (80% split)
- [ ] `data/test_split.csv` (20% split)
- [ ] Sample count report (expected: 100-150 samples)

#### **Task 1.2: Establish ML.NET Baseline**
```bash
# Run ML.NET model against test set
dotnet run --project tools/MLNetEvaluator \
  --model models/classification-simplified-model.zip \
  --test-data data/test_split.csv \
  --output results/mlnet_baseline.json
```

**Deliverables**:
- [ ] `results/mlnet_baseline.json`:
  - Overall accuracy
  - Per-class precision/recall
  - Confusion matrix
  - Average confidence scores
  - Prediction latency (p50, p95, p99)

**Success Gate**: Baseline accuracy ≥ 95% (validate current system)

---

### Day 3-5: Go Tokenizer Implementation

#### **Task 1.3: Port Italian Regex Tokenizer to Go**
```go
// File: pkg/ml/tokenizer/italian_tokenizer.go
type ItalianTokenizer struct {
    episodePatterns  []*regexp.Regexp  // 6 patterns
    qualityPatterns  []*regexp.Regexp  // 12 patterns
    languagePatterns []*regexp.Regexp  // 5 patterns
    releasePatterns  []*regexp.Regexp  // 5 patterns
}

func (t *ItalianTokenizer) ExtractSeriesName(filename string) string
func (t *ItalianTokenizer) Tokenize(filename string) []string
```

**Implementation Steps**:
1. Copy 29 regex patterns from .NET `TokenizerService.cs`
2. Implement `ExtractSeriesName()` (episode marker detection)
3. Implement `Tokenize()` (quality/language removal, normalization)
4. Add lowercase normalization and separator handling

**Deliverables**:
- [ ] `pkg/ml/tokenizer/italian_tokenizer.go`
- [ ] `pkg/ml/tokenizer/italian_tokenizer_test.go`
- [ ] 100% parity tests against .NET TokenizerService

**Test Cases** (from .NET tests):
```go
TestExtractSeriesName_StandardFormat()        // "Breaking.Bad.S01E01.mkv" → "breaking bad"
TestExtractSeriesName_ItalianFormat()         // "The.Walking.Dead.8x04.ITA.mkv" → "the walking dead"
TestExtractSeriesName_DateBased()             // "Series.2023-01-15.mkv" → "series"
TestTokenize_RemovesQualityIndicators()       // removes "1080p", "BluRay", "x264"
TestTokenize_RemovesLanguageCodes()           // removes "ITA", "ENG", "SUB"
TestTokenize_RemovesReleaseGroups()           // removes "-UBi", "-NovaRip"
```

**Success Gate**: 100% test parity with .NET TokenizerService

---

## Week 2: ML Implementation & Training

### Day 6-7: TF-IDF Engine

#### **Task 2.1: Implement TF-IDF Vectorizer**
```go
// File: pkg/ml/tfidf/vectorizer.go
type TFIDFVectorizer struct {
    idf map[string]float64  // token → IDF score
}

// Compute term frequency for a document
func ComputeTF(tokens []string) map[string]float64

// Compute inverse document frequency from SQLite
func (v *TFIDFVectorizer) ComputeIDF(db *sql.DB) error

// Transform tokens to TF-IDF weighted vector
func (v *TFIDFVectorizer) Transform(tokens []string) map[string]float64
```

**Formulas** (sklearn-compatible):
```
TF(token) = count(token) / total_tokens
IDF(token) = log((1 + num_docs) / (1 + doc_freq(token))) + 1
TF-IDF(token) = TF(token) × IDF(token)
```

**Deliverables**:
- [ ] `pkg/ml/tfidf/vectorizer.go`
- [ ] `pkg/ml/tfidf/vectorizer_test.go`
- [ ] Unit tests with known TF-IDF values (validate math)

**Validation**:
```python
# Generate Python sklearn reference values
from sklearn.feature_extraction.text import TfidfVectorizer
vectorizer = TfidfVectorizer()
X = vectorizer.fit_transform(["breaking bad s01e01", "the office s02e03"])
# Compare Go output to sklearn output (tolerance: 1e-6)
```

---

### Day 8-9: Naive Bayes Classifier

#### **Task 2.2: Implement Multinomial Naive Bayes**
```go
// File: pkg/ml/classifier/naive_bayes.go
type NaiveBayesModel struct {
    Classes []string                     // ["BREAKING BAD", "THE OFFICE", ...]
    Priors  map[string]float64          // P(class)
    Weights map[string]map[string]float64  // P(token|class)
    IDF     map[string]float64          // token → IDF score
}

// Train model from SQLite statistics
func TrainFromSQLite(db *sql.DB) (*NaiveBayesModel, error)

// Predict with confidence scores
func (m *NaiveBayesModel) Predict(tfidf map[string]float64) (string, float64, error)

// Get top-N alternative predictions
func (m *NaiveBayesModel) PredictTopN(tfidf map[string]float64, n int) []Prediction
```

**Naive Bayes Formula**:
```
P(class|tokens) ∝ P(class) × ∏ P(token|class)^tfidf(token)

Where:
  P(class) = doc_count(class) / total_docs
  P(token|class) = (token_count(class) + α) / (total_tokens(class) + α × vocab_size)
  α = smoothing parameter (default: 1.0)
```

**Deliverables**:
- [ ] `pkg/ml/classifier/naive_bayes.go`
- [ ] `pkg/ml/classifier/naive_bayes_test.go`
- [ ] Unit tests with toy dataset (validate probabilities)

---

### Day 10: SQLite Schema & Training Pipeline

#### **Task 2.3: Implement SQLite Training Schema**
```sql
-- File: pkg/ml/db/migrations/001_ml_schema.sql

-- Training samples
CREATE TABLE ml_samples (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  filename TEXT NOT NULL,
  series_name TEXT NOT NULL,  -- Extracted via tokenizer
  class TEXT NOT NULL,         -- TV series category
  tokens TEXT NOT NULL,        -- JSON array of tokens
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Class statistics (for priors)
CREATE TABLE ml_class_stats (
  class TEXT PRIMARY KEY,
  doc_count INTEGER NOT NULL DEFAULT 0,
  total_tokens INTEGER NOT NULL DEFAULT 0
);

-- Token-class co-occurrence (for P(token|class))
CREATE TABLE ml_token_stats (
  token TEXT NOT NULL,
  class TEXT NOT NULL,
  count INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (token, class)
);

-- IDF statistics (document frequency)
CREATE TABLE ml_idf_stats (
  token TEXT PRIMARY KEY,
  doc_freq INTEGER NOT NULL DEFAULT 0  -- Number of docs containing token
);

-- Model snapshots (versioning)
CREATE TABLE ml_model_snapshots (
  version INTEGER PRIMARY KEY AUTOINCREMENT,
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
  accuracy REAL,
  num_samples INTEGER,
  num_classes INTEGER,
  serialized_model BLOB  -- JSON or Gob
);

-- Indexes for fast queries
CREATE INDEX idx_ml_samples_class ON ml_samples(class);
CREATE INDEX idx_ml_token_stats_class ON ml_token_stats(class);
CREATE INDEX idx_ml_token_stats_token ON ml_token_stats(token);
```

#### **Task 2.4: Implement Incremental Training**
```go
// File: pkg/ml/training/incremental_trainer.go
type IncrementalTrainer struct {
    db         *sql.DB
    tokenizer  *tokenizer.ItalianTokenizer
    vectorizer *tfidf.TFIDFVectorizer
}

// Add single training sample and update statistics
func (t *IncrementalTrainer) AddSample(filename, class string) error {
    // 1. Extract series name and tokenize
    // 2. Insert into ml_samples
    // 3. Update ml_class_stats (doc_count++)
    // 4. Update ml_token_stats (per-token counts)
    // 5. Update ml_idf_stats (document frequency)
    // All in transaction
}

// Rebuild model from current statistics
func (t *IncrementalTrainer) RebuildModel() (*classifier.NaiveBayesModel, error) {
    // Query statistics from SQLite
    // Construct model in-memory
}
```

**Deliverables**:
- [ ] `pkg/ml/db/migrations/001_ml_schema.sql`
- [ ] `pkg/ml/training/incremental_trainer.go`
- [ ] `pkg/ml/training/incremental_trainer_test.go`
- [ ] Integration test: Add 100 samples, verify statistics

---

## Week 3: Evaluation & Benchmarking

### Day 11-12: Accuracy Evaluation

#### **Task 3.1: Train Model on Training Set**
```bash
# Import training data into SQLite
go run cmd/ml-trainer/main.go \
  --import data/train_split.csv \
  --db temp/mediabutler.dev.db

# Verify sample count
sqlite3 temp/mediabutler.dev.db \
  "SELECT class, COUNT(*) FROM ml_samples GROUP BY class"
```

#### **Task 3.2: Run Evaluation on Test Set**
```go
// File: cmd/ml-evaluator/main.go
func main() {
    // 1. Load test set (data/test_split.csv)
    // 2. Load trained Go NB model from SQLite
    // 3. Load ML.NET baseline results
    // 4. For each test sample:
    //    - Predict with Go NB
    //    - Compare to ML.NET prediction
    //    - Record accuracy, precision, recall
    // 5. Generate confusion matrix
    // 6. Output results/go_nb_evaluation.json
}
```

**Deliverables**:
- [ ] `results/go_nb_evaluation.json`:
  - Overall accuracy
  - Per-class precision/recall/F1
  - Confusion matrix
  - Confidence score distribution
  - Comparison vs ML.NET baseline

**Success Criteria**:
```
Go NB Accuracy ≥ 90% (absolute minimum)
Go NB Accuracy ≥ 95% (ideal, matches ML.NET)

If < 90%: STOP, recommend ONNX export instead
If 90-94%: CAUTION, assess if acceptable
If ≥ 95%: PROCEED with full implementation
```

---

### Day 13: Performance Benchmarking

#### **Task 3.3: Latency Benchmark**
```go
// File: cmd/ml-benchmark/main.go
func BenchmarkPrediction(b *testing.B) {
    model := loadModel()
    filenames := loadTestSet()

    b.ResetTimer()
    for i := 0; i < b.N; i++ {
        filename := filenames[i % len(filenames)]
        _, _ = model.Predict(filename)
    }
}

func BenchmarkIncrementalUpdate(b *testing.B) {
    trainer := NewIncrementalTrainer(db)

    b.ResetTimer()
    for i := 0; i < b.N; i++ {
        _ = trainer.AddSample(
            fmt.Sprintf("test.file.%d.mkv", i),
            "TEST SERIES",
        )
    }
}
```

**Run on ARM32** (Raspberry Pi 3B+ or equivalent):
```bash
# Build for ARM32
GOOS=linux GOARCH=arm GOARM=7 go build -o ml-benchmark cmd/ml-benchmark

# Copy to ARM32 device and run
scp ml-benchmark pi@raspberrypi:/tmp/
ssh pi@raspberrypi "/tmp/ml-benchmark -test.bench=. -test.benchmem"
```

**Deliverables**:
- [ ] `results/benchmark_arm32.txt`:
  - Prediction latency (p50, p95, p99)
  - Incremental update time
  - Memory usage (RSS, heap)
  - Comparison vs ML.NET on same hardware

**Success Criteria**:
```
Prediction p95 < 10ms (claimed 1-5ms)
Incremental update < 10ms (claimed)
Memory RSS < 10MB (claimed < 5MB)
```

---

### Day 14: Memory Profiling

#### **Task 3.4: Memory & Resource Analysis**
```bash
# Profile memory usage
go test -memprofile=mem.prof -bench=BenchmarkPrediction
go tool pprof -http=:8080 mem.prof

# Profile CPU usage
go test -cpuprofile=cpu.prof -bench=BenchmarkPrediction
go tool pprof -http=:8080 cpu.prof
```

**Analysis**:
- [ ] Identify memory allocations
- [ ] Optimize hot paths
- [ ] Validate SQLite cache size
- [ ] Check for memory leaks (continuous operation test)

**Deliverables**:
- [ ] Memory profile reports
- [ ] CPU profile reports
- [ ] Optimization recommendations

---

### Day 15: Decision Gate & Report

#### **Task 3.5: Comprehensive Evaluation Report**

**Report Structure**:
```markdown
# Go ML Validation Results

## Executive Summary
- [ ] Go/No-Go recommendation
- [ ] Key metrics comparison
- [ ] Risk assessment

## Accuracy Analysis
- [ ] Overall accuracy: ___%
- [ ] Per-class performance
- [ ] Confusion matrix analysis
- [ ] Error case analysis

## Performance Analysis
- [ ] Latency benchmarks (ARM32)
- [ ] Memory usage (vs target)
- [ ] Incremental training speed

## Risk Assessment
- [ ] Technical risks
- [ ] Accuracy risks
- [ ] Implementation complexity

## Recommendation
IF accuracy ≥ 95%:
  ✅ PROCEED with full Go NB implementation (4-5 weeks)

ELSE IF accuracy 90-94%:
  ⚠️ CONDITIONAL GO (discuss trade-offs with stakeholders)

ELSE IF accuracy < 90%:
  ❌ NO-GO, recommend ONNX export instead (3-4 weeks)
```

**Deliverables**:
- [ ] `docs/go_ml_validation_results.md`
- [ ] Presentation deck for decision meeting
- [ ] Cost-benefit analysis (Go NB vs ONNX vs Status Quo)

---

## Success Criteria Summary

### Critical (Must Pass)
- ✅ **Accuracy ≥ 90%** on test set
- ✅ **Tokenizer 100% parity** with .NET
- ✅ **Latency p95 < 10ms** on ARM32
- ✅ **Memory < 10MB** RSS on ARM32

### Stretch Goals
- ✅ **Accuracy ≥ 95%** (match ML.NET)
- ✅ **Latency p95 < 5ms** (claimed)
- ✅ **Memory < 5MB** (claimed)
- ✅ **Incremental update < 5ms** (better than claimed)

---

## Risk Mitigation Plan

### Risk 1: Accuracy < 90%
**Mitigation**:
- Analyze confusion matrix to identify problematic classes
- Improve tokenization for edge cases
- Consider bigram features (token pairs)
- Fallback to ONNX export plan

### Risk 2: Performance < Target
**Mitigation**:
- Profile and optimize hot paths
- Cache tokenization results
- Pre-compile regexes
- SQLite query optimization

### Risk 3: Tokenizer Bugs
**Mitigation**:
- 100% test coverage requirement
- Cross-validation with .NET test suite
- Fuzzing with real filenames
- Manual QA on Italian content

---

## Deliverables Checklist

### Code Artifacts
- [ ] `pkg/ml/tokenizer/italian_tokenizer.go`
- [ ] `pkg/ml/tfidf/vectorizer.go`
- [ ] `pkg/ml/classifier/naive_bayes.go`
- [ ] `pkg/ml/training/incremental_trainer.go`
- [ ] `pkg/ml/db/migrations/001_ml_schema.sql`
- [ ] `cmd/ml-trainer/main.go` (import training data)
- [ ] `cmd/ml-evaluator/main.go` (accuracy evaluation)
- [ ] `cmd/ml-benchmark/main.go` (performance benchmarks)

### Test Suites
- [ ] Tokenizer unit tests (100% parity)
- [ ] TF-IDF unit tests (math validation)
- [ ] Naive Bayes unit tests (probability checks)
- [ ] Integration tests (end-to-end classification)
- [ ] Benchmark tests (latency, memory)

### Data & Results
- [ ] `data/training_samples.csv` (all samples)
- [ ] `data/train_split.csv` (80% split)
- [ ] `data/test_split.csv` (20% split)
- [ ] `results/mlnet_baseline.json` (ML.NET metrics)
- [ ] `results/go_nb_evaluation.json` (Go NB metrics)
- [ ] `results/benchmark_arm32.txt` (performance)

### Documentation
- [ ] `docs/go_ml_validation_results.md` (final report)
- [ ] API documentation (godoc)
- [ ] Architecture decision record (ADR)

---

## Timeline Summary

| Week | Focus | Key Deliverables | Decision Point |
|------|-------|------------------|----------------|
| **Week 1** | Foundation | Tokenizer, Baseline | Day 5: Tokenizer parity |
| **Week 2** | ML Implementation | TF-IDF, NB, Training | Day 10: Model trained |
| **Week 3** | Validation | Accuracy, Perf, Report | Day 15: Go/No-Go |

**Total Duration**: 15 working days (3 weeks)

**Team Size**: 1 developer (full-time)

**Dependencies**:
- Access to .NET ML.NET model and training data
- ARM32 device for benchmarking (Raspberry Pi 3B+)
- SQLite database with existing samples

---

## Next Steps After Sprint

### If GO (Accuracy ≥ 90%)
**Week 4-8**: Full production implementation
- API endpoint integration
- Model hot-reloading
- SSE event notifications
- Migration tooling from ML.NET
- A/B testing framework
- Production deployment

### If NO-GO (Accuracy < 90%)
**Week 4-7**: ONNX export fallback
- Export ML.NET model to ONNX
- Integrate onnxruntime_go
- Deploy with Go API
- Keep .NET for training only

---

## Contact & Questions

**Sprint Lead**: [Assign developer]
**Stakeholders**: [List stakeholders]
**Status Updates**: Daily standup + Friday demo
**Decision Meeting**: End of Day 15

**Questions or blockers?** → Escalate immediately, don't wait

---

## Appendix: Reference Materials

### ML.NET Source Code
- `src/MediaButler.ML/Services/TokenizerService.cs` (29 regexes)
- `src/MediaButler.ML/Services/FastTextClassificationService.cs` (classification)
- `tests/MediaButler.Tests.Unit/ML/TokenizerServiceTests.cs` (test cases)

### Existing Training Data
- Database: `temp/mediabutler.dev.db`
- Table: `TrackedFiles` (WHERE Category IS NOT NULL)
- Expected samples: 100-150

### Performance Baseline
- Current ML.NET: ~50ms p95 latency on ARM32
- Current ML.NET: ~50MB memory usage
- Target Go NB: <10ms p95, <5MB memory

---

**Document Version**: 1.0
**Created**: 2025-12-18
**Last Updated**: 2025-12-18
**Status**: DRAFT - Pending Approval
