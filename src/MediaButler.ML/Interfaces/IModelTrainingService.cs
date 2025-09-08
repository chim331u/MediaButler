using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Service for ML model training pipeline management.
/// Provides comprehensive model training capabilities optimized for Italian TV series classification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable training configurations and evaluation results
/// - Single responsibility: Only handles ML model training concerns
/// - Compose don't complect: Independent from data collection and architecture services
/// - Declarative: Clear training specifications without implementation coupling
/// </remarks>
public interface IModelTrainingService
{
    /// <summary>
    /// Trains a new classification model using the provided training data and configuration.
    /// </summary>
    /// <param name="trainingData">Collection of training samples for model training</param>
    /// <param name="trainingConfig">Configuration settings for the training process</param>
    /// <param name="cancellationToken">Token to cancel the training operation</param>
    /// <returns>Result containing the trained model information or error details</returns>
    Task<Result<TrainedModelInfo>> TrainModelAsync(
        IEnumerable<TrainingSample> trainingData,
        TrainingConfiguration trainingConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a trained model's performance using cross-validation and test data.
    /// </summary>
    /// <param name="modelInfo">Information about the trained model to evaluate</param>
    /// <param name="evaluationData">Data to use for model evaluation</param>
    /// <param name="evaluationConfig">Configuration for evaluation process</param>
    /// <param name="cancellationToken">Token to cancel the evaluation operation</param>
    /// <returns>Result containing comprehensive evaluation metrics</returns>
    Task<Result<ModelEvaluationResult>> EvaluateModelAsync(
        TrainedModelInfo modelInfo,
        IEnumerable<TrainingSample> evaluationData,
        EvaluationConfiguration evaluationConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs cross-validation on the training data to assess model stability and generalization.
    /// </summary>
    /// <param name="trainingData">Complete training dataset for cross-validation</param>
    /// <param name="architecture">Model architecture configuration to validate</param>
    /// <param name="cancellationToken">Token to cancel the cross-validation operation</param>
    /// <returns>Result containing cross-validation metrics and fold results</returns>
    Task<Result<CrossValidationResult>> PerformCrossValidationAsync(
        IEnumerable<TrainingSample> trainingData,
        MLModelArchitecture architecture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and configures the ML.NET pipeline for Italian TV series classification.
    /// </summary>
    /// <param name="architecture">Model architecture specification</param>
    /// <param name="featurePipeline">Feature processing pipeline configuration</param>
    /// <returns>Result containing the configured ML.NET training pipeline</returns>
    Task<Result<MLTrainingPipeline>> CreateTrainingPipelineAsync(
        MLModelArchitecture architecture,
        FeaturePipelineConfig featurePipeline);

    /// <summary>
    /// Saves a trained model to persistent storage with versioning support.
    /// </summary>
    /// <param name="modelInfo">Information about the trained model</param>
    /// <param name="modelPath">File path where the model should be saved</param>
    /// <param name="metadata">Additional metadata to store with the model</param>
    /// <returns>Result indicating success or failure of the save operation</returns>
    Task<Result<ModelPersistenceInfo>> SaveModelAsync(
        TrainedModelInfo modelInfo,
        string modelPath,
        ModelMetadata metadata);

    /// <summary>
    /// Loads a previously trained model from persistent storage.
    /// </summary>
    /// <param name="modelPath">File path of the saved model</param>
    /// <param name="validationConfig">Optional validation configuration to verify model integrity</param>
    /// <returns>Result containing the loaded model information</returns>
    Task<Result<TrainedModelInfo>> LoadModelAsync(
        string modelPath,
        ModelValidationConfig? validationConfig = null);

    /// <summary>
    /// Performs hyperparameter optimization to find the best model configuration.
    /// </summary>
    /// <param name="trainingData">Training data for hyperparameter optimization</param>
    /// <param name="optimizationConfig">Configuration for the optimization process</param>
    /// <param name="cancellationToken">Token to cancel the optimization operation</param>
    /// <returns>Result containing the optimized hyperparameters and performance metrics</returns>
    Task<Result<HyperparameterOptimizationResult>> OptimizeHyperparametersAsync(
        IEnumerable<TrainingSample> trainingData,
        HyperparameterOptimizationConfig optimizationConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates training data quality and consistency before model training.
    /// </summary>
    /// <param name="trainingData">Training data to validate</param>
    /// <param name="validationRules">Rules to apply during validation</param>
    /// <returns>Result containing validation report with issues and recommendations</returns>
    Task<Result<TrainingDataValidationReport>> ValidateTrainingDataAsync(
        IEnumerable<TrainingSample> trainingData,
        TrainingDataValidationRules validationRules);

    /// <summary>
    /// Gets training progress and metrics for monitoring long-running training operations.
    /// </summary>
    /// <param name="trainingSessionId">Unique identifier for the training session</param>
    /// <returns>Current training progress information</returns>
    Task<Result<TrainingProgress>> GetTrainingProgressAsync(string trainingSessionId);

    /// <summary>
    /// Estimates training time and resource requirements for a given configuration.
    /// </summary>
    /// <param name="trainingData">Training data to analyze</param>
    /// <param name="architecture">Model architecture to estimate for</param>
    /// <returns>Result containing estimated training time and resource requirements</returns>
    Task<Result<TrainingResourceEstimate>> EstimateTrainingRequirementsAsync(
        IEnumerable<TrainingSample> trainingData,
        MLModelArchitecture architecture);
}