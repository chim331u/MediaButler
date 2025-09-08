using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Service for ML model architecture management and configuration.
/// Handles model lifecycle, pipeline configuration, and evaluation metrics.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable model configurations and evaluation results
/// - Single responsibility: Only handles ML model architecture concerns
/// - Declarative: Clear model specifications without implementation coupling
/// </remarks>
public interface IMLModelService
{
    /// <summary>
    /// Creates a new multi-class classification model architecture for Italian TV series categorization.
    /// Configures the complete pipeline from feature vectors to category predictions.
    /// </summary>
    /// <param name="modelConfig">Model architecture configuration</param>
    /// <returns>Model architecture specification ready for training</returns>
    Task<Result<MLModelArchitecture>> CreateClassificationModelAsync(ModelConfiguration modelConfig);
    
    /// <summary>
    /// Configures the feature pipeline for transforming raw features into ML.NET compatible format.
    /// Handles normalization, categorical encoding, and feature selection.
    /// </summary>
    /// <param name="featureConfig">Feature pipeline configuration</param>
    /// <returns>Feature pipeline specification</returns>
    Task<Result<FeaturePipelineConfig>> ConfigureFeaturePipelineAsync(FeaturePipelineConfiguration featureConfig);
    
    /// <summary>
    /// Defines cross-validation strategy for model evaluation and hyperparameter tuning.
    /// Uses stratified k-fold to maintain category balance across folds.
    /// </summary>
    /// <param name="validationConfig">Validation strategy configuration</param>
    /// <returns>Cross-validation configuration</returns>
    Task<Result<CrossValidationConfig>> DefineCrossValidationStrategyAsync(ValidationConfiguration validationConfig);
    
    /// <summary>
    /// Defines evaluation metrics for model performance assessment.
    /// Includes accuracy, precision, recall, F1-score, and confusion matrix analysis.
    /// </summary>
    /// <param name="metricsConfig">Metrics configuration for evaluation</param>
    /// <returns>Evaluation metrics specification</returns>
    Task<Result<ModelEvaluationMetrics>> DefineEvaluationMetricsAsync(EvaluationMetricsConfiguration metricsConfig);
    
    /// <summary>
    /// Validates model architecture configuration for compatibility and performance requirements.
    /// Ensures architecture meets Italian content optimization and ARM32 deployment constraints.
    /// </summary>
    /// <param name="architecture">Model architecture to validate</param>
    /// <returns>Validation result with recommendations</returns>
    Task<Result<ArchitectureValidationResult>> ValidateModelArchitectureAsync(MLModelArchitecture architecture);
    
    /// <summary>
    /// Estimates model resource requirements for ARM32 deployment planning.
    /// Calculates memory usage, prediction latency, and model size estimates.
    /// </summary>
    /// <param name="architecture">Model architecture for estimation</param>
    /// <returns>Resource requirement estimates</returns>
    Task<Result<ModelResourceEstimate>> EstimateResourceRequirementsAsync(MLModelArchitecture architecture);
    
    /// <summary>
    /// Gets recommended model architecture for Italian TV series classification.
    /// Pre-configured with optimal settings based on training data analysis.
    /// </summary>
    /// <returns>Recommended model architecture configuration</returns>
    Task<Result<MLModelArchitecture>> GetRecommendedArchitectureAsync();
}