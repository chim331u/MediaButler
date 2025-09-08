using System.Text.Json.Serialization;

namespace MediaButler.ML.Models;

/// <summary>
/// Complete ML model architecture specification for Italian TV series classification.
/// Defines the full pipeline from feature vectors to category predictions.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable architecture specification
/// - Single responsibility: Only holds model architecture definition
/// - Declarative: Clear model specification without training implementation
/// </remarks>
public sealed record MLModelArchitecture
{
    /// <summary>
    /// Model architecture unique identifier.
    /// </summary>
    public required string ArchitectureId { get; init; }
    
    /// <summary>
    /// Model version for tracking and compatibility.
    /// </summary>
    public required string Version { get; init; }
    
    /// <summary>
    /// Model type specification.
    /// </summary>
    public required ModelType ModelType { get; init; }
    
    /// <summary>
    /// ML algorithm configuration.
    /// </summary>
    public required AlgorithmConfiguration Algorithm { get; init; }
    
    /// <summary>
    /// Feature pipeline configuration.
    /// </summary>
    public required FeaturePipelineConfig FeaturePipeline { get; init; }
    
    /// <summary>
    /// Model evaluation configuration.
    /// </summary>
    public required ModelEvaluationMetrics EvaluationMetrics { get; init; }
    
    /// <summary>
    /// Cross-validation strategy.
    /// </summary>
    public required CrossValidationConfig CrossValidation { get; init; }
    
    /// <summary>
    /// Italian content optimization settings.
    /// </summary>
    public required ItalianContentConfig ItalianOptimization { get; init; }
    
    /// <summary>
    /// Performance requirements and constraints.
    /// </summary>
    public required PerformanceRequirements Performance { get; init; }
    
    /// <summary>
    /// When this architecture was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Optional metadata for architecture tracking.
    /// </summary>
    public string? Metadata { get; init; }
    
    /// <summary>
    /// Estimated resource requirements.
    /// </summary>
    public ModelResourceEstimate? ResourceEstimate { get; init; }
    
    /// <summary>
    /// Gets architecture summary for display.
    /// </summary>
    public string Summary => $"{ModelType} - {Algorithm.AlgorithmType} (v{Version})";
    
    /// <summary>
    /// Validates architecture configuration completeness.
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(ArchitectureId) && 
                          !string.IsNullOrEmpty(Version) &&
                          FeaturePipeline.IsValid &&
                          Algorithm.IsValid;
    
    /// <summary>
    /// Creates recommended architecture for Italian TV series classification.
    /// </summary>
    /// <returns>Pre-configured optimal architecture</returns>
    public static MLModelArchitecture CreateRecommendedArchitecture()
    {
        return new MLModelArchitecture
        {
            ArchitectureId = "italian-tv-classification-v1",
            Version = "1.0.0",
            ModelType = ModelType.MultiClassClassification,
            Algorithm = AlgorithmConfiguration.CreateOptimizedForItalianContent(),
            FeaturePipeline = FeaturePipelineConfig.CreateComprehensive(),
            EvaluationMetrics = ModelEvaluationMetrics.CreateForClassification(),
            CrossValidation = CrossValidationConfig.CreateStratified(folds: 5),
            ItalianOptimization = ItalianContentConfig.CreateDefault(),
            Performance = PerformanceRequirements.CreateForARM32(),
            CreatedAt = DateTime.UtcNow,
            Metadata = "Optimized for Italian TV series with 43+ categories, ARM32 deployment"
        };
    }
}

/// <summary>
/// ML model types supported by MediaButler.
/// </summary>
public enum ModelType
{
    /// <summary>
    /// Multi-class classification for TV series categorization.
    /// </summary>
    MultiClassClassification = 0,
    
    /// <summary>
    /// Binary classification for content filtering.
    /// </summary>
    BinaryClassification = 1,
    
    /// <summary>
    /// Regression for confidence scoring.
    /// </summary>
    Regression = 2
}

