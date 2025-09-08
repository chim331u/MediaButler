using System.ComponentModel.DataAnnotations;

namespace MediaButler.ML.Models;

/// <summary>
/// Configuration for hyperparameter optimization operations.
/// Optimized for Italian TV series classification with ARM32 constraints.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable optimization configuration
/// - Compose don't complect: Optimization logic independent from training
/// </remarks>
public sealed record HyperparameterOptimizationConfig
{
    /// <summary>
    /// Optimization algorithm to use.
    /// Default: GridSearch for deterministic results
    /// </summary>
    public required OptimizationAlgorithm Algorithm { get; init; } = OptimizationAlgorithm.GridSearch;

    /// <summary>
    /// Maximum number of optimization iterations.
    /// Default: 50 for ARM32 efficiency
    /// </summary>
    [Range(10, 200)]
    public required int MaxIterations { get; init; } = 50;

    /// <summary>
    /// Cross-validation folds for each hyperparameter evaluation.
    /// Default: 3 for faster optimization
    /// </summary>
    [Range(2, 5)]
    public required int CrossValidationFolds { get; init; } = 3;

    /// <summary>
    /// Metric to optimize for.
    /// Default: Accuracy for Italian content classification
    /// </summary>
    public required OptimizationMetric TargetMetric { get; init; } = OptimizationMetric.Accuracy;

    /// <summary>
    /// Hyperparameter search space definitions.
    /// </summary>
    public required IReadOnlyList<HyperparameterSearchSpace> SearchSpaces { get; init; }

    /// <summary>
    /// Random seed for reproducible optimization.
    /// </summary>
    public required int RandomSeed { get; init; } = 42;

    /// <summary>
    /// Maximum optimization time in minutes.
    /// Default: 60 minutes for ARM32 constraints
    /// </summary>
    [Range(15, 300)]
    public required int MaxOptimizationTimeMinutes { get; init; } = 60;

    /// <summary>
    /// Early stopping if no improvement for N iterations.
    /// Default: 10 iterations for efficiency
    /// </summary>
    [Range(5, 25)]
    public required int EarlyStoppingPatience { get; init; } = 10;

    /// <summary>
    /// Minimum improvement threshold for early stopping.
    /// </summary>
    [Range(0.001, 0.01)]
    public required double MinimumImprovement { get; init; } = 0.005;

    /// <summary>
    /// Creates default optimization configuration for LightGBM algorithm.
    /// </summary>
    public static HyperparameterOptimizationConfig CreateForLightGBM() => new()
    {
        Algorithm = OptimizationAlgorithm.RandomSearch,
        MaxIterations = 30,
        CrossValidationFolds = 3,
        TargetMetric = OptimizationMetric.WeightedF1Score,
        MaxOptimizationTimeMinutes = 45,
        EarlyStoppingPatience = 8,
        MinimumImprovement = 0.003,
        RandomSeed = 42,
        SearchSpaces = new[]
        {
            new HyperparameterSearchSpace
            {
                ParameterName = "learning_rate",
                ParameterType = HyperparameterType.Continuous,
                MinValue = 0.05,
                MaxValue = 0.3,
                DefaultValue = 0.1
            },
            new HyperparameterSearchSpace
            {
                ParameterName = "num_leaves",
                ParameterType = HyperparameterType.Integer,
                MinValue = 15,
                MaxValue = 63,
                DefaultValue = 31
            },
            new HyperparameterSearchSpace
            {
                ParameterName = "max_depth",
                ParameterType = HyperparameterType.Integer,
                MinValue = 3,
                MaxValue = 12,
                DefaultValue = 6
            },
            new HyperparameterSearchSpace
            {
                ParameterName = "min_data_per_leaf",
                ParameterType = HyperparameterType.Integer,
                MinValue = 5,
                MaxValue = 25,
                DefaultValue = 10
            }
        }.AsReadOnly()
    };

