using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Service for ML model prediction and classification operations.
/// Provides thread-safe prediction capabilities optimized for Italian TV series classification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable prediction results and input data
/// - Single responsibility: Only handles prediction and classification concerns
/// - Compose don't complect: Independent from training and data collection services
/// - Declarative: Clear prediction specifications without implementation coupling
/// </remarks>
public interface IPredictionService
{
    /// <summary>
    /// Predicts the category for a given filename using the loaded ML model.
    /// </summary>
    /// <param name="filename">The filename to classify</param>
    /// <param name="cancellationToken">Token to cancel the prediction operation</param>
    /// <returns>Result containing prediction with confidence score or error details</returns>
    Task<Result<ClassificationResult>> PredictAsync(
        string filename,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Predicts categories for multiple filenames in a batch operation.
    /// </summary>
    /// <param name="filenames">Collection of filenames to classify</param>
    /// <param name="cancellationToken">Token to cancel the batch prediction operation</param>
    /// <returns>Result containing batch predictions with individual confidence scores</returns>
    Task<Result<BatchClassificationResult>> PredictBatchAsync(
        IEnumerable<string> filenames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a filename can be processed by the prediction service.
    /// </summary>
    /// <param name="filename">The filename to validate</param>
    /// <returns>Result containing validation information and processing recommendations</returns>
    Task<Result<FilenameValidationResult>> ValidateFilenameAsync(string filename);

    /// <summary>
    /// Gets performance statistics for the prediction service.
    /// </summary>
    /// <returns>Result containing prediction performance metrics and statistics</returns>
    Task<Result<PredictionPerformanceStats>> GetPerformanceStatsAsync();
}