/// <summary>
/// ML algorithm configuration with hyperparameters.
/// </summary>
public sealed record AlgorithmConfiguration
{
    /// <summary>
    /// Algorithm type selection.
    /// </summary>
    public required AlgorithmType AlgorithmType { get; init; }
    
    /// <summary>
    /// Algorithm-specific hyperparameters.
    /// </summary>
    public required Dictionary<string, object> Hyperparameters { get; init; }
    
    /// <summary>
    /// Feature importance threshold for selection.
    /// </summary>
    public required double FeatureImportanceThreshold { get; init; }
    
    /// <summary>
    /// Maximum training iterations.
    /// </summary>
    public required int MaxIterations { get; init; }
    
    /// <summary>
    /// Learning rate for gradient-based algorithms.
    /// </summary>
    public required double LearningRate { get; init; }
    
    /// <summary>
    /// Regularization strength to prevent overfitting.
    /// </summary>
    public required double RegularizationStrength { get; init; }
    
    /// <summary>
    /// Validation criteria for configuration completeness.
    /// </summary>
    public bool IsValid => Hyperparameters.Any() && 
                          MaxIterations > 0 && 
                          LearningRate > 0 && 
                          FeatureImportanceThreshold >= 0;
    
    /// <summary>
    /// Creates optimized algorithm configuration for Italian content.
    /// </summary>
    public static AlgorithmConfiguration CreateOptimizedForItalianContent()
    {
        return new AlgorithmConfiguration
        {
            AlgorithmType = AlgorithmType.LightGBM,
            MaxIterations = 500,
            LearningRate = 0.1,
            RegularizationStrength = 0.01,
            FeatureImportanceThreshold = 0.001,
            Hyperparameters = new Dictionary<string, object>
            {
                ["num_leaves"] = 63,
                ["max_depth"] = 8,
                ["min_data_in_leaf"] = 5,
                ["bagging_fraction"] = 0.8,
                ["feature_fraction"] = 0.9,
                ["lambda_l1"] = 0.1,
                ["lambda_l2"] = 0.1,
                ["objective"] = "multiclass",
                ["metric"] = "multi_logloss",
                ["boosting_type"] = "gbdt"
            }
        };
    }
}

/// <summary>
/// ML algorithm types available for classification.
/// </summary>
public enum AlgorithmType
{
    /// <summary>
    /// Light Gradient Boosting Machine - optimal for Italian content.
    /// </summary>
    LightGBM = 0,
    
    /// <summary>
    /// FastTree - fast training with good accuracy.
    /// </summary>
    FastTree = 1,
    
    /// <summary>
    /// Logistic Regression - simple and interpretable.
    /// </summary>
    LogisticRegression = 2,
    
    /// <summary>
    /// Support Vector Machine - good for high-dimensional features.
    /// </summary>
    SVM = 3,
    
    /// <summary>
    /// Random Forest - ensemble method with feature importance.
    /// </summary>
    RandomForest = 4
}

/// <summary>
/// Feature pipeline configuration for data preprocessing.
/// </summary>
public sealed record FeaturePipelineConfig
{
    /// <summary>
    /// Feature normalization settings.
    /// </summary>
    public required NormalizationConfig Normalization { get; init; }
    
    /// <summary>
    /// Categorical feature encoding settings.
    /// </summary>
    public required CategoricalEncodingConfig CategoricalEncoding { get; init; }
    
    /// <summary>
    /// Feature selection configuration.
    /// </summary>
    public required FeatureSelectionConfig FeatureSelection { get; init; }
    
    /// <summary>
    /// Text feature processing settings.
    /// </summary>
    public required TextFeatureConfig TextFeatures { get; init; }
    
    /// <summary>
    /// Pipeline validation criteria.
    /// </summary>
    public bool IsValid => Normalization.IsValid && 
                          CategoricalEncoding.IsValid && 
                          FeatureSelection.IsValid;
    
    /// <summary>
    /// Creates comprehensive feature pipeline configuration.
    /// </summary>
    public static FeaturePipelineConfig CreateComprehensive()
    {
        return new FeaturePipelineConfig
        {
            Normalization = NormalizationConfig.CreateDefault(),
            CategoricalEncoding = CategoricalEncodingConfig.CreateOptimal(),
            FeatureSelection = FeatureSelectionConfig.CreateForItalianContent(),
            TextFeatures = TextFeatureConfig.CreateForFilenames()
        };
    }
}

