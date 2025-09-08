using System.ComponentModel.DataAnnotations;

namespace MediaButler.ML.Models;

/// <summary>
/// Configuration settings for ML model training operations.
/// Optimized for Italian TV series classification with ARM32 deployment constraints.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable configuration that doesn't change during training
/// - Compose don't complect: Training settings independent from architecture and data
/// </remarks>
public sealed record TrainingConfiguration
{
    /// <summary>
    /// Maximum number of training epochs/iterations.
    /// Default: 100 for ARM32 optimization
    /// </summary>
    [Range(10, 1000)]
    public required int MaxEpochs { get; init; } = 100;

    /// <summary>
    /// Learning rate for the optimization algorithm.
    /// Default: 0.1 for stable convergence
    /// </summary>
    [Range(0.001, 1.0)]
    public required double LearningRate { get; init; } = 0.1;

    /// <summary>
    /// Batch size for training data processing.
    /// Default: 32 for memory efficiency on ARM32
    /// </summary>
    [Range(1, 1000)]
    public required int BatchSize { get; init; } = 32;

    /// <summary>
    /// Fraction of data to use for validation during training.
    /// Default: 0.2 (20% validation split)
    /// </summary>
    [Range(0.1, 0.4)]
    public required double ValidationSplit { get; init; } = 0.2;

    /// <summary>
    /// Random seed for reproducible training results.
    /// </summary>
    public required int RandomSeed { get; init; } = 42;

    /// <summary>
    /// Early stopping patience - stop training if no improvement for N epochs.
    /// Default: 10 epochs for ARM32 efficiency
    /// </summary>
    [Range(5, 50)]
    public required int EarlyStoppingPatience { get; init; } = 10;

    /// <summary>
    /// Minimum improvement threshold for early stopping.
    /// Default: 0.001 for sensitive stopping
    /// </summary>
    [Range(0.0001, 0.01)]
    public required double MinimumImprovement { get; init; } = 0.001;

    /// <summary>
    /// Enable data augmentation during training.
    /// Default: true for better generalization
    /// </summary>
    public required bool EnableDataAugmentation { get; init; } = true;

    /// <summary>
    /// Custom hyperparameters specific to the chosen algorithm.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomHyperparameters { get; init; } = 
        new Dictionary<string, object>();

    /// <summary>
    /// Training session identifier for progress tracking.
    /// </summary>
    public required string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Maximum training time in minutes before automatic termination.
    /// Default: 30 minutes for ARM32 constraints
    /// </summary>
    [Range(5, 480)] // 5 minutes to 8 hours
    public required int MaxTrainingTimeMinutes { get; init; } = 30;

    /// <summary>
    /// Creates a default training configuration optimized for Italian content classification.
    /// </summary>
    public static TrainingConfiguration CreateDefault() => new()
    {
        MaxEpochs = 100,
        LearningRate = 0.1,
        BatchSize = 32,
        ValidationSplit = 0.2,
        RandomSeed = 42,
        EarlyStoppingPatience = 10,
        MinimumImprovement = 0.001,
        EnableDataAugmentation = true,
        MaxTrainingTimeMinutes = 30,
        SessionId = Guid.NewGuid().ToString()
    };

    /// <summary>
    /// Creates a fast training configuration for testing and development.
    /// </summary>
    public static TrainingConfiguration CreateFast() => new()
    {
        MaxEpochs = 25,
        LearningRate = 0.2,
        BatchSize = 64,
        ValidationSplit = 0.15,
        RandomSeed = 123,
        EarlyStoppingPatience = 5,
        MinimumImprovement = 0.005,
        EnableDataAugmentation = false,
        MaxTrainingTimeMinutes = 10,
        SessionId = Guid.NewGuid().ToString()
    };
}

/// <summary>
/// Configuration for model evaluation operations.
/// </summary>
public sealed record EvaluationConfiguration
{
    /// <summary>
    /// Metrics to calculate during evaluation.
    /// </summary>
    public required IReadOnlyList<EvaluationMetricType> Metrics { get; init; }

    /// <summary>
    /// Generate detailed confusion matrix.
    /// Default: true for classification analysis
    /// </summary>
    public required bool GenerateConfusionMatrix { get; init; } = true;