    /// <summary>
    /// Creates optimization configuration for FastTree algorithm.
    /// </summary>
    public static HyperparameterOptimizationConfig CreateForFastTree() => new()
    {
        Algorithm = OptimizationAlgorithm.GridSearch,
        MaxIterations = 40,
        CrossValidationFolds = 3,
        TargetMetric = OptimizationMetric.Accuracy,
        MaxOptimizationTimeMinutes = 30,
        EarlyStoppingPatience = 10,
        MinimumImprovement = 0.005,
        RandomSeed = 42,
        SearchSpaces = new[]
        {
            new HyperparameterSearchSpace
            {
                ParameterName = "learning_rate",
                ParameterType = HyperparameterType.Continuous,
                MinValue = 0.1,
                MaxValue = 0.3,
                DefaultValue = 0.15
            },
            new HyperparameterSearchSpace
            {
                ParameterName = "num_trees",
                ParameterType = HyperparameterType.Integer,
                MinValue = 100,
                MaxValue = 500,
                DefaultValue = 300
            },
            new HyperparameterSearchSpace
            {
                ParameterName = "num_leaves",
                ParameterType = HyperparameterType.Integer,
                MinValue = 15,
                MaxValue = 63,
                DefaultValue = 31
            }
        }.AsReadOnly()
    };
}

/// <summary>
/// Optimization algorithm types.
/// </summary>
public enum OptimizationAlgorithm
{
    /// <summary>
    /// Exhaustive grid search over parameter space.
    /// </summary>
    GridSearch,

    /// <summary>
    /// Random sampling of parameter space.
    /// </summary>
    RandomSearch,

    /// <summary>
    /// Bayesian optimization for efficient search.
    /// </summary>
    BayesianOptimization
}

/// <summary>
/// Metrics available for hyperparameter optimization.
/// </summary>
public enum OptimizationMetric
{
    /// <summary>
    /// Classification accuracy.
    /// </summary>
    Accuracy,

    /// <summary>
    /// Macro-averaged F1 score.
    /// </summary>
    MacroF1Score,

    /// <summary>
    /// Weighted F1 score (recommended for imbalanced data).
    /// </summary>
    WeightedF1Score,

    /// <summary>
    /// Macro-averaged precision.
    /// </summary>
    MacroPrecision,

    /// <summary>
    /// Macro-averaged recall.
    /// </summary>
    MacroRecall,

    /// <summary>
    /// Log loss (lower is better).
    /// </summary>
    LogLoss
}

/// <summary>
/// Search space definition for a single hyperparameter.
/// </summary>
public sealed record HyperparameterSearchSpace
{
    /// <summary>
    /// Name of the hyperparameter.
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// Type of hyperparameter (continuous, integer, categorical).
    /// </summary>
    public required HyperparameterType ParameterType { get; init; }

    /// <summary>
    /// Minimum value for continuous/integer parameters.
    /// </summary>
    public double? MinValue { get; init; }

    /// <summary>
    /// Maximum value for continuous/integer parameters.
    /// </summary>
    public double? MaxValue { get; init; }

    /// <summary>
    /// Default/starting value for the parameter.
    /// </summary>
    public required object DefaultValue { get; init; }

    /// <summary>
    /// Discrete values for categorical parameters.
    /// </summary>
    public IReadOnlyList<object>? CategoricalValues { get; init; }

    /// <summary>
    /// Step size for integer parameters.
    /// </summary>
    public int? StepSize { get; init; } = 1;

    /// <summary>
    /// Indicates if the search space uses log scale.
    /// </summary>
    public bool UseLogScale { get; init; } = false;

    /// <summary>
    /// Importance weight for this parameter in optimization.
    /// </summary>
    [Range(0.1, 2.0)]
    public double ImportanceWeight { get; init; } = 1.0;
}

/// <summary>
/// Types of hyperparameters for optimization.
/// </summary>
public enum HyperparameterType
{
    /// <summary>
    /// Continuous real-valued parameter.
    /// </summary>
    Continuous,

    /// <summary>
    /// Integer-valued parameter.
    /// </summary>
    Integer,