/// <summary>
/// Cross-validation configuration for model evaluation.
/// </summary>
public sealed record CrossValidationConfig
{
    /// <summary>
    /// Number of folds for k-fold validation.
    /// </summary>
    public required int Folds { get; init; }
    
    /// <summary>
    /// Whether to use stratified sampling.
    /// </summary>
    public required bool UseStratification { get; init; }
    
    /// <summary>
    /// Random seed for reproducible splits.
    /// </summary>
    public required int RandomSeed { get; init; }
    
    /// <summary>
    /// Validation sampling strategy.
    /// </summary>
    public required ValidationSamplingStrategy SamplingStrategy { get; init; }
    
    /// <summary>
    /// Creates stratified cross-validation configuration.
    /// </summary>
    public static CrossValidationConfig CreateStratified(int folds = 5)
    {
        return new CrossValidationConfig
        {
            Folds = folds,
            UseStratification = true,
            RandomSeed = 42,
            SamplingStrategy = ValidationSamplingStrategy.Stratified
        };
    }
}

/// <summary>
/// Validation sampling strategies.
/// </summary>
public enum ValidationSamplingStrategy
{
    /// <summary>
    /// Random sampling across all categories.
    /// </summary>
    Random = 0,
    
    /// <summary>
    /// Stratified sampling maintaining category proportions.
    /// </summary>
    Stratified = 1,
    
    /// <summary>
    /// Time-based sampling for temporal data.
    /// </summary>
    Temporal = 2
}

/// <summary>
/// Model evaluation metrics configuration.
/// </summary>
public sealed record ModelEvaluationMetrics
{
    /// <summary>
    /// Primary metrics to calculate.
    /// </summary>
    public required IReadOnlyList<EvaluationMetricType> PrimaryMetrics { get; init; }
    
    /// <summary>
    /// Secondary metrics for detailed analysis.
    /// </summary>
    public required IReadOnlyList<EvaluationMetricType> SecondaryMetrics { get; init; }
    
    /// <summary>
    /// Target accuracy threshold for production deployment.
    /// </summary>
    public required double TargetAccuracy { get; init; }
    
    /// <summary>
    /// Minimum F1-score for category-specific performance.
    /// </summary>
    public required double MinimumF1Score { get; init; }
    
    /// <summary>
    /// Whether to generate confusion matrix analysis.
    /// </summary>
    public required bool GenerateConfusionMatrix { get; init; }
    
    /// <summary>
    /// Whether to calculate per-category metrics.
    /// </summary>
    public required bool CalculatePerCategoryMetrics { get; init; }
    
    /// <summary>
    /// Creates evaluation metrics for classification tasks.
    /// </summary>
    public static ModelEvaluationMetrics CreateForClassification()
    {
        return new ModelEvaluationMetrics
        {
            PrimaryMetrics = new[]
            {
                EvaluationMetricType.Accuracy,
                EvaluationMetricType.MacroF1Score,
                EvaluationMetricType.WeightedF1Score
            }.AsReadOnly(),
            SecondaryMetrics = new[]
            {
                EvaluationMetricType.Precision,
                EvaluationMetricType.Recall,
                EvaluationMetricType.LogLoss,
                EvaluationMetricType.AUC
            }.AsReadOnly(),
            TargetAccuracy = 0.85, // 85% accuracy target
            MinimumF1Score = 0.80, // 80% F1-score minimum
            GenerateConfusionMatrix = true,
            CalculatePerCategoryMetrics = true
        };
    }
}

/// <summary>
/// Evaluation metric types for model assessment.
/// </summary>
public enum EvaluationMetricType
{
    /// <summary>
    /// Overall classification accuracy.
    /// </summary>
    Accuracy = 0,
    
    /// <summary>
    /// Macro-averaged F1 score.
    /// </summary>
    MacroF1Score = 1,
    