    /// <summary>
    /// Calculate per-category performance metrics.
    /// Default: true for Italian content analysis
    /// </summary>
    public required bool CalculatePerCategoryMetrics { get; init; } = true;

    /// <summary>
    /// Include confidence score analysis in evaluation.
    /// </summary>
    public required bool IncludeConfidenceAnalysis { get; init; } = true;

    /// <summary>
    /// Random seed for consistent evaluation results.
    /// </summary>
    public required int RandomSeed { get; init; } = 42;

    /// <summary>
    /// Creates comprehensive evaluation configuration.
    /// </summary>
    public static EvaluationConfiguration CreateComprehensive() => new()
    {
        Metrics = new[]
        {
            EvaluationMetricType.Accuracy,
            EvaluationMetricType.MacroF1Score,
            EvaluationMetricType.WeightedF1Score,
            EvaluationMetricType.Precision,
            EvaluationMetricType.Recall,
            EvaluationMetricType.LogLoss
        }.AsReadOnly(),
        GenerateConfusionMatrix = true,
        CalculatePerCategoryMetrics = true,
        IncludeConfidenceAnalysis = true,
        RandomSeed = 42
    };
}

/// <summary>
/// Information about a successfully trained model.
/// </summary>
public sealed record TrainedModelInfo
{
    /// <summary>
    /// Unique identifier for the trained model.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Model architecture used for training.
    /// </summary>
    public required MLModelArchitecture Architecture { get; init; }

    /// <summary>
    /// Training configuration used.
    /// </summary>
    public required TrainingConfiguration TrainingConfig { get; init; }

    /// <summary>
    /// Final training metrics.
    /// </summary>
    public required TrainingMetrics TrainingMetrics { get; init; }

    /// <summary>
    /// Model performance on validation data.
    /// </summary>
    public required ModelPerformanceMetrics ValidationMetrics { get; init; }

    /// <summary>
    /// File path where the model is saved.
    /// </summary>
    public required string ModelPath { get; init; }

    /// <summary>
    /// Timestamp when training was completed.
    /// </summary>
    public required DateTime TrainingCompletedAt { get; init; }

    /// <summary>
    /// Total training duration.
    /// </summary>
    public required TimeSpan TrainingDuration { get; init; }

    /// <summary>
    /// Number of training samples used.
    /// </summary>
    public required int TrainingSampleCount { get; init; }

    /// <summary>
    /// Model version for tracking and compatibility.
    /// </summary>
    public required string ModelVersion { get; init; }

    /// <summary>
    /// Indicates if the model meets production quality standards.
    /// </summary>
    public bool IsProductionReady => 
        ValidationMetrics.Accuracy >= Architecture.EvaluationMetrics.TargetAccuracy &&
        ValidationMetrics.MacroF1Score >= Architecture.EvaluationMetrics.MinimumF1Score;
}

/// <summary>
/// Metrics collected during the model training process.
/// </summary>
public sealed record TrainingMetrics
{
    /// <summary>
    /// Training loss progression over epochs.
    /// </summary>
    public required IReadOnlyList<double> TrainingLossHistory { get; init; }

    /// <summary>
    /// Validation loss progression over epochs.
    /// </summary>
    public required IReadOnlyList<double> ValidationLossHistory { get; init; }

    /// <summary>
    /// Training accuracy progression over epochs.
    /// </summary>
    public required IReadOnlyList<double> TrainingAccuracyHistory { get; init; }

    /// <summary>
    /// Validation accuracy progression over epochs.
    /// </summary>
    public required IReadOnlyList<double> ValidationAccuracyHistory { get; init; }

    /// <summary>
    /// Final training loss value.
    /// </summary>
    public required double FinalTrainingLoss { get; init; }

    /// <summary>
    /// Final validation loss value.
    /// </summary>
    public required double FinalValidationLoss { get; init; }

    /// <summary>
    /// Epoch at which training was stopped.
    /// </summary>
    public required int EpochsStopped { get; init; }

    /// <summary>
    /// Reason for training termination.
    /// </summary>
    public required TrainingStopReason StopReason { get; init; }

