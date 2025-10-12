# Phase 3: FastText Integration - Implementation Plan

**Goal**: Replace mock ClassificationService with real FastText ML model for Italian TV series classification

**Target**: >85% accuracy, <50ms inference time, ARM32-optimized

---

## 📋 Current State Analysis

### Existing Infrastructure ✅

| Component | Status | Notes |
|-----------|--------|-------|
| **TokenizerService** | ✅ Complete | Italian-optimized, source-generated regexes |
| **FeatureEngineeringService** | ✅ Complete | Single-pass, span-based, ARM32-optimized |
| **PredictionService** | ✅ Complete | Circular buffer for metrics |
| **ClassificationService** | ⚠️ Mock | Returns "MOCK SERIES" for all inputs |
| **IClassificationService Interface** | ✅ Complete | Clear contract defined |

### Current ClassificationService Contract

```csharp
public interface IClassificationService
{
    Task<Result<ClassificationResult>> ClassifyFilenameAsync(string filename);
    Task<Result<IEnumerable<ClassificationResult>>> ClassifyBatchAsync(IEnumerable<string> filenames);
    Result<IEnumerable<string>> GetAvailableCategories();
    Result<ModelInfo> GetModelInfo();
    bool IsModelReady();
}
```

### ML Pipeline Flow

```
Filename
  → TokenizerService (extract tokens, episode info, quality, etc.)
  → FeatureEngineeringService (create feature vector)
  → ClassificationService (predict category) ← TO BE IMPLEMENTED
  → ClassificationResult (category + confidence + alternatives)
```

---

## 🎯 FastText Integration Options

### Option 1: Microsoft.ML with Text Classification

**Packages**:
- `Microsoft.ML` (already installed: v3.0.1)
- `Microsoft.ML.FastTree` (already installed: v3.0.1)

**Pros**:
- ✅ Already integrated in project
- ✅ .NET native, no native binaries
- ✅ Good ARM32 support
- ✅ Comprehensive ML.NET ecosystem

