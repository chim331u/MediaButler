using System.ComponentModel.DataAnnotations;

namespace MediaButler.ML.Models;

/// <summary>
/// Represents a single test case for model evaluation.
/// </summary>
public sealed record EvaluationTestCase
{
    /// <summary>
    /// Input filename for classification.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Expected/ground truth category.
    /// </summary>
    public required string ExpectedCategory { get; init; }

    /// <summary>
    /// Model's predicted category.
    /// </summary>
    public string? PredictedCategory { get; init; }

    /// <summary>
    /// Model's confidence in the prediction (0.0-1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public double? PredictionConfidence { get; init; }

    /// <summary>
    /// Time taken for this prediction in milliseconds.
    /// </summary>
    public double? PredictionTimeMs { get; init; }

    /// <summary>
    /// Whether the prediction was correct.
    /// </summary>
    public bool IsCorrect => PredictedCategory?.Equals(ExpectedCategory, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Additional metadata for the test case.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>().AsReadOnly();
}

/// <summary>
/// Comprehensive accuracy metrics for model evaluation.
/// </summary>
public sealed record AccuracyMetrics
{
    /// <summary>
    /// Overall accuracy (correct predictions / total predictions).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double OverallAccuracy { get; init; }

    /// <summary>
    /// Precision per category (true positives / (true positives + false positives)).
    /// </summary>
    public required IReadOnlyDictionary<string, double> PrecisionByCategory { get; init; }

    /// <summary>
    /// Recall per category (true positives / (true positives + false negatives)).
    /// </summary>
    public required IReadOnlyDictionary<string, double> RecallByCategory { get; init; }

    /// <summary>
    /// F1-score per category (2 * precision * recall / (precision + recall)).
    /// </summary>
    public required IReadOnlyDictionary<string, double> F1ScoreByCategory { get; init; }

    /// <summary>
    /// Macro-averaged precision across all categories.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double MacroPrecision { get; init; }

    /// <summary>
    /// Macro-averaged recall across all categories.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double MacroRecall { get; init; }

    /// <summary>
    /// Macro-averaged F1-score across all categories.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double MacroF1Score { get; init; }

    /// <summary>
    /// Weighted precision (precision weighted by support).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double WeightedPrecision { get; init; }

    /// <summary>
    /// Weighted recall (recall weighted by support).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double WeightedRecall { get; init; }

    /// <summary>
    /// Weighted F1-score (F1 weighted by support).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double WeightedF1Score { get; init; }

    /// <summary>
    /// Total number of test cases evaluated.
    /// </summary>
    public required int TotalTestCases { get; init; }

    /// <summary>
    /// Number of correct predictions.
    /// </summary>
    public required int CorrectPredictions { get; init; }

    /// <summary>
    /// Number of categories evaluated.
    /// </summary>
    public required int CategoryCount { get; init; }

    /// <summary>
    /// Average confidence score across all predictions.
    /// </summary>
    [Range(0.0, 1.0)]
    public double? AverageConfidence { get; init; }

    /// <summary>
    /// When the evaluation was performed.
    /// </summary>
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;
}


/// <summary>
/// Performance benchmark results with timing and throughput metrics.
/// </summary>
public sealed record PerformanceBenchmark
{
    /// <summary>
    /// Average prediction time per file in milliseconds.
    /// </summary>
    public required double AveragePredictionTimeMs { get; init; }

    /// <summary>
    /// Median prediction time per file in milliseconds.
    /// </summary>
    public required double MedianPredictionTimeMs { get; init; }

    /// <summary>
    /// 95th percentile prediction time in milliseconds.
    /// </summary>
    public required double P95PredictionTimeMs { get; init; }

    /// <summary>
    /// 99th percentile prediction time in milliseconds.
    /// </summary>
    public required double P99PredictionTimeMs { get; init; }

    /// <summary>
    /// Throughput in predictions per second.
    /// </summary>
    public required double ThroughputPredictionsPerSecond { get; init; }

    /// <summary>
    /// Peak memory usage during benchmarking in MB.
    /// </summary>
    public required double PeakMemoryUsageMB { get; init; }

    /// <summary>
    /// Average memory usage during benchmarking in MB.
    /// </summary>
    public required double AverageMemoryUsageMB { get; init; }

