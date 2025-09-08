using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Service for evaluating ML model performance with comprehensive metrics and analysis.
/// Handles accuracy calculation, confusion matrix generation, and performance benchmarking.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles model evaluation and metrics
/// - Values over state: Immutable evaluation results and metrics
/// - Compose don't complex: Independent evaluation components
/// </remarks>
public interface IModelEvaluationService
{
    /// <summary>
    /// Evaluates model accuracy using a test dataset.
    /// </summary>
    /// <param name="testData">Test dataset for evaluation</param>
    /// <returns>Comprehensive accuracy metrics and analysis</returns>
    Task<Result<AccuracyMetrics>> EvaluateAccuracyAsync(IReadOnlyList<EvaluationTestCase> testData);

    /// <summary>
    /// Generates confusion matrix for multi-class classification analysis.
    /// </summary>
    /// <param name="testData">Test dataset with actual and predicted categories</param>
    /// <returns>Confusion matrix with detailed classification breakdown</returns>
    Task<Result<EvaluationConfusionMatrix>> GenerateConfusionMatrixAsync(IReadOnlyList<EvaluationTestCase> testData);

    /// <summary>
    /// Performs comprehensive performance benchmarking of the model.
    /// </summary>
    /// <param name="benchmarkConfig">Configuration for benchmark testing</param>
    /// <returns>Performance benchmark results with timing and throughput metrics</returns>
    Task<Result<PerformanceBenchmark>> BenchmarkPerformanceAsync(BenchmarkConfiguration benchmarkConfig);

    /// <summary>
    /// Performs cross-validation analysis to assess model generalization.
    /// </summary>
    /// <param name="dataset">Complete dataset for cross-validation</param>
    /// <param name="folds">Number of folds for cross-validation (default: 5)</param>
    /// <returns>Cross-validation results with variance and stability metrics</returns>
    Task<Result<CrossValidationResults>> PerformCrossValidationAsync(
        IReadOnlyList<TrainingSample> dataset, 
        int folds = 5);

    /// <summary>
    /// Generates comprehensive model quality report.
    /// </summary>
    /// <param name="evaluationConfig">Configuration for quality evaluation</param>
    /// <returns>Complete quality assessment report</returns>
    Task<Result<ModelQualityReport>> GenerateQualityReportAsync(ModelEvaluationConfiguration evaluationConfig);

    /// <summary>
    /// Validates model against quality thresholds and assertions.
    /// </summary>
    /// <param name="qualityThresholds">Minimum quality thresholds to validate against</param>
    /// <returns>Validation results with pass/fail status and detailed feedback</returns>
    Task<Result<ModelValidationResult>> ValidateModelQualityAsync(QualityThresholds qualityThresholds);

    /// <summary>
    /// Compares performance between different model versions.
    /// </summary>
    /// <param name="baselineModelPath">Path to baseline model for comparison</param>
    /// <param name="candidateModelPath">Path to candidate model for comparison</param>
    /// <returns>Detailed comparison results with recommendations</returns>
    Task<Result<ModelComparison>> CompareModelsAsync(string baselineModelPath, string candidateModelPath);

    /// <summary>
    /// Analyzes prediction confidence distribution and calibration.
    /// </summary>
    /// <param name="testData">Test dataset with confidence scores</param>
    /// <returns>Confidence analysis with calibration metrics</returns>
    Task<Result<EvaluationConfidenceAnalysis>> AnalyzeConfidenceDistributionAsync(IReadOnlyList<EvaluationTestCase> testData);
}