**Cons**:
- ❌ Not true FastText (different algorithm)
- ❌ Larger model size (~50-100MB vs FastText's ~20MB)
- ❌ Slower inference (80-100ms vs FastText's 20-30ms)

### Option 2: FastText.NetWrapper

**Packages**:
- `FastText.NetWrapper` (P/Invoke wrapper for native FastText)

**Pros**:
- ✅ True Facebook FastText implementation
- ✅ Small model size (~20MB)
- ✅ Fast inference (<30ms)
- ✅ Proven accuracy for text classification

**Cons**:
- ❌ Requires native binaries (libfasttext.so for Linux)
- ❌ ARM32 support requires cross-compilation
- ❌ More complex deployment

### Option 3: Custom FastText Implementation

**Packages**:
- None (pure C# implementation)

**Pros**:
- ✅ Full control over implementation
- ✅ No native dependencies
- ✅ Perfect ARM32 compatibility

**Cons**:
- ❌ Significant development time (2-3 weeks)
- ❌ Need to implement FastText algorithm from scratch
- ❌ Potential accuracy differences vs. reference implementation

### Option 4: Hybrid Approach (RECOMMENDED)

**Strategy**: Use Microsoft.ML for now, prepare for FastText.NetWrapper migration

**Phase 3A** (Immediate):
1. Implement with Microsoft.ML TextClassification
2. Train model on Italian TV series dataset
3. Achieve >80% accuracy baseline
4. Validate ARM32 performance

**Phase 3B** (Future):
1. Migrate to FastText.NetWrapper when ARM32 binaries available
2. A/B test accuracy differences
3. Optimize for <50ms inference

**Rationale**:
- Fastest path to production ML classification
- Proven ARM32 support
- Migration path to true FastText if needed

---

## 🏗️ Implementation Architecture

### 1. FastTextClassificationService

**Responsibilities**:
- Load trained ML.NET model from disk
- Orchestrate: Tokenize → Extract Features → Predict → Format Result
- Manage model lifecycle (load, unload, reload)
- Cache predictions for repeated filenames

**Key Methods**:
```csharp
public class FastTextClassificationService : IClassificationService
{
    private readonly ITokenizerService _tokenizer;
    private readonly IFeatureEngineeringService _featureService;
    private readonly IPredictionService _predictionService;
    private readonly MLConfiguration _config;
    private readonly ILogger<FastTextClassificationService> _logger;

    private PredictionEngine<FeatureVector, CategoryPrediction> _predictionEngine;
    private bool _modelLoaded;

    public async Task<Result<ClassificationResult>> ClassifyFilenameAsync(string filename)
    {
        // 1. Tokenize filename
        var tokenResult = _tokenizer.TokenizeFilename(filename);

        // 2. Extract features
        var featureResult = _featureService.ExtractFeatures(tokenResult.Value);

        // 3. Predict category
        var prediction = _predictionEngine.Predict(featureResult.Value);

        // 4. Format result
        return BuildClassificationResult(filename, prediction, featureResult.Value);
    }
}
```

### 2. Model Training Pipeline

**Component**: `ModelTrainingService`

**Responsibilities**:
- Load training data from CSV
- Prepare features for ML.NET
- Train text classification model
- Evaluate model performance
- Save trained model to disk

**Training Data Format**:
```csv
Filename;Category
Il.Trono.Di.Spade.8x01.ITA.1080p.mkv;GAME OF THRONES
One.Piece.1089.Sub.ITA.1080p.mkv;ONE PIECE
Breaking.Bad.S05E16.FINAL.1080p.mkv;BREAKING BAD
...
```

### 3. Model Architecture

**Algorithm**: Multi-class Text Classification (ML.NET)

**Pipeline**:
```csharp
var pipeline = mlContext.Transforms.Text
    .FeaturizeText("Features", "SeriesTokens")
    .Append(mlContext.Transforms.Conversion.MapValueToKey("Label", "Category"))
    .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
```

**Features Used**:
- Series tokens (from TokenizerService)
- N-grams (from FeatureEngineeringService)
- Quality indicators
- Language patterns
- Release group

**Target Performance**:
- Training time: <5 minutes (1000 samples)
- Model size: <50MB
- Inference time: <80ms (Phase 3A), <50ms (Phase 3B)
- Accuracy: >80% (Phase 3A), >85% (Phase 3B)

---

## 📊 Training Data Strategy

### Dataset Requirements

**Minimum**: 500 Italian TV series filenames with confirmed categories
**Target**: 1000+ samples for better accuracy
**Distribution**: Balanced across popular series

### Data Sources

1. **Existing TrackedFiles** (if available)
   - Query database for files with `Status = Moved`
   - Extract: `FileName` + `Category`
   - Validation: Manual review for accuracy

2. **Manual Curation**
   - Popular Italian TV series (100+ series)
   - 5-10 filename variations per series
   - Cover different seasons, qualities, release groups

3. **Synthetic Generation**
   - Template-based filename generation
   - Combine series names with quality/language patterns
   - Validate against tokenization rules

### Example Training Dataset

```csv
Filename;Category
Il.Trono.Di.Spade.8x01.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv;GAME OF THRONES
Il.Trono.Di.Spade.8x02.ITA.720p.WEB-DLMux-NovaRip.mkv;GAME OF THRONES
One.Piece.1089.Sub.ITA.1080p.WEB-DLMux.x264-UBi.mkv;ONE PIECE
One.Piece.1090.Sub.ITA.720p.WEB-DLMux-UBi.mkv;ONE PIECE
Breaking.Bad.S05E16.FINAL.ITA.1080p.BluRay.x264-KILLERS.mkv;BREAKING BAD
My.Hero.Academia.6x25.ITA.1080p.WEB-DLMux-Pir8.mkv;MY HERO ACADEMIA
L.Attacco.Dei.Giganti.4x01.Sub.ITA.1080p.WEB-DLMux.x264-UBi.mkv;ATTACK ON TITAN
Stranger.Things.4x09.ITA.ENG.1080p.NF.WEB-DLMux-DarkSideMux.mkv;STRANGER THINGS
The.Walking.Dead.11x24.FINAL.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv;THE WALKING DEAD
...
```

**Target Distribution**:
- Top 20 popular series: 50 samples each (1000 total)
- Medium popularity: 30 series × 10 samples (300 total)
- Long tail: 100 series × 3 samples (300 total)
- **Total**: ~1600 training samples

---

## 🔧 Implementation Tasks

### Phase 3A: Microsoft.ML Implementation (Week 1-2)

#### Task 1: Training Data Preparation
- [ ] Create `data/training/tv-series-training-data.csv`
- [ ] Curate 1000+ Italian TV series filenames with categories
- [ ] Validate data quality (no duplicates, balanced distribution)
- [ ] Split: 70% training, 15% validation, 15% test

#### Task 2: Model Training Service
- [ ] Implement `IModelTrainingService` interface
- [ ] Create `MLNetModelTrainingService` class
- [ ] Implement training pipeline (text featurization + SDCA)
- [ ] Add model evaluation metrics (accuracy, precision, recall, F1)
- [ ] Save trained model to `models/classification-model.zip`

#### Task 3: FastText Classification Service
- [ ] Implement `FastTextClassificationService` class
- [ ] Model loading on startup (lazy or eager)
- [ ] Integrate with existing TokenizerService + FeatureEngineeringService
- [ ] Map ML.NET predictions to ClassificationResult format
- [ ] Handle edge cases (unknown series, low confidence)

#### Task 4: Prediction Caching
- [ ] Implement prediction cache (LRU, 1000 items max)
- [ ] Cache key: SHA256 hash of filename
- [ ] TTL: 1 hour (configurable)
- [ ] ARM32 memory optimization: <5MB cache size

#### Task 5: Testing
- [ ] Unit tests for ModelTrainingService (10+ tests)
- [ ] Unit tests for FastTextClassificationService (15+ tests)
- [ ] Integration tests with real model (5+ tests)
- [ ] Performance benchmarks (inference time, memory usage)

#### Task 6: Configuration
- [ ] Add `MLConfiguration.ModelPath` (default: "models")
- [ ] Add `MLConfiguration.TrainingDataPath` (default: "data/training")
- [ ] Add `MLConfiguration.PredictionCacheTTL` (default: 3600 seconds)
- [ ] Add `MLConfiguration.MinConfidenceThreshold` (default: 0.5)

### Phase 3B: Optimization & FastText Migration (Week 3-4)

#### Task 7: ARM32 Performance Optimization
- [ ] Profile model loading time (<5s target)
- [ ] Profile inference latency (<80ms target for Phase 3A)
- [ ] Optimize feature vectorization
- [ ] Memory-map model file for faster loading

#### Task 8: Alternative Predictions
- [ ] Implement top-K prediction (K=3)
- [ ] Calculate confidence scores for alternatives
- [ ] Threshold-based decision logic (auto vs. suggest vs. unknown)

#### Task 9: Model Versioning
- [ ] Implement model version tracking
- [ ] Support model reloading without restart
- [ ] A/B testing framework for model comparison

#### Task 10: FastText.NetWrapper Migration (Optional)
- [ ] Research ARM32 native binary availability
- [ ] Create FastText model training script (Python)
- [ ] Implement FastText.NetWrapper integration
- [ ] A/B test accuracy vs. ML.NET
- [ ] Migrate if performance gains justify complexity

---

## 📈 Success Criteria

### Phase 3A Completion (ML.NET)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Model Accuracy** | >80% | Test set evaluation |
| **Inference Time** | <80ms | Average over 100 predictions |
| **Model Size** | <50MB | File size on disk |
| **Training Time** | <5 min | 1000 samples on ARM32 |
| **Model Loading** | <5s | Cold start on ARM32 |
| **Memory Overhead** | <30MB | Additional heap usage |
| **Cache Hit Rate** | >50% | After 1 hour of operation |

### Phase 3B Completion (Optimized)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Model Accuracy** | >85% | Test set evaluation |
| **Inference Time** | <50ms | Average over 100 predictions |
| **Model Size** | <30MB | File size on disk |
| **Top-3 Accuracy** | >95% | Correct category in top 3 |
| **F1 Score** | >0.85 | Weighted average across categories |

---

## 🔬 Evaluation Methodology

### Metrics

1. **Accuracy**: Correct predictions / Total predictions
2. **Precision**: True Positives / (True Positives + False Positives)
3. **Recall**: True Positives / (True Positives + False Negatives)
4. **F1 Score**: Harmonic mean of Precision and Recall
5. **Top-K Accuracy**: Correct category in top K predictions

### Test Data

**Holdout Set**: 15% of total data (never used in training)

**Categories**:
- Well-represented series (>20 samples): 10 series
- Medium series (10-20 samples): 15 series
- Rare series (3-10 samples): 25 series
- Unknown series (0 samples): 10 series (for generalization test)

### Confusion Matrix Analysis

Identify common misclassifications:
- Similar series names (e.g., "Breaking Bad" vs "Better Call Saul")
- Ambiguous abbreviations (e.g., "TWD" could be multiple series)
- Different language variants (e.g., Italian name vs English name)

---

## 🚀 Deployment Strategy

### Model Lifecycle

1. **Training** (periodic, e.g., weekly)
   - Collect new confirmed classifications
   - Retrain model with updated dataset
   - Evaluate against test set
   - Deploy if accuracy improves

2. **Loading** (on application startup)
   - Load model from disk (models/classification-model.zip)
   - Validate model integrity
   - Initialize PredictionEngine
   - Mark service as ready

3. **Inference** (per classification request)
   - Check cache first
   - Tokenize → Extract Features → Predict
   - Cache result
   - Return ClassificationResult

4. **Monitoring** (continuous)
   - Track prediction latency (P50, P95, P99)
   - Monitor confidence distribution
   - Alert on low confidence spike
   - Log misclassifications for retraining

### ARM32 Deployment Considerations

**Docker Image**:
- Include pre-trained model in image
- Or download from external storage on startup
- Model size impacts image size (~50MB additional)

**Resource Limits**:
- Memory: 300MB total (ML overhead: <30MB)
- CPU: Inference should use <20% CPU
- Disk: Model storage <50MB

---

## 📝 Code Structure

### New Files

```
src/MediaButler.ML/
├── Services/
│   ├── FastTextClassificationService.cs (new - main implementation)
│   ├── MLNetModelTrainingService.cs (new - training pipeline)
│   └── PredictionCacheService.cs (new - caching layer)
├── Models/
│   ├── TrainingData.cs (new - CSV data model)
│   └── PredictionCacheEntry.cs (new - cache entry model)
└── Configuration/
    └── MLTrainingConfiguration.cs (new - training config)

data/
└── training/
    ├── tv-series-training-data.csv (new - 1000+ samples)
    └── test-data.csv (new - holdout test set)

models/
└── classification-model.zip (generated - trained model)

tests/
└── MediaButler.Tests.Unit/ML/
    ├── FastTextClassificationServiceTests.cs (new)
    ├── MLNetModelTrainingServiceTests.cs (new)
    └── PredictionCacheServiceTests.cs (new)
```

---

## ⏱️ Timeline Estimate

### Week 1: Foundation (8-12 hours)
- Training data curation: 4 hours
- ModelTrainingService implementation: 4 hours
- Initial model training and evaluation: 4 hours

### Week 2: Integration (8-12 hours)
- FastTextClassificationService implementation: 6 hours
- Prediction caching: 2 hours
- Testing and validation: 4 hours

### Week 3: Optimization (6-8 hours)
- ARM32 performance profiling: 3 hours
- Inference optimization: 2 hours
- Alternative predictions and thresholding: 3 hours

### Week 4: Polish & Documentation (4-6 hours)
- Model versioning: 2 hours
- Comprehensive testing: 2 hours
- Documentation updates: 2 hours

**Total Estimate**: 26-38 hours over 4 weeks

---

## 🎯 Next Immediate Steps

1. **Create training data CSV** with 100 sample filenames (starter set)
2. **Implement ModelTrainingService** with basic ML.NET pipeline
3. **Train initial model** to validate approach
4. **Implement FastTextClassificationService** replacing mock
5. **Run integration tests** to verify end-to-end flow

---

**Document Version**: 1.0
**Created**: 2025-01-10
**Author**: Claude Code 🤖
**Status**: 🚧 Planning Complete, Ready for Implementation