    /// <summary>
    /// Total benchmark duration in milliseconds.
    /// </summary>
    public required double TotalBenchmarkTimeMs { get; init; }

    /// <summary>
    /// Number of predictions in benchmark.
    /// </summary>
    public required int BenchmarkPredictionCount { get; init; }

    /// <summary>
    /// CPU usage statistics during benchmark.
    /// </summary>
    public CpuUsageStats? CpuStats { get; init; }

    /// <summary>
    /// Whether benchmark passed performance requirements.
    /// </summary>
    public required bool PassedPerformanceRequirements { get; init; }

    /// <summary>
    /// Performance requirement violations if any.
    /// </summary>
    public IReadOnlyList<string> PerformanceViolations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// When the benchmark was performed.
    /// </summary>
    public DateTime BenchmarkedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// CPU usage statistics during performance testing.
/// </summary>
public sealed record CpuUsageStats
{
    /// <summary>
    /// Average CPU usage percentage.
    /// </summary>
    [Range(0.0, 100.0)]
    public required double AverageCpuUsagePercent { get; init; }

    /// <summary>
    /// Peak CPU usage percentage.
    /// </summary>
    [Range(0.0, 100.0)]
    public required double PeakCpuUsagePercent { get; init; }

    /// <summary>
    /// CPU usage samples collected during benchmark.
    /// </summary>
    public IReadOnlyList<double> CpuUsageSamples { get; init; } = Array.Empty<double>();
}

/// <summary>
/// Cross-validation results with variance and stability metrics.
/// </summary>
public sealed record CrossValidationResults
{
    /// <summary>
    /// Number of folds used in cross-validation.
    /// </summary>
    public required int FoldCount { get; init; }

    /// <summary>
    /// Accuracy scores for each fold.
    /// </summary>
    public required IReadOnlyList<double> FoldAccuracyScores { get; init; }

    /// <summary>
    /// Mean accuracy across all folds.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double MeanAccuracy { get; init; }

    /// <summary>
    /// Standard deviation of accuracy across folds.
    /// </summary>
    public required double AccuracyStandardDeviation { get; init; }

    /// <summary>
    /// Confidence interval for accuracy (95%).
    /// </summary>
    public required (double Lower, double Upper) AccuracyConfidenceInterval { get; init; }

    /// <summary>
    /// Coefficient of variation for accuracy scores.
    /// </summary>
    public required double AccuracyCoefficientsOfVariation { get; init; }

    /// <summary>
    /// Detailed metrics for each fold.
    /// </summary>
    public required IReadOnlyList<FoldMetrics> FoldDetails { get; init; }

    /// <summary>
    /// Overall cross-validation quality assessment.
    /// </summary>
    public required CrossValidationQuality Quality { get; init; }

    /// <summary>
    /// When cross-validation was performed.
    /// </summary>
    public DateTime PerformedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Metrics for a single cross-validation fold.
/// </summary>
public sealed record FoldMetrics
{
    /// <summary>
    /// Fold number (1-based).
    /// </summary>
    public required int FoldNumber { get; init; }

    /// <summary>
    /// Accuracy for this fold.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double Accuracy { get; init; }

    /// <summary>
    /// Precision for this fold.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double Precision { get; init; }

    /// <summary>
    /// Recall for this fold.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double Recall { get; init; }

    /// <summary>
    /// F1-score for this fold.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double F1Score { get; init; }

    /// <summary>
    /// Number of test samples in this fold.
    /// </summary>
    public required int TestSampleCount { get; init; }

    /// <summary>
    /// Training time for this fold in milliseconds.
    /// </summary>
    public double? TrainingTimeMs { get; init; }
}

/// <summary>
/// Comprehensive model quality report.
/// </summary>
public sealed record ModelQualityReport
{
    /// <summary>
    /// Model version or identifier.
    /// </summary>
    public required string ModelVersion { get; init; }

    /// <summary>
    /// Overall quality score (0.0-1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double OverallQualityScore { get; init; }

    /// <summary>
    /// Accuracy metrics from evaluation.
    /// </summary>
    public required AccuracyMetrics AccuracyMetrics { get; init; }