    /// <summary>
    /// Categorical parameter with discrete choices.
    /// </summary>
    Categorical
}

/// <summary>
/// Results from hyperparameter optimization.
/// </summary>
public sealed record HyperparameterOptimizationResult
{
    /// <summary>
    /// Best hyperparameter configuration found.
    /// </summary>
    public required IReadOnlyDictionary<string, object> BestHyperparameters { get; init; }

    /// <summary>
    /// Best cross-validation score achieved.
    /// </summary>
    public required double BestScore { get; init; }

    /// <summary>
    /// Standard deviation of the best score across CV folds.
    /// </summary>
    public required double BestScoreStdDev { get; init; }

    /// <summary>
    /// Number of optimization iterations performed.
    /// </summary>
    public required int IterationsPerformed { get; init; }

    /// <summary>
    /// Total optimization duration.
    /// </summary>
    public required TimeSpan OptimizationDuration { get; init; }

    /// <summary>
    /// Optimization algorithm used.
    /// </summary>
    public required OptimizationAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Target metric that was optimized.
    /// </summary>
    public required OptimizationMetric TargetMetric { get; init; }

    /// <summary>
    /// Detailed results for each optimization iteration.
    /// </summary>
    public required IReadOnlyList<OptimizationIteration> IterationResults { get; init; }

    /// <summary>
    /// Convergence analysis of the optimization process.
    /// </summary>
    public required OptimizationConvergence Convergence { get; init; }

    /// <summary>
    /// Model architecture with optimized hyperparameters applied.
    /// </summary>
    public required MLModelArchitecture OptimizedArchitecture { get; init; }

    /// <summary>
    /// Indicates if optimization found significantly better parameters.
    /// </summary>
    public bool SignificantImprovement => BestScore > 0 && Convergence.ConvergedSuccessfully;
}

/// <summary>
/// Results from a single optimization iteration.
/// </summary>
public sealed record OptimizationIteration
{
    /// <summary>
    /// Iteration number (0-based).
    /// </summary>
    public required int IterationNumber { get; init; }

    /// <summary>
    /// Hyperparameters tested in this iteration.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Hyperparameters { get; init; }

    /// <summary>
    /// Cross-validation score for this configuration.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Standard deviation of score across CV folds.
    /// </summary>
    public required double ScoreStdDev { get; init; }

    /// <summary>
    /// Training time for this configuration.
    /// </summary>
    public required TimeSpan TrainingTime { get; init; }

    /// <summary>
    /// Memory usage during training.
    /// </summary>
    public required double MemoryUsageMB { get; init; }

    /// <summary>
    /// Indicates if this iteration produced the best score so far.
    /// </summary>
    public required bool IsBestSoFar { get; init; }
}

/// <summary>
/// Analysis of optimization convergence.
/// </summary>
public sealed record OptimizationConvergence
{
    /// <summary>
    /// Indicates if optimization converged successfully.
    /// </summary>
    public required bool ConvergedSuccessfully { get; init; }

    /// <summary>
    /// Iteration at which convergence was detected.
    /// </summary>
    public required int ConvergenceIteration { get; init; }

    /// <summary>
    /// Best score improvement over baseline.
    /// </summary>
    public required double ScoreImprovement { get; init; }

    /// <summary>
    /// Percentage improvement over baseline.
    /// </summary>
    public required double ImprovementPercentage { get; init; }

    /// <summary>
    /// Reason for optimization termination.
    /// </summary>
    public required OptimizationStopReason StopReason { get; init; }

    /// <summary>
    /// Quality assessment of the optimization process.
    /// </summary>
    public required OptimizationQuality Quality { get; init; }
}

/// <summary>
/// Reason for optimization termination.
/// </summary>
public enum OptimizationStopReason
{
    /// <summary>
    /// Completed all planned iterations.
    /// </summary>
    MaxIterationsReached,

    /// <summary>
    /// Early stopping due to convergence.
    /// </summary>
    EarlyStopping,