    /// <summary>
    /// Learning rate used during training.
    /// </summary>
    public required double LearningRateUsed { get; init; }
}

/// <summary>
/// Reason why training was terminated.
/// </summary>
public enum TrainingStopReason
{
    /// <summary>
    /// Training completed all specified epochs.
    /// </summary>
    MaxEpochsReached,

    /// <summary>
    /// Early stopping triggered due to no improvement.
    /// </summary>
    EarlyStopping,

    /// <summary>
    /// Training stopped due to time limit.
    /// </summary>
    TimeLimit,

    /// <summary>
    /// Training cancelled by user request.
    /// </summary>
    UserCancelled,

    /// <summary>
    /// Training stopped due to error condition.
    /// </summary>
    Error,

    /// <summary>
    /// Target accuracy achieved.
    /// </summary>
    TargetAccuracyReached
}

/// <summary>
/// Model performance metrics on evaluation data.
/// </summary>
public sealed record ModelPerformanceMetrics
{
    /// <summary>
    /// Overall classification accuracy.
    /// </summary>
    public required double Accuracy { get; init; }

    /// <summary>
    /// Macro-averaged F1 score.
    /// </summary>
    public required double MacroF1Score { get; init; }

    /// <summary>
    /// Weighted F1 score.
    /// </summary>
    public required double WeightedF1Score { get; init; }

    /// <summary>
    /// Macro-averaged precision.
    /// </summary>
    public required double MacroPrecision { get; init; }

    /// <summary>
    /// Macro-averaged recall.
    /// </summary>
    public required double MacroRecall { get; init; }

    /// <summary>
    /// Log loss (cross-entropy loss).
    /// </summary>
    public required double LogLoss { get; init; }

    /// <summary>
    /// Per-category performance metrics.
    /// </summary>
    public required IReadOnlyDictionary<string, CategoryPerformanceMetrics> PerCategoryMetrics { get; init; }

    /// <summary>
    /// Confusion matrix for detailed error analysis.
    /// </summary>
    public required ConfusionMatrix ConfusionMatrix { get; init; }

    /// <summary>
    /// Confidence score distribution analysis.
    /// </summary>
    public required ConfidenceAnalysis ConfidenceDistribution { get; init; }
}

/// <summary>
/// Performance metrics for a specific category.
/// </summary>
public sealed record CategoryPerformanceMetrics
{
    /// <summary>
    /// Category name.
    /// </summary>
    public required string CategoryName { get; init; }

    /// <summary>
    /// Precision for this category.
    /// </summary>
    public required double Precision { get; init; }

    /// <summary>
    /// Recall for this category.
    /// </summary>
    public required double Recall { get; init; }

    /// <summary>
    /// F1 score for this category.
    /// </summary>
    public required double F1Score { get; init; }

    /// <summary>
    /// Number of true positive predictions.
    /// </summary>
    public required int TruePositives { get; init; }

    /// <summary>
    /// Number of false positive predictions.
    /// </summary>
    public required int FalsePositives { get; init; }

    /// <summary>
    /// Number of false negative predictions.
    /// </summary>
    public required int FalseNegatives { get; init; }

    /// <summary>
    /// Total number of samples for this category.
    /// </summary>
    public required int SampleCount { get; init; }
}

/// <summary>
/// Confusion matrix for classification analysis.
/// </summary>
public sealed record ConfusionMatrix
{
    /// <summary>
    /// Category labels in order.
    /// </summary>
    public required IReadOnlyList<string> Labels { get; init; }

    /// <summary>
    /// Confusion matrix values [actual][predicted].
    /// </summary>
    public required int[,] Matrix { get; init; }

    /// <summary>
    /// Total number of predictions in the matrix.
    /// </summary>
    public required int TotalPredictions { get; init; }
}

/// <summary>
/// Analysis of prediction confidence scores.
/// </summary>
public sealed record ConfidenceAnalysis
{
    /// <summary>
    /// Mean confidence score across all predictions.
    /// </summary>
    public required double MeanConfidence { get; init; }

    /// <summary>
    /// Median confidence score.
    /// </summary>
    public required double MedianConfidence { get; init; }