    /// <summary>
    /// Performance benchmark results.
    /// </summary>
    public required PerformanceBenchmark PerformanceBenchmark { get; init; }

    /// <summary>
    /// Cross-validation results.
    /// </summary>
    public CrossValidationResults? CrossValidationResults { get; init; }

    /// <summary>
    /// Confidence analysis results.
    /// </summary>
    public EvaluationConfidenceAnalysis? ConfidenceAnalysis { get; init; }

    /// <summary>
    /// Quality assessment summary.
    /// </summary>
    public required QualityAssessment QualityAssessment { get; init; }

    /// <summary>
    /// Recommendations for improvement.
    /// </summary>
    public IReadOnlyList<string> ImprovementRecommendations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// When the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Quality assessment categories and ratings.
/// </summary>
public sealed record QualityAssessment
{
    /// <summary>
    /// Accuracy quality rating.
    /// </summary>
    public required QualityRating AccuracyRating { get; init; }

    /// <summary>
    /// Performance quality rating.
    /// </summary>
    public required QualityRating PerformanceRating { get; init; }

    /// <summary>
    /// Stability quality rating (from cross-validation).
    /// </summary>
    public QualityRating? StabilityRating { get; init; }

    /// <summary>
    /// Confidence calibration rating.
    /// </summary>
    public QualityRating? CalibrationRating { get; init; }

    /// <summary>
    /// Overall model readiness for production.
    /// </summary>
    public required ModelReadiness ProductionReadiness { get; init; }