    /// <summary>
    /// Time limit exceeded.
    /// </summary>
    TimeLimit,

    /// <summary>
    /// User cancelled optimization.
    /// </summary>
    UserCancelled,

    /// <summary>
    /// Target score achieved.
    /// </summary>
    TargetScoreReached,

    /// <summary>
    /// Error during optimization.
    /// </summary>
    Error
}

/// <summary>
/// Quality assessment of optimization results.
/// </summary>
public enum OptimizationQuality
{
    /// <summary>
    /// Excellent optimization with significant improvement.
    /// </summary>
    Excellent,

    /// <summary>
    /// Good optimization with notable improvement.
    /// </summary>
    Good,

    /// <summary>
    /// Fair optimization with modest improvement.
    /// </summary>
    Fair,

    /// <summary>
    /// Poor optimization with minimal improvement.
    /// </summary>
    Poor,

    /// <summary>
    /// Failed optimization with no improvement.
    /// </summary>
    Failed
}

/// <summary>
/// Configuration for model validation after loading.
/// </summary>
public sealed record ModelValidationConfig
{
    /// <summary>
    /// Verify model integrity with checksum validation.
    /// Default: true for production safety
    /// </summary>
    public required bool VerifyChecksum { get; init; } = true;

    /// <summary>
    /// Validate model schema compatibility.
    /// Default: true for version safety
    /// </summary>
    public required bool ValidateSchema { get; init; } = true;

    /// <summary>
    /// Perform basic prediction test with sample data.
    /// Default: true for functionality verification
    /// </summary>
    public required bool TestPrediction { get; init; } = true;

    /// <summary>
    /// Sample filenames for prediction testing.
    /// </summary>
    public required IReadOnlyList<string> TestFilenames { get; init; }

    /// <summary>
    /// Maximum allowed model age in days.
    /// Default: 90 days for freshness
    /// </summary>
    [Range(1, 365)]
    public required int MaxModelAgeDays { get; init; } = 90;

    /// <summary>
    /// Required minimum accuracy for model validation.
    /// Default: 0.7 for basic quality assurance
    /// </summary>
    [Range(0.5, 1.0)]
    public required double MinimumAccuracy { get; init; } = 0.7;

    /// <summary>
    /// Creates default validation configuration.
    /// </summary>
    public static ModelValidationConfig CreateDefault() => new()
    {
        VerifyChecksum = true,
        ValidateSchema = true,
        TestPrediction = true,
        MaxModelAgeDays = 90,
        MinimumAccuracy = 0.7,
        TestFilenames = new[]
        {
            "Breaking.Bad.S01E01.1080p.BluRay.x264-NovaRip.mkv",
            "Il.Commissario.Montalbano.S14E03.ITA.HDTV.x264-DarkSideMux.avi",
            "Gomorra.S04E12.FINAL.ITA.720p.HDTV.x264-Pir8.mkv"
        }.AsReadOnly()
    };

    /// <summary>
    /// Creates strict validation configuration for production deployment.
    /// </summary>
    public static ModelValidationConfig CreateStrict() => new()
    {
        VerifyChecksum = true,
        ValidateSchema = true,
        TestPrediction = true,
        MaxModelAgeDays = 30,
        MinimumAccuracy = 0.85,
        TestFilenames = new[]
        {
            "Breaking.Bad.S01E01.1080p.BluRay.x264-NovaRip.mkv",
            "Il.Commissario.Montalbano.S14E03.ITA.HDTV.x264-DarkSideMux.avi",
            "Gomorra.S04E12.FINAL.ITA.720p.HDTV.x264-Pir8.mkv",
            "Suburra.S03E06.ITA.1080p.NF.WEB-DL.DDP5.1.x264-Pir8.mkv",
            "La.Casa.di.Carta.S05E10.FINAL.ITA.720p.NF.WEB-DL.DDP5.1.x264-MeM.mkv"
        }.AsReadOnly()
    };
}

