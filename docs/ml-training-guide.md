# MediaButler ML Training Guide

## Overview

MediaButler uses a FastText-based machine learning model to automatically classify TV series files based on their filenames. This guide covers model training, evaluation, and optimization for Italian content classification.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Model Training Process](#model-training-process)
- [Training Data Management](#training-data-management)
- [Model Evaluation and Validation](#model-evaluation-and-validation)
- [Performance Optimization](#performance-optimization)
- [ARM32 Deployment Considerations](#arm32-deployment-considerations)
- [Troubleshooting](#troubleshooting)

## Architecture Overview

### Core Components

MediaButler's ML system follows "Simple Made Easy" principles with clear separation of concerns:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ ITokenizerService │    │IFeatureEngineering│    │IPredictionService│
│                 │    │    Service      │    │                 │
│ • Text cleanup  │    │ • Extract features│    │ • Single predict│
│ • Tokenization  │    │ • Vector creation │    │ • Batch predict │
│ • Series extraction│    │ • Italian optimization│  │ • Performance stats│
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                │
                ┌─────────────────▼─────────────────┐
                │      IModelTrainingService        │
                │                                   │
                │ • Training data preparation       │
                │ • Model training orchestration    │
                │ • Hyperparameter optimization     │
                │ • Training progress monitoring    │
                └─────────────────┬─────────────────┘
                                │
                ┌─────────────────▼─────────────────┐
                │    IModelEvaluationService        │
                │                                   │
                │ • Accuracy evaluation             │
                │ • Confusion matrix analysis       │
                │ • Performance benchmarking        │
                │ • Cross-validation testing        │
                └───────────────────────────────────┘
```

### Key Interfaces

- **ITokenizerService**: Handles filename tokenization and series name extraction
- **IFeatureEngineeringService**: Creates feature vectors optimized for Italian content
- **IModelTrainingService**: Orchestrates the complete training process
- **IPredictionService**: Provides thread-safe prediction capabilities
- **IModelEvaluationService**: Validates model performance and accuracy

## Model Training Process

### 1. Training Data Collection

MediaButler collects training data from multiple sources:

#### User Feedback (Highest Quality)
```csharp
// Automatic collection from user confirmations
var userSample = TrainingSample.FromUserFeedback(
    filename: "Breaking.Bad.S05E16.FINAL.1080p.ITA.ENG.mkv",
    category: "BREAKING BAD",
    confidence: 1.0
);
```

#### Automated Analysis (Medium Quality)
```csharp
// Collection from high-confidence predictions
var autoSample = TrainingSample.FromAutomatedAnalysis(
    filename: "The.Office.S02E01.720p.Sub.ITA.mkv", 
    category: "THE OFFICE",
    confidence: 0.92
);
```

#### CSV Import (Variable Quality)
```csharp
// Batch import from external sources
var importService = serviceProvider.GetRequiredService<ICsvImportService>();
var result = await importService.ImportTrainingDataAsync(csvFilePath);
```

### 2. Training Data Validation

Before training, data undergoes comprehensive validation:

```csharp
var validationService = serviceProvider.GetRequiredService<ITrainingDataService>();
var validation = await validationService.ValidateDatasetAsync(trainingData);

if (!validation.IsValid)
{
    foreach (var issue in validation.Issues)
    {
        logger.LogWarning("Training data issue: {Issue}", issue.Description);
    }
}
```

#### Validation Criteria
- **Minimum samples per category**: 10 samples
- **Category name consistency**: UPPERCASE, no special characters
- **Filename validity**: Must contain episode/season markers
- **Balance checking**: No category should have <10% of average samples

### 3. Feature Engineering

MediaButler uses specialized feature engineering for Italian TV content:

```csharp
public class FeatureEngineeringService : IFeatureEngineeringService
{
    // Italian-specific optimizations
    private readonly string[] ItalianLanguageTags = { "ITA", "SUB.ITA", "DUB.ITA" };
    private readonly string[] QualityTags = { "720p", "1080p", "2160p", "HDTV", "BluRay" };
    
    public async Task<FeatureVector> ExtractFeaturesAsync(string filename)
    {
        var tokens = await _tokenizerService.TokenizeAsync(filename);
        
        return new FeatureVector
        {
            SeriesTokens = ExtractSeriesTokens(tokens),
            EpisodeMarkers = ExtractEpisodeInfo(tokens),
            QualityIndicators = ExtractQualityTags(tokens),
            LanguageMarkers = ExtractLanguageTags(tokens),
            ItalianContentScore = CalculateItalianScore(tokens)
        };
    }
}
```

### 4. Model Training Configuration

```csharp
var trainingConfig = new TrainingConfiguration
{
    Algorithm = TrainingAlgorithm.FastText,
    EmbeddingDimensions = 100,         // Optimized for ARM32 memory
    LearningRate = 0.1,
    EpochCount = 50,
    MinWordCount = 3,
    WindowSize = 5,
    
    // ARM32 optimizations
    ThreadCount = 4,                   // Match ARM32 cores
    MaxMemoryMB = 200,                // Leave room for system
    EnableVectorQuantization = true,   // Reduce model size
    
    // Italian content optimizations
    TokenizationLanguage = "italian",
    PreprocessingRules = new[]
    {
        "normalize_italian_articles",
        "handle_compound_series_names",
        "extract_episode_numbers"
    }
};
```

### 5. Training Execution

```csharp
var trainingService = serviceProvider.GetRequiredService<IModelTrainingService>();

// Start training with progress monitoring
var trainingResult = await trainingService.TrainModelAsync(trainingConfig);

if (trainingResult.IsSuccess)
{
    logger.LogInformation("Training completed successfully");
    logger.LogInformation("Final accuracy: {Accuracy:P2}", 
        trainingResult.Value.FinalAccuracy);
    logger.LogInformation("Training time: {Duration}", 
        trainingResult.Value.TrainingDuration);
}
```

## Training Data Management

### Data Storage Structure

```sql
-- Training samples with BaseEntity audit trail
CREATE TABLE TrainingSamples (
    Id INTEGER PRIMARY KEY,
    Filename TEXT NOT NULL,
    Category TEXT NOT NULL,
    Confidence REAL NOT NULL,
    Source INTEGER NOT NULL,  -- TrainingSampleSource enum
    IsManuallyVerified BOOLEAN NOT NULL DEFAULT 0,
    
    -- BaseEntity properties
    CreatedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastUpdateDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    Note TEXT NULL
);

-- Series patterns for similarity matching
CREATE TABLE SeriesPatterns (
    Id INTEGER PRIMARY KEY,
    SeriesName TEXT NOT NULL,
    EmbeddingVector BLOB NOT NULL,     -- Serialized feature vector
    ModelVersion TEXT NOT NULL,
    SampleCount INTEGER NOT NULL,
    
    -- BaseEntity properties
    CreatedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastUpdateDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    Note TEXT NULL
);
```

### Data Quality Metrics

Monitor training data quality using built-in statistics:

```csharp
var statistics = await trainingDataService.GetDatasetStatisticsAsync();

logger.LogInformation("Dataset Statistics:");
logger.LogInformation("- Total samples: {Total}", statistics.TotalSamples);
logger.LogInformation("- Unique categories: {Categories}", statistics.UniqueCategories);
logger.LogInformation("- Average samples per category: {Average:F1}", 
    statistics.AverageSamplesPerCategory);
logger.LogInformation("- Dataset balanced: {IsBalanced}", statistics.IsBalanced);

// Category distribution
foreach (var (category, count) in statistics.SamplesPerCategory)
{
    logger.LogInformation("- {Category}: {Count} samples", category, count);
}
```

## Model Evaluation and Validation

### Comprehensive Evaluation Pipeline

```csharp
var evaluationService = serviceProvider.GetRequiredService<IModelEvaluationService>();

// 1. Accuracy evaluation
var accuracyResult = await evaluationService.EvaluateAccuracyAsync(testData);
if (accuracyResult.IsSuccess)
{
    var metrics = accuracyResult.Value;
    logger.LogInformation("Overall Accuracy: {Accuracy:P2}", metrics.OverallAccuracy);
    logger.LogInformation("Macro Precision: {Precision:P2}", metrics.MacroPrecision);
    logger.LogInformation("Macro Recall: {Recall:P2}", metrics.MacroRecall);
    logger.LogInformation("Macro F1: {F1:P2}", metrics.MacroF1Score);
}

// 2. Confusion matrix analysis
var confusionResult = await evaluationService.GenerateConfusionMatrixAsync(testData);
if (confusionResult.IsSuccess)
{
    var matrix = confusionResult.Value;
    logger.LogInformation("Confusion Matrix generated with {Categories} categories", 
        matrix.Categories.Count);
    
    // Identify problematic misclassifications
    foreach (var misclassification in matrix.MostCommonMisclassifications.Take(5))
    {
        logger.LogWarning("Common misclassification: {Actual} → {Predicted} ({Count} times)",
            misclassification.ActualCategory, 
            misclassification.PredictedCategory,
            misclassification.Count);
    }
}

// 3. Performance benchmarking
var benchmarkConfig = new BenchmarkConfiguration
{
    PredictionCount = 1000,
    WarmupCount = 100,
    MonitorMemoryUsage = true,
    TimeoutMs = 30000
};

var benchmarkResult = await evaluationService.BenchmarkPerformanceAsync(benchmarkConfig);
if (benchmarkResult.IsSuccess)
{
    var benchmark = benchmarkResult.Value;
    logger.LogInformation("Performance Benchmark Results:");
    logger.LogInformation("- Average prediction time: {Time:F1}ms", 
        benchmark.AveragePredictionTimeMs);
    logger.LogInformation("- Throughput: {Throughput:F1} predictions/sec", 
        benchmark.ThroughputPredictionsPerSecond);
    logger.LogInformation("- Peak memory usage: {Memory:F1}MB", 
        benchmark.PeakMemoryUsageMB);
}
```

### Cross-Validation for Model Stability

```csharp
// 5-fold cross-validation for robust evaluation
var crossValidationResult = await evaluationService.PerformCrossValidationAsync(
    dataset: allTrainingData, 
    folds: 5
);

if (crossValidationResult.IsSuccess)
{
    var cvResults = crossValidationResult.Value;
    
    logger.LogInformation("Cross-Validation Results:");
    logger.LogInformation("- Mean accuracy: {Accuracy:P2} (±{StdDev:P3})", 
        cvResults.MeanAccuracy, cvResults.AccuracyStandardDeviation);
    logger.LogInformation("- Quality assessment: {Quality}", cvResults.Quality);
    
    foreach (var fold in cvResults.FoldResults)
    {
        logger.LogInformation("- Fold {Fold}: Accuracy {Accuracy:P2}, F1 {F1:P2}",
            fold.FoldNumber, fold.Accuracy, fold.F1Score);
    }
}
```

### Model Quality Thresholds

Configure quality thresholds for production deployment:

```csharp
var qualityThresholds = new QualityThresholds
{
    MinimumOverallAccuracy = 0.82,        // 82% minimum accuracy
    MinimumCategoryAccuracy = 0.70,       // 70% per-category minimum
    MaximumPredictionTimeMs = 100,        // ARM32 constraint
    MaximumMemoryUsageMB = 280,           // ARM32 constraint
    MinimumConfidenceDistribution = 0.60  // 60% high-confidence predictions
};

var validationResult = await evaluationService.ValidateModelQualityAsync(qualityThresholds);
if (validationResult.IsSuccess)
{
    var validation = validationResult.Value;
    
    if (validation.PassedValidation)
    {
        logger.LogInformation("Model passed all quality checks - ready for production");
    }
    else
    {
        foreach (var failure in validation.FailedChecks)
        {
            logger.LogError("Quality check failed: {Check} - {Reason}",
                failure.CheckName, failure.FailureReason);
        }
    }
}
```

## Performance Optimization

### ARM32 Memory Optimization

```csharp
// Model size optimization for ARM32 deployment
var optimizationConfig = new ModelOptimizationConfiguration
{
    // Reduce embedding dimensions for smaller model size
    EmbeddingDimensions = 50,              // vs. 100 for full model
    
    // Enable quantization to reduce memory footprint
    UseQuantization = true,
    QuantizationBits = 8,                  // 8-bit quantization
    
    // Limit vocabulary size
    MaxVocabularySize = 10000,             // Italian TV series vocabulary
    MinWordFrequency = 3,                  // Remove rare words
    
    // Optimize for inference speed
    OptimizeForInference = true,
    EnableFastApproximation = true         // Slight accuracy trade-off for speed
};
```

### Batch Processing Optimization

```csharp
// Efficient batch processing for ARM32 constraints
var batchConfig = new BatchProcessingConfiguration
{
    MaxBatchSize = 50,                     // Conservative for 1GB RAM
    ParallelismLevel = 4,                  // Match ARM32 cores
    UseMemoryPooling = true,               // Reduce GC pressure
    EnableProgressiveLoading = true        // Load features on-demand
};

var batchResult = await predictionService.PredictBatchAsync(
    filenames: largeBatch,
    configuration: batchConfig
);
```

### Caching Strategy

```csharp
// Implement intelligent caching for frequently accessed patterns
var cacheConfig = new CacheConfiguration
{
    MaxCacheSize = 1000,                   // Recently classified series
    CacheExpiryMinutes = 60,               // Refresh hourly
    UseSeriesPatternCache = true,          // Cache embedding vectors
    EnableStatisticalCaching = true        // Cache common n-grams
};
```

## ARM32 Deployment Considerations

### Memory Constraints

MediaButler is designed for ARM32 devices (Raspberry Pi) with limited memory:

- **Total system memory target**: <300MB
- **ML model size**: ~20MB FastText model
- **Runtime memory**: <100MB during inference
- **Batch processing**: Chunked to fit memory limits

### Performance Targets

- **Single prediction**: <100ms latency
- **Batch throughput**: >10 predictions/second
- **Concurrent predictions**: Support 2-4 simultaneous
- **Memory recovery**: >85% cleanup after large batches

### Model Deployment

```bash
# Model file structure for ARM32
models/
├── fasttext_v1.0.0.bin          # Main FastText model (~20MB)
├── vocabulary_italian.json      # Italian-specific vocabulary
├── series_embeddings.db         # Cached series pattern embeddings
└── model_metadata.json          # Version and configuration info
```

### Configuration for ARM32

```json
{
  "MediaButler": {
    "ML": {
      "ModelPath": "models/fasttext_v1.0.0.bin",
      "MaxMemoryMB": 200,
      "MaxConcurrentPredictions": 4,
      "BatchSizeLimit": 50,
      "EnableQuantization": true,
      "CacheSize": 1000,
      "PredictionTimeoutMs": 5000,
      "ARM32Optimizations": {
        "EnableMemoryPooling": true,
        "UseProgressiveLoading": true,
        "OptimizeForLatency": true
      }
    }
  }
}
```

## Monitoring and Maintenance

### Training Progress Monitoring

```csharp
// Monitor training progress in real-time
trainingService.TrainingProgress += (sender, args) =>
{
    logger.LogInformation("Training progress: Epoch {Epoch}/{Total} - Loss: {Loss:F4}",
        args.CurrentEpoch, args.TotalEpochs, args.CurrentLoss);
    
    // Early stopping for ARM32 resource management
    if (args.MemoryUsageMB > 250)
    {
        logger.LogWarning("Memory usage high: {Memory}MB - consider reducing batch size",
            args.MemoryUsageMB);
    }
};
```

### Model Performance Tracking

```csharp
// Continuous performance monitoring
var performanceStats = await predictionService.GetPerformanceStatsAsync();
if (performanceStats.IsSuccess)
{
    var stats = performanceStats.Value;
    
    // Log key metrics for monitoring
    logger.LogInformation("Performance Statistics:");
    logger.LogInformation("- Total predictions: {Total}", stats.TotalPredictions);
    logger.LogInformation("- Success rate: {Rate:P2}", stats.SuccessRate);
    logger.LogInformation("- Average latency: {Latency}ms", 
        stats.AveragePredictionTime.TotalMilliseconds);
    logger.LogInformation("- High confidence rate: {Rate:P2}",
        stats.ConfidenceBreakdown.HighConfidencePercentage / 100.0);
}
```

### Automated Retraining

```csharp
// Configure automatic retraining based on data accumulation
var retrainingConfig = new AutoRetrainingConfiguration
{
    MinNewSamplesForRetrain = 100,         // Retrain after 100 new samples
    MaxDaysSinceLastTraining = 30,         // Monthly retraining
    AccuracyThresholdForRetrain = 0.80,    // Retrain if accuracy drops below 80%
    EnableScheduledRetraining = true,      // Weekly scheduled check
    RetrainingTimeWindow = TimeSpan.FromHours(2)  // Off-peak hours
};
```

## Best Practices

### 1. Data Quality Management
- Validate all training data before use
- Maintain balanced category distribution
- Prioritize user feedback over automated samples
- Regular data quality audits

### 2. Model Training
- Use cross-validation for reliable evaluation
- Monitor memory usage during training on ARM32
- Implement early stopping for resource management
- Keep detailed training logs

### 3. Performance Optimization
- Optimize for ARM32 constraints from the start
- Use quantization for model size reduction
- Implement intelligent caching strategies
- Monitor prediction latency continuously

### 4. Production Deployment
- Always validate model quality before deployment
- Implement gradual rollout for new models
- Monitor performance metrics in production
- Have rollback procedures ready

## Troubleshooting

See the [ML Troubleshooting Guide](ml-troubleshooting-guide.md) for common issues and solutions.