    /// <summary>
    /// Standard deviation of confidence scores.
    /// </summary>
    public required double ConfidenceStdDev { get; init; }

    /// <summary>
    /// Distribution of confidence scores in bins.
    /// </summary>
    public required IReadOnlyDictionary<string, int> ConfidenceBins { get; init; }

    /// <summary>
    /// Percentage of predictions with high confidence (>0.8).
    /// </summary>
    public required double HighConfidencePercentage { get; init; }

    /// <summary>
    /// Percentage of predictions with low confidence (less than 0.5).
    /// </summary>
    public required double LowConfidencePercentage { get; init; }
}

/// <summary>
/// Results from cross-validation analysis.
/// </summary>
public sealed record CrossValidationResult
{
    /// <summary>
    /// Mean accuracy across all folds.
    /// </summary>
    public required double MeanAccuracy { get; init; }

    /// <summary>
    /// Standard deviation of accuracy across folds.
    /// </summary>
    public required double AccuracyStdDev { get; init; }

    /// <summary>
    /// Mean F1 score across all folds.
    /// </summary>
    public required double MeanF1Score { get; init; }

    /// <summary>
    /// Standard deviation of F1 score across folds.
    /// </summary>
    public required double F1ScoreStdDev { get; init; }

    /// <summary>
    /// Individual fold results.
    /// </summary>
    public required IReadOnlyList<FoldResult> FoldResults { get; init; }

    /// <summary>
    /// Number of cross-validation folds performed.
    /// </summary>
    public required int NumberOfFolds { get; init; }

    /// <summary>
    /// Total cross-validation duration.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Indicates if cross-validation results meet quality standards.
    /// </summary>
    public bool IsStable => AccuracyStdDev <= 0.05 && F1ScoreStdDev <= 0.05;
}

/// <summary>
/// Results from a single cross-validation fold.
/// </summary>
public sealed record FoldResult
{
    /// <summary>
    /// Fold number (0-based).
    /// </summary>
    public required int FoldNumber { get; init; }

    /// <summary>
    /// Accuracy for this fold.
    /// </summary>
    public required double Accuracy { get; init; }

    /// <summary>
    /// F1 score for this fold.
    /// </summary>
    public required double F1Score { get; init; }

    /// <summary>
    /// Precision for this fold.
    /// </summary>
    public required double Precision { get; init; }

    /// <summary>
    /// Recall for this fold.
    /// </summary>
    public required double Recall { get; init; }

    /// <summary>
    /// Training time for this fold.
    /// </summary>
    public required TimeSpan TrainingTime { get; init; }

    /// <summary>
    /// Number of training samples in this fold.
    /// </summary>
    public required int TrainingSampleCount { get; init; }

    /// <summary>
    /// Number of validation samples in this fold.
    /// </summary>
    public required int ValidationSampleCount { get; init; }
}

/// <summary>
/// ML.NET training pipeline configuration.
/// </summary>
public sealed record MLTrainingPipeline
{
    /// <summary>
    /// Pipeline identifier.
    /// </summary>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Model architecture this pipeline is configured for.
    /// </summary>
    public required MLModelArchitecture Architecture { get; init; }

    /// <summary>
    /// Feature processing pipeline.
    /// </summary>
    public required FeaturePipelineConfig FeaturePipeline { get; init; }

    /// <summary>
    /// Transformation steps in order.
    /// </summary>
    public required IReadOnlyList<TransformationStep> TransformationSteps { get; init; }

    /// <summary>
    /// Training algorithm configuration.
    /// </summary>
    public required TrainingAlgorithmConfig AlgorithmConfig { get; init; }

    /// <summary>
    /// Pipeline creation timestamp.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Indicates if the pipeline is valid and ready for training.
    /// </summary>
    public bool IsValid => 
        TransformationSteps.Count > 0 && 
        AlgorithmConfig.IsValid;
}

/// <summary>
/// Configuration for a single transformation step in the ML pipeline.
/// </summary>
public sealed record TransformationStep
{
    /// <summary>
    /// Transformation type name.
    /// </summary>
    public required string TransformationType { get; init; }