/// <summary>
/// Rules for training data validation.
/// </summary>
public sealed record TrainingDataValidationRules
{
    /// <summary>
    /// Minimum number of samples required for training.
    /// Default: 100 samples for basic model quality
    /// </summary>
    [Range(10, 10000)]
    public required int MinimumSampleCount { get; init; } = 100;

    /// <summary>
    /// Minimum number of samples per category.
    /// Default: 5 samples per category for balance
    /// </summary>
    [Range(2, 100)]
    public required int MinimumSamplesPerCategory { get; init; } = 5;

    /// <summary>
    /// Maximum allowed class imbalance ratio.
    /// Default: 10:1 ratio for reasonable balance
    /// </summary>
    [Range(2.0, 50.0)]
    public required double MaxClassImbalanceRatio { get; init; } = 10.0;

    /// <summary>
    /// Minimum filename length for meaningful features.
    /// Default: 10 characters for basic content
    /// </summary>
    [Range(5, 100)]
    public required int MinimumFilenameLength { get; init; } = 10;

    /// <summary>
    /// Maximum percentage of duplicate filenames allowed.
    /// Default: 5% duplicates maximum
    /// </summary>
    [Range(0.0, 0.2)]
    public required double MaxDuplicatePercentage { get; init; } = 0.05;

    /// <summary>
    /// Minimum confidence threshold for training samples.
    /// Default: 0.8 for high-quality training data
    /// </summary>
    [Range(0.5, 1.0)]
    public required double MinimumSampleConfidence { get; init; } = 0.8;

    /// <summary>
    /// Required file extensions for video files.
    /// </summary>
    public required IReadOnlySet<string> AllowedExtensions { get; init; }

    /// <summary>
    /// Forbidden patterns in filenames (indicating low quality).
    /// </summary>
    public required IReadOnlySet<string> ForbiddenPatterns { get; init; }

    /// <summary>
    /// Creates default validation rules for Italian TV series.
    /// </summary>
    public static TrainingDataValidationRules CreateDefault() => new()
    {
        MinimumSampleCount = 100,
        MinimumSamplesPerCategory = 8,
        MaxClassImbalanceRatio = 8.0,
        MinimumFilenameLength = 15,
        MaxDuplicatePercentage = 0.03,
        MinimumSampleConfidence = 0.85,
        AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".avi", ".mp4", ".m4v", ".mov", ".wmv", ".flv", ".webm"
        },
        ForbiddenPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sample", "preview", "trailer", "fake", "virus", "password",
            "rarbg.to", "yify", "eztv", "tgx", "sample-"
        }
    };

    /// <summary>
    /// Creates strict validation rules for production training.
    /// </summary>
    public static TrainingDataValidationRules CreateStrict() => new()
    {
        MinimumSampleCount = 200,
        MinimumSamplesPerCategory = 12,
        MaxClassImbalanceRatio = 5.0,
        MinimumFilenameLength = 20,
        MaxDuplicatePercentage = 0.02,
        MinimumSampleConfidence = 0.9,
        AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".avi", ".mp4", ".m4v"
        },
        ForbiddenPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sample", "preview", "trailer", "fake", "virus", "password",
            "rarbg.to", "yify", "eztv", "tgx", "sample-", "test", "demo",
            "xxxxx", "zzzzz", "temp", "tmp"
        }
    };
}

/// <summary>
/// Status of validation operations.
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// Validation passed successfully.
    /// </summary>
    Valid,

    /// <summary>
    /// Validation passed with warnings.
    /// </summary>
    Warning,

    /// <summary>
    /// Validation failed.
    /// </summary>
    Invalid
}

/// <summary>
/// Report from training data validation.
/// </summary>
public sealed record TrainingDataValidationReport
{
    /// <summary>
    /// Overall validation status.
    /// </summary>
    public required ValidationStatus Status { get; init; }

    /// <summary>
    /// Total number of samples validated.
    /// </summary>
    public required int TotalSamples { get; init; }

    /// <summary>
    /// Number of valid samples.
    /// </summary>
    public required int ValidSamples { get; init; }

