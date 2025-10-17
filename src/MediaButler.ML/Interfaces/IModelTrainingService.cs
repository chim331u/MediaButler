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
}