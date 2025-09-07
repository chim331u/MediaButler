using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Interface for managing ML training data.
/// This service handles training data collection, validation, and preparation without domain coupling.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only manages training data
/// - No complecting: Separate from model training and domain logic
/// - Values over state: Immutable training data structures
/// - Compose don't complex: Independent from other ML services
/// </remarks>
public interface ITrainingDataService
{
    /// <summary>
    /// Adds a new training sample to the dataset.
    /// </summary>
    /// <param name="filename">The filename used for training</param>
    /// <param name="expectedCategory">The correct category for this filename</param>
    /// <returns>Result indicating success or error information</returns>
    Task<Result<bool>> AddTrainingSampleAsync(string filename, string expectedCategory);

    /// <summary>
    /// Gets training data for model training, split into train/validation/test sets.
    /// </summary>
    /// <param name="trainRatio">Ratio of data to use for training (default 0.7)</param>
    /// <param name="validationRatio">Ratio of data to use for validation (default 0.2)</param>
    /// <returns>Result containing training data split or error information</returns>
    Task<Result<TrainingDataSplit>> GetTrainingDataAsync(double trainRatio = 0.7, double validationRatio = 0.2);

    /// <summary>
    /// Validates training data quality and consistency.
    /// </summary>
    /// <returns>Result containing validation results or error information</returns>
    Task<Result<TrainingDataValidation>> ValidateTrainingDataAsync();

    /// <summary>
    /// Gets statistics about the training dataset.
    /// </summary>
    /// <returns>Result containing dataset statistics or error information</returns>
    Task<Result<DatasetStatistics>> GetDatasetStatisticsAsync();

    /// <summary>
    /// Exports training data to a file for backup or external processing.
    /// </summary>
    /// <param name="filePath">Path where to export the training data</param>
    /// <returns>Result indicating success or error information</returns>
    Task<Result<bool>> ExportTrainingDataAsync(string filePath);

    /// <summary>
    /// Imports training data from a file.
    /// </summary>
    /// <param name="filePath">Path to the training data file</param>
    /// <returns>Result indicating success with import statistics or error information</returns>
    Task<Result<ImportResult>> ImportTrainingDataAsync(string filePath);
}