    /// <summary>
    /// Number of invalid samples.
    /// </summary>
    public required int InvalidSamples { get; init; }

    /// <summary>
    /// Detailed validation issues found.
    /// </summary>
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }

    /// <summary>
    /// Per-category sample distribution.
    /// </summary>
    public required IReadOnlyDictionary<string, int> CategoryDistribution { get; init; }

    /// <summary>
    /// Class imbalance analysis.
    /// </summary>
    public required ClassImbalanceAnalysis ImbalanceAnalysis { get; init; }

    /// <summary>
    /// Quality score of the training data (0-1).
    /// </summary>
    public required double QualityScore { get; init; }

    /// <summary>
    /// Recommendations for improving training data quality.
    /// </summary>
    public required IReadOnlyList<string> Recommendations { get; init; }

    /// <summary>
    /// Summary statistics of the validation process.
    /// </summary>
    public required ValidationStatistics Statistics { get; init; }

    /// <summary>
    /// Indicates if the training data is ready for model training.
    /// </summary>
    public bool IsTrainingReady => Status == ValidationStatus.Valid && QualityScore >= 0.8;
}

/// <summary>
/// Analysis of class imbalance in training data.
/// </summary>
public sealed record ClassImbalanceAnalysis
{
    /// <summary>
    /// Largest category sample count.
    /// </summary>
    public required int MaxCategorySamples { get; init; }

    /// <summary>
    /// Smallest category sample count.
    /// </summary>
    public required int MinCategorySamples { get; init; }

    /// <summary>
    /// Current imbalance ratio (max/min).
    /// </summary>
    public required double ImbalanceRatio { get; init; }

    /// <summary>
    /// Name of the most represented category.
    /// </summary>
    public required string MajorityCategory { get; init; }

    /// <summary>
    /// Name of the least represented category.
    /// </summary>
    public required string MinorityCategory { get; init; }

    /// <summary>
    /// Indicates if class imbalance is within acceptable limits.
    /// </summary>
    public required bool IsBalanced { get; init; }

    /// <summary>
    /// Recommended sampling strategy to address imbalance.
    /// </summary>
    public required SamplingStrategy RecommendedStrategy { get; init; }
}

/// <summary>
/// Sampling strategies for addressing class imbalance.
/// </summary>
public enum SamplingStrategy
{
    /// <summary>
    /// No sampling adjustment needed.
    /// </summary>
    None,

    /// <summary>
    /// Oversample minority classes.
    /// </summary>
    Oversample,

    /// <summary>
    /// Undersample majority classes.
    /// </summary>
    Undersample,

    /// <summary>
    /// Combination of over and undersampling.
    /// </summary>
    Combined,

    /// <summary>
    /// Collect more data for underrepresented classes.
    /// </summary>
    CollectMoreData
}

/// <summary>
/// Statistical summary of training data validation.
/// </summary>
public sealed record ValidationStatistics
{
    /// <summary>
    /// Mean filename length.
    /// </summary>
    public required double MeanFilenameLength { get; init; }

    /// <summary>
    /// Standard deviation of filename lengths.
    /// </summary>
    public required double FilenameStdDev { get; init; }

    /// <summary>
    /// Most common file extension.
    /// </summary>
    public required string MostCommonExtension { get; init; }

    /// <summary>
    /// Percentage of samples with Italian language indicators.
    /// </summary>
    public required double ItalianContentPercentage { get; init; }

    /// <summary>
    /// Average confidence score across all samples.
    /// </summary>
    public required double AverageConfidence { get; init; }

    /// <summary>
    /// Number of unique categories in the dataset.
    /// </summary>
    public required int UniqueCategoryCount { get; init; }

    /// <summary>
    /// Percentage of duplicate filenames found.
    /// </summary>
    public required double DuplicatePercentage { get; init; }

    /// <summary>
    /// Distribution of quality indicators in filenames.
    /// </summary>
    public required IReadOnlyDictionary<string, int> QualityDistribution { get; init; }
}