    /// <summary>
    /// Weighted F1 score by class frequency.
    /// </summary>
    WeightedF1Score = 2,
    
    /// <summary>
    /// Precision (positive predictive value).
    /// </summary>
    Precision = 3,
    
    /// <summary>
    /// Recall (sensitivity).
    /// </summary>
    Recall = 4,
    
    /// <summary>
    /// Logarithmic loss for probability calibration.
    /// </summary>
    LogLoss = 5,
    
    /// <summary>
    /// Area under ROC curve.
    /// </summary>
    AUC = 6
}

/// <summary>
/// Italian content optimization configuration.
/// </summary>
public sealed record ItalianContentConfig
{
    /// <summary>
    /// Known Italian release group patterns.
    /// </summary>
    public required IReadOnlyList<string> ReleaseGroupPatterns { get; init; }
    
    /// <summary>
    /// Italian language indicators to prioritize.
    /// </summary>
    public required IReadOnlyList<string> LanguageIndicators { get; init; }
    
    /// <summary>
    /// Quality tier preferences for Italian content.
    /// </summary>
    public required Dictionary<string, double> QualityTierWeights { get; init; }
    
    /// <summary>
    /// Series name normalization rules for Italian content.
    /// </summary>
    public required Dictionary<string, string> SeriesNormalizationRules { get; init; }
    
    /// <summary>
    /// Creates default Italian content optimization.
    /// </summary>
    public static ItalianContentConfig CreateDefault()
    {
        return new ItalianContentConfig
        {
            ReleaseGroupPatterns = new[]
            {
                "NovaRip", "DarkSideMux", "Pir8", "iGM", "UBi", 
                "NTb", "MIXED", "IGM", "KILLERS", "SVA"
            }.AsReadOnly(),
            LanguageIndicators = new[]
            {
                "ITA", "iTALiAN", "SUB.ITA", "ITA.ENG", "MULTI"
            }.AsReadOnly(),
            QualityTierWeights = new Dictionary<string, double>
            {
                ["UltraHigh"] = 1.2,
                ["High"] = 1.0,
                ["Standard"] = 0.8,
                ["Low"] = 0.6
            },
            SeriesNormalizationRules = new Dictionary<string, string>
            {
                ["Il Trono di Spade"] = "GAME OF THRONES",
                ["Casa di Carta"] = "MONEY HEIST",
                ["Peaky Blinders"] = "PEAKY BLINDERS"
            }
        };
    }
}

/// <summary>
/// Performance requirements for ARM32 deployment.
/// </summary>
public sealed record PerformanceRequirements
{
    /// <summary>
    /// Maximum memory usage in MB.
    /// </summary>
    public required int MaxMemoryUsageMB { get; init; }
    
    /// <summary>
    /// Maximum prediction latency in milliseconds.
    /// </summary>
    public required int MaxPredictionLatencyMs { get; init; }
    
    /// <summary>
    /// Maximum model file size in MB.
    /// </summary>
    public required int MaxModelSizeMB { get; init; }
    
    /// <summary>
    /// Target throughput in predictions per second.
    /// </summary>
    public required int TargetThroughput { get; init; }
    
    /// <summary>
    /// Creates performance requirements for ARM32.
    /// </summary>
    public static PerformanceRequirements CreateForARM32()
    {
        return new PerformanceRequirements
        {
            MaxMemoryUsageMB = 200, // Conservative for 1GB RAM systems
            MaxPredictionLatencyMs = 500, // 0.5 second max
            MaxModelSizeMB = 50, // Reasonable model size
            TargetThroughput = 10 // 10 classifications per second
        };
    }
}

/// <summary>
/// Model resource usage estimates.
/// </summary>
public sealed record ModelResourceEstimate
{
    /// <summary>
    /// Estimated memory usage in MB.
    /// </summary>
    public required double EstimatedMemoryMB { get; init; }
    
    /// <summary>
    /// Estimated prediction latency in milliseconds.
    /// </summary>
    public required double EstimatedLatencyMs { get; init; }
    