    /// <summary>
    /// Input column names for this transformation.
    /// </summary>
    public required IReadOnlyList<string> InputColumns { get; init; }

    /// <summary>
    /// Output column names from this transformation.
    /// </summary>
    public required IReadOnlyList<string> OutputColumns { get; init; }

    /// <summary>
    /// Transformation-specific parameters.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }

    /// <summary>
    /// Order of this step in the pipeline.
    /// </summary>
    public required int Order { get; init; }
}

/// <summary>
/// Configuration for the training algorithm.
/// </summary>
public sealed record TrainingAlgorithmConfig
{
    /// <summary>
    /// Algorithm type being used.
    /// </summary>
    public required AlgorithmType AlgorithmType { get; init; }

    /// <summary>
    /// Algorithm-specific hyperparameters.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Hyperparameters { get; init; }

    /// <summary>
    /// Label column name in the training data.
    /// </summary>
    public required string LabelColumnName { get; init; } = "Category";

    /// <summary>
    /// Features column name after transformation.
    /// </summary>
    public required string FeaturesColumnName { get; init; } = "Features";

    /// <summary>
    /// Prediction column name for output.
    /// </summary>
    public required string PredictionColumnName { get; init; } = "PredictedLabel";

    /// <summary>
    /// Score column name for confidence values.
    /// </summary>
    public required string ScoreColumnName { get; init; } = "Score";

    /// <summary>
    /// Indicates if the configuration is valid.
    /// </summary>
    public bool IsValid => 
        !string.IsNullOrEmpty(LabelColumnName) && 
        !string.IsNullOrEmpty(FeaturesColumnName) &&
        Hyperparameters.Count > 0;
}

/// <summary>
/// Model persistence information.
/// </summary>
public sealed record ModelPersistenceInfo
{
    /// <summary>
    /// File path where the model was saved.
    /// </summary>
    public required string ModelPath { get; init; }

    /// <summary>
    /// Model file size in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// Model metadata saved with the model.
    /// </summary>
    public required ModelMetadata Metadata { get; init; }

    /// <summary>
    /// Timestamp when the model was saved.
    /// </summary>
    public required DateTime SavedAt { get; init; }

    /// <summary>
    /// Checksum for model integrity verification.
    /// </summary>
    public required string Checksum { get; init; }

    /// <summary>
    /// Model version for compatibility tracking.
    /// </summary>
    public required string ModelVersion { get; init; }
}

/// <summary>
/// Progress information for ongoing training operations.
/// </summary>
public sealed record TrainingProgress
{
    /// <summary>
    /// Training session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Current epoch number (0-based).
    /// </summary>
    public required int CurrentEpoch { get; init; }

    /// <summary>
    /// Total number of planned epochs.
    /// </summary>
    public required int TotalEpochs { get; init; }

    /// <summary>
    /// Current training loss.
    /// </summary>
    public required double CurrentTrainingLoss { get; init; }

    /// <summary>
    /// Current validation loss.
    /// </summary>
    public required double CurrentValidationLoss { get; init; }

    /// <summary>
    /// Current training accuracy.
    /// </summary>
    public required double CurrentTrainingAccuracy { get; init; }

    /// <summary>
    /// Current validation accuracy.
    /// </summary>
    public required double CurrentValidationAccuracy { get; init; }

    /// <summary>
    /// Elapsed training time.
    /// </summary>
    public required TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Estimated remaining time.
    /// </summary>
    public required TimeSpan EstimatedRemainingTime { get; init; }

    /// <summary>
    /// Training completion percentage (0-100).
    /// </summary>
    public required double CompletionPercentage { get; init; }

    /// <summary>
    /// Current training phase.
    /// </summary>
    public required TrainingPhase CurrentPhase { get; init; }

    /// <summary>
    /// Status message for current operation.
    /// </summary>
    public required string StatusMessage { get; init; }
}

/// <summary>
/// Current phase of the training process.
/// </summary>
public enum TrainingPhase
{
    /// <summary>
    /// Initializing training pipeline.
    /// </summary>
    Initializing,

    /// <summary>
    /// Loading and preprocessing training data.
    /// </summary>
    DataLoading,