    /// <summary>
    /// Critical issues that must be addressed.
    /// </summary>
    public IReadOnlyList<string> CriticalIssues { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Warning issues that should be addressed.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Model validation results against quality thresholds.
/// </summary>
public sealed record ModelValidationResult
{
    /// <summary>
    /// Whether the model passed all quality thresholds.
    /// </summary>
    public required bool PassedValidation { get; init; }

    /// <summary>
    /// Quality thresholds that were validated against.
    /// </summary>
    public required QualityThresholds AppliedThresholds { get; init; }

    /// <summary>
    /// Validation results per threshold category.
    /// </summary>
    public required IReadOnlyDictionary<string, ValidationResult> ValidationResults { get; init; }

    /// <summary>
    /// Failed validations with details.
    /// </summary>
    public IReadOnlyList<ValidationFailure> FailedValidations { get; init; } = Array.Empty<ValidationFailure>();

    /// <summary>
    /// Overall validation score (0.0-1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double ValidationScore { get; init; }

    /// <summary>
    /// When validation was performed.
    /// </summary>
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Individual validation result.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Whether this validation passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Actual measured value.
    /// </summary>
    public required double ActualValue { get; init; }

    /// <summary>
    /// Required threshold value.
    /// </summary>
    public required double ThresholdValue { get; init; }

    /// <summary>
    /// Validation message or details.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Validation failure details.
/// </summary>
public sealed record ValidationFailure
{
    /// <summary>
    /// Name of the failed validation.
    /// </summary>
    public required string ValidationName { get; init; }

    /// <summary>
    /// Expected threshold value.
    /// </summary>
    public required double ExpectedValue { get; init; }

    /// <summary>
    /// Actual measured value.
    /// </summary>
    public required double ActualValue { get; init; }

    /// <summary>
    /// Severity of the failure.
    /// </summary>
    public required ValidationSeverity Severity { get; init; }

    /// <summary>
    /// Detailed failure message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Recommended actions to address the failure.
    /// </summary>
    public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Quality thresholds for model validation.
/// </summary>
public sealed record QualityThresholds
{
    /// <summary>
    /// Minimum required overall accuracy.
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinAccuracy { get; init; } = 0.8;

    /// <summary>
    /// Minimum required macro F1-score.
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinF1Score { get; init; } = 0.75;

    /// <summary>
    /// Maximum acceptable average prediction time in milliseconds.
    /// </summary>
    public double MaxPredictionTimeMs { get; init; } = 100.0;

    /// <summary>
    /// Maximum acceptable memory usage in MB.
    /// </summary>
    public double MaxMemoryUsageMB { get; init; } = 300.0;

    /// <summary>
    /// Minimum required throughput in predictions per second.
    /// </summary>
    public double MinThroughputPredictionsPerSecond { get; init; } = 10.0;

    /// <summary>
    /// Maximum acceptable cross-validation standard deviation.
    /// </summary>
    public double MaxCrossValidationStdDev { get; init; } = 0.05;

    /// <summary>
    /// Minimum required confidence calibration score.
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinConfidenceCalibration { get; init; } = 0.7;

    /// <summary>
    /// Per-category minimum accuracy thresholds.
    /// </summary>
    public IReadOnlyDictionary<string, double> CategoryMinAccuracy { get; init; } = 
        new Dictionary<string, double>().AsReadOnly();
}

/// <summary>
/// Configuration for model evaluation.
/// </summary>
public sealed record ModelEvaluationConfiguration
{
    /// <summary>
    /// Test dataset for evaluation.
    /// </summary>
    public required IReadOnlyList<EvaluationTestCase> TestDataset { get; init; }

    /// <summary>
    /// Whether to perform cross-validation.
    /// </summary>
    public bool PerformCrossValidation { get; init; } = true;

    /// <summary>
    /// Number of cross-validation folds.
    /// </summary>
    public int CrossValidationFolds { get; init; } = 5;

    /// <summary>
    /// Whether to perform performance benchmarking.
    /// </summary>
    public bool PerformBenchmarking { get; init; } = true;

    /// <summary>
    /// Benchmark configuration.
    /// </summary>
    public BenchmarkConfiguration? BenchmarkConfig { get; init; }

    /// <summary>
    /// Quality thresholds for validation.
    /// </summary>
    public QualityThresholds? QualityThresholds { get; init; }

    /// <summary>
    /// Whether to generate detailed confidence analysis.
    /// </summary>
    public bool AnalyzeConfidence { get; init; } = true;
}

/// <summary>
/// Configuration for performance benchmarking.
/// </summary>
public sealed record BenchmarkConfiguration
{
    /// <summary>
    /// Number of predictions to benchmark.
    /// </summary>
    public int PredictionCount { get; init; } = 1000;

    /// <summary>
    /// Number of warmup predictions before benchmarking.
    /// </summary>
    public int WarmupCount { get; init; } = 100;

    /// <summary>
    /// Whether to monitor memory usage during benchmarking.
    /// </summary>
    public bool MonitorMemoryUsage { get; init; } = true;

    /// <summary>
    /// Whether to monitor CPU usage during benchmarking.
    /// </summary>
    public bool MonitorCpuUsage { get; init; } = true;

    /// <summary>
    /// Benchmark timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000; // 30 seconds

    /// <summary>
    /// Test data for benchmarking.
    /// </summary>
    public IReadOnlyList<string>? BenchmarkFilenames { get; init; }
}

/// <summary>
/// Model comparison results.
/// </summary>
public sealed record ModelComparison
{
    /// <summary>
    /// Baseline model identifier.
    /// </summary>
    public required string BaselineModel { get; init; }

    /// <summary>
    /// Candidate model identifier.
    /// </summary>
    public required string CandidateModel { get; init; }

    /// <summary>
    /// Accuracy comparison results.
    /// </summary>
    public required MetricComparison AccuracyComparison { get; init; }

    /// <summary>
    /// Performance comparison results.
    /// </summary>
    public required MetricComparison PerformanceComparison { get; init; }

    /// <summary>
    /// Overall recommendation.
    /// </summary>
    public required ComparisonRecommendation Recommendation { get; init; }

    /// <summary>
    /// Detailed comparison analysis.
    /// </summary>
    public required string Analysis { get; init; }

    /// <summary>
    /// Statistical significance of differences.
    /// </summary>
    public IReadOnlyDictionary<string, double> StatisticalSignificance { get; init; } = 
        new Dictionary<string, double>().AsReadOnly();

    /// <summary>
    /// When comparison was performed.
    /// </summary>
    public DateTime ComparedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Metric comparison between two models.
/// </summary>
public sealed record MetricComparison
{
    /// <summary>
    /// Baseline model metric value.
    /// </summary>
    public required double BaselineValue { get; init; }

    /// <summary>
    /// Candidate model metric value.
    /// </summary>
    public required double CandidateValue { get; init; }

    /// <summary>
    /// Absolute difference (candidate - baseline).
    /// </summary>
    public double AbsoluteDifference => CandidateValue - BaselineValue;

    /// <summary>
    /// Relative difference as percentage.
    /// </summary>
    public double RelativeDifferencePercent => BaselineValue != 0 ? 
        (AbsoluteDifference / BaselineValue) * 100 : 0;

    /// <summary>
    /// Whether the candidate is significantly better.
    /// </summary>
    public required bool IsSignificantImprovement { get; init; }

    /// <summary>
    /// Confidence level of the comparison.
    /// </summary>
    [Range(0.0, 1.0)]
    public double? ConfidenceLevel { get; init; }
}

/// <summary>
/// Detailed confusion matrix for model evaluation with comprehensive metrics.
/// </summary>
public sealed record EvaluationConfusionMatrix
{
    /// <summary>
    /// List of category labels in matrix order.
    /// </summary>
    public required IReadOnlyList<string> Categories { get; init; }

    /// <summary>
    /// Confusion matrix values [actual][predicted].
    /// </summary>
    public required int[,] Matrix { get; init; }

    /// <summary>
    /// Total number of predictions.
    /// </summary>
    public required int TotalPredictions { get; init; }

    /// <summary>
    /// True positives per category.
    /// </summary>
    public required IReadOnlyDictionary<string, int> TruePositives { get; init; }

    /// <summary>
    /// False positives per category.
    /// </summary>
    public required IReadOnlyDictionary<string, int> FalsePositives { get; init; }

    /// <summary>
    /// False negatives per category.
    /// </summary>
    public required IReadOnlyDictionary<string, int> FalseNegatives { get; init; }

    /// <summary>
    /// True negatives per category.
    /// </summary>
    public required IReadOnlyDictionary<string, int> TrueNegatives { get; init; }

    /// <summary>
    /// Gets the confusion matrix as a formatted string.
    /// </summary>
    /// <returns>Human-readable confusion matrix</returns>
    public string ToFormattedString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Confusion Matrix:");
        sb.AppendLine("Predicted →");
        sb.Append("Actual ↓".PadRight(15));

        // Header with category names
        foreach (var category in Categories)
        {
            sb.Append(category.Substring(0, Math.Min(category.Length, 10)).PadLeft(12));
        }
        sb.AppendLine();

        // Matrix rows
        for (int i = 0; i < Categories.Count; i++)
        {
            sb.Append(Categories[i].Substring(0, Math.Min(Categories[i].Length, 12)).PadRight(15));
            for (int j = 0; j < Categories.Count; j++)
            {
                sb.Append(Matrix[i, j].ToString().PadLeft(12));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>
/// Comprehensive confidence analysis for model evaluation.
/// </summary>
public sealed record EvaluationConfidenceAnalysis
{
    /// <summary>
    /// Average confidence score across all predictions.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double AverageConfidence { get; init; }

    /// <summary>
    /// Confidence score distribution by bins.
    /// </summary>
    public required IReadOnlyDictionary<string, int> ConfidenceDistribution { get; init; }

    /// <summary>
    /// Calibration curve data points.
    /// </summary>
    public required IReadOnlyList<CalibrationPoint> CalibrationCurve { get; init; }

    /// <summary>
    /// Calibration error (Expected Calibration Error).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double CalibrationError { get; init; }

    /// <summary>
    /// Brier score for confidence calibration.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double BrierScore { get; init; }

    /// <summary>
    /// Reliability index for confidence scores.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double ReliabilityIndex { get; init; }

    /// <summary>
    /// Over-confidence or under-confidence bias.
    /// </summary>
    public required ConfidenceBias ConfidenceBias { get; init; }

    /// <summary>
    /// Confidence analysis quality assessment.
    /// </summary>
    public required ConfidenceQuality Quality { get; init; }
}

/// <summary>
/// Single point on the calibration curve.
/// </summary>
public sealed record CalibrationPoint
{
    /// <summary>
    /// Predicted confidence bucket (e.g., 0.8-0.9).
    /// </summary>
    public required string ConfidenceBucket { get; init; }

    /// <summary>
    /// Average predicted confidence in this bucket.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double AverageConfidence { get; init; }

    /// <summary>
    /// Actual accuracy for predictions in this bucket.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double ActualAccuracy { get; init; }

    /// <summary>
    /// Number of predictions in this bucket.
    /// </summary>
    public required int SampleCount { get; init; }
}

/// <summary>
/// Enumeration of quality ratings.
/// </summary>
public enum QualityRating
{
    /// <summary>
    /// Poor quality, needs significant improvement.
    /// </summary>
    Poor = 0,

    /// <summary>
    /// Below average quality, needs improvement.
    /// </summary>
    BelowAverage = 1,

    /// <summary>
    /// Average quality, acceptable for basic use.
    /// </summary>
    Average = 2,

    /// <summary>
    /// Good quality, suitable for production.
    /// </summary>
    Good = 3,

    /// <summary>
    /// Excellent quality, exceeds requirements.
    /// </summary>
    Excellent = 4
}

/// <summary>
/// Model readiness for production deployment.
/// </summary>
public enum ModelReadiness
{
    /// <summary>
    /// Not ready, has critical issues.
    /// </summary>
    NotReady = 0,

    /// <summary>
    /// Ready for development/testing only.
    /// </summary>
    DevelopmentOnly = 1,

    /// <summary>
    /// Ready for staging environment.
    /// </summary>
    StagingReady = 2,

    /// <summary>
    /// Ready for production deployment.
    /// </summary>
    ProductionReady = 3,

    /// <summary>
    /// Exceeds production requirements.
    /// </summary>
    ExceedsRequirements = 4
}

/// <summary>
/// Cross-validation quality assessment.
/// </summary>
public enum CrossValidationQuality
{
    /// <summary>
    /// Poor cross-validation results, model is unstable.
    /// </summary>
    Poor = 0,

    /// <summary>
    /// Below average stability, needs improvement.
    /// </summary>
    BelowAverage = 1,

    /// <summary>
    /// Average stability, acceptable variance.
    /// </summary>
    Average = 2,

    /// <summary>
    /// Good stability, consistent performance.
    /// </summary>
    Good = 3,

    /// <summary>
    /// Excellent stability, very consistent.
    /// </summary>
    Excellent = 4
}

/// <summary>
/// Validation severity levels.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Information only, no action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning, should be addressed but not critical.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error, must be addressed before production.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical error, blocks deployment.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Model comparison recommendation.
/// </summary>
public enum ComparisonRecommendation
{
    /// <summary>
    /// Keep baseline model, candidate is worse.
    /// </summary>
    KeepBaseline = 0,

    /// <summary>
    /// No clear winner, consider other factors.
    /// </summary>
    Inconclusive = 1,

    /// <summary>
    /// Candidate shows promise but needs more evaluation.
    /// </summary>
    CandidateNeedsMoreEvaluation = 2,

    /// <summary>
    /// Switch to candidate model, it's better.
    /// </summary>
    SwitchToCandidate = 3,

    /// <summary>
    /// Candidate significantly better, strongly recommend switch.
    /// </summary>
    StronglyRecommendCandidate = 4
}

/// <summary>
/// Confidence bias assessment.
/// </summary>
public enum ConfidenceBias
{
    /// <summary>
    /// Significantly under-confident.
    /// </summary>
    SignificantlyUnderConfident = -2,

    /// <summary>
    /// Somewhat under-confident.
    /// </summary>
    UnderConfident = -1,

    /// <summary>
    /// Well-calibrated confidence.
    /// </summary>
    WellCalibrated = 0,

    /// <summary>
    /// Somewhat over-confident.
    /// </summary>
    OverConfident = 1,

    /// <summary>
    /// Significantly over-confident.
    /// </summary>
    SignificantlyOverConfident = 2
}

/// <summary>
/// Confidence analysis quality.
/// </summary>
public enum ConfidenceQuality
{
    /// <summary>
    /// Poor confidence calibration.
    /// </summary>
    Poor = 0,

    /// <summary>
    /// Below average calibration.
    /// </summary>
    BelowAverage = 1,

    /// <summary>
    /// Average calibration, acceptable.
    /// </summary>
    Average = 2,

    /// <summary>
    /// Good calibration, reliable confidence.
    /// </summary>
    Good = 3,

    /// <summary>
    /// Excellent calibration, highly reliable.
    /// </summary>
    Excellent = 4
}