    /// <summary>
    /// Estimated model file size in MB.
    /// </summary>
    public required double EstimatedModelSizeMB { get; init; }
    
    /// <summary>
    /// Estimated training time in minutes.
    /// </summary>
    public required double EstimatedTrainingTimeMinutes { get; init; }
    
    /// <summary>
    /// Whether estimates meet performance requirements.
    /// </summary>
    public bool MeetsRequirements(PerformanceRequirements requirements)
    {
        return EstimatedMemoryMB <= requirements.MaxMemoryUsageMB &&
               EstimatedLatencyMs <= requirements.MaxPredictionLatencyMs &&
               EstimatedModelSizeMB <= requirements.MaxModelSizeMB;
    }
}

/// <summary>
/// Architecture validation result with recommendations.
/// </summary>
public sealed record ArchitectureValidationResult
{
    /// <summary>
    /// Overall validation status.
    /// </summary>
    public required ArchitectureValidationStatus Status { get; init; }
    
    /// <summary>
    /// Validation issues identified.
    /// </summary>
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    
    /// <summary>
    /// Recommendations for improvement.
    /// </summary>
    public required IReadOnlyList<string> Recommendations { get; init; }
    
    /// <summary>
    /// Whether architecture is ready for production.
    /// </summary>
    public bool IsProductionReady => Status == ArchitectureValidationStatus.Valid;
}

// Configuration classes for pipeline components
public sealed record NormalizationConfig
{
    public required bool NormalizeNumerical { get; init; }
    public required NormalizationMethod Method { get; init; }
    public bool IsValid => true;
    
    public static NormalizationConfig CreateDefault() => new()
    {
        NormalizeNumerical = true,
        Method = NormalizationMethod.MinMax
    };
}

public sealed record CategoricalEncodingConfig
{
    public required EncodingMethod Method { get; init; }
    public bool IsValid => true;
    
    public static CategoricalEncodingConfig CreateOptimal() => new()
    {
        Method = EncodingMethod.OneHot
    };
}

public sealed record FeatureSelectionConfig
{
    public required int MaxFeatures { get; init; }
    public required double ImportanceThreshold { get; init; }
    public bool IsValid => MaxFeatures > 0;
    
    public static FeatureSelectionConfig CreateForItalianContent() => new()
    {
        MaxFeatures = 100,
        ImportanceThreshold = 0.001
    };
}

public sealed record TextFeatureConfig
{
    public required bool UseNGrams { get; init; }
    public required int MaxNGramLength { get; init; }
    
    public static TextFeatureConfig CreateForFilenames() => new()
    {
        UseNGrams = true,
        MaxNGramLength = 3
    };
}

public enum NormalizationMethod { MinMax, ZScore, Robust }
public enum EncodingMethod { OneHot, Label, Target }

/// <summary>
/// Architecture validation status levels.
/// </summary>
public enum ArchitectureValidationStatus
{
    /// <summary>
    /// Architecture is valid and ready for production.
    /// </summary>
    Valid = 0,
    
    /// <summary>
    /// Architecture has warnings but is usable.
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Architecture has errors that should be fixed.
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Architecture has critical issues preventing use.
    /// </summary>
    Critical = 3
}

// Configuration input classes
public sealed record ModelConfiguration
{
    public required ModelType ModelType { get; init; }
    public required AlgorithmType AlgorithmType { get; init; }
    public Dictionary<string, object>? CustomHyperparameters { get; init; }
}

public sealed record FeaturePipelineConfiguration
{
    public bool NormalizeFeatures { get; init; } = true;
    public int MaxFeatures { get; init; } = 100;
    public double FeatureImportanceThreshold { get; init; } = 0.001;
}

public sealed record ValidationConfiguration
{
    public int Folds { get; init; } = 5;
    public bool UseStratification { get; init; } = true;
    public int RandomSeed { get; init; } = 42;
}

public sealed record EvaluationMetricsConfiguration
{
    public double TargetAccuracy { get; init; } = 0.85;
    public bool GenerateConfusionMatrix { get; init; } = true;
    public bool CalculatePerCategoryMetrics { get; init; } = true;
}