    /// <summary>
    /// Training the model.
    /// </summary>
    Training,

    /// <summary>
    /// Validating model performance.
    /// </summary>
    Validation,

    /// <summary>
    /// Saving the trained model.
    /// </summary>
    Saving,

    /// <summary>
    /// Training completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Training failed with error.
    /// </summary>
    Failed
}

/// <summary>
/// Metadata associated with a trained model.
/// </summary>
public sealed record ModelMetadata
{
    /// <summary>
    /// Model name or identifier.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Model description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Model version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Author or creator of the model.
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Additional model tags.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Tags { get; init; }

    /// <summary>
    /// Creates default model metadata.
    /// </summary>
    public static ModelMetadata CreateDefault() => new()
    {
        ModelName = "Italian TV Series Classifier",
        Description = "ML model for classifying Italian TV series from filenames",
        Version = "1.0.0",
        Author = "MediaButler System",
        CreatedAt = DateTime.UtcNow,
        Tags = new Dictionary<string, string>
        {
            ["type"] = "classification",
            ["domain"] = "italian-tv-series",
            ["target"] = "arm32"
        }.AsReadOnly()
    };
}

/// <summary>
/// Result of model evaluation operations.
/// </summary>
public sealed record ModelEvaluationResult
{
    /// <summary>
    /// Overall performance metrics.
    /// </summary>
    public required ModelPerformanceMetrics OverallMetrics { get; init; }

    /// <summary>
    /// Configuration used for evaluation.
    /// </summary>
    public required EvaluationConfiguration EvaluationConfig { get; init; }

    /// <summary>
    /// Number of samples used in evaluation.
    /// </summary>
    public required int SampleCount { get; init; }

    /// <summary>
    /// Duration of the evaluation process.
    /// </summary>
    public required TimeSpan EvaluationDuration { get; init; }

    /// <summary>
    /// Overall quality assessment of the model.
    /// </summary>
    public required ModelQuality ModelQuality { get; init; }

    /// <summary>
    /// Indicates if the model meets production readiness criteria.
    /// </summary>
    public required bool IsProductionReady { get; init; }

    /// <summary>
    /// Textual assessment of model quality and recommendations.
    /// </summary>
    public required string QualityAssessment { get; init; }
}

/// <summary>
/// Quality assessment of the trained model.
/// </summary>
public enum ModelQuality
{
    /// <summary>
    /// Excellent model performance, ready for production.
    /// </summary>
    Excellent,

    /// <summary>
    /// Good model performance, suitable for most use cases.
    /// </summary>
    Good,

    /// <summary>
    /// Fair model performance, may need improvements.
    /// </summary>
    Fair,

    /// <summary>
    /// Poor model performance, requires significant improvements.
    /// </summary>
    Poor
}

/// <summary>
/// Resource requirements estimate for training operations.
/// </summary>
public sealed record TrainingResourceEstimate
{
    /// <summary>
    /// Estimated peak memory usage in MB.
    /// </summary>
    public required double EstimatedPeakMemoryMB { get; init; }

    /// <summary>
    /// Estimated training duration.
    /// </summary>
    public required TimeSpan EstimatedTrainingTime { get; init; }

    /// <summary>
    /// Estimated CPU utilization percentage.
    /// </summary>
    public required double EstimatedCpuUtilization { get; init; }

    /// <summary>
    /// Estimated temporary disk space required in MB.
    /// </summary>
    public required double EstimatedTempDiskSpaceMB { get; init; }

    /// <summary>
    /// Number of training samples analyzed for this estimate.
    /// </summary>
    public required int SampleCount { get; init; }

    /// <summary>
    /// Number of features in the training data.
    /// </summary>
    public required int FeatureCount { get; init; }

    /// <summary>
    /// Confidence level of the estimate (0-1).
    /// </summary>
    public required double EstimateConfidence { get; init; }

    /// <summary>
    /// Indicates if the estimated requirements fit within ARM32 constraints.
    /// </summary>
    public bool FitsARM32Constraints => 
        EstimatedPeakMemoryMB <= 200 && // Conservative memory limit
        EstimatedTrainingTime <= TimeSpan.FromMinutes(45); // Reasonable training time
}