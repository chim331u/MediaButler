using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Interface for ML-powered file classification.
/// This service operates independently of domain concerns, focusing solely on ML prediction logic.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only performs ML classification
/// - No complecting: Separate from file management and business rules
/// - Values over state: Stateless prediction operations
/// - Declarative: Describes what classification does, not how
/// </remarks>
public interface IClassificationService
{
    /// <summary>
    /// Classifies a filename into a category using ML prediction.
    /// </summary>
    /// <param name="filename">The filename to classify</param>
    /// <returns>Result containing classification result or error information</returns>
    Task<Result<ClassificationResult>> ClassifyFilenameAsync(string filename);

    /// <summary>
    /// Classifies multiple filenames in batch for efficiency.
    /// </summary>
    /// <param name="filenames">The filenames to classify</param>
    /// <returns>Result containing batch classification results or error information</returns>
    Task<Result<IEnumerable<ClassificationResult>>> ClassifyBatchAsync(IEnumerable<string> filenames);

    /// <summary>
    /// Gets available categories that the model can predict.
    /// </summary>
    /// <returns>Result containing list of available categories or error information</returns>
    Result<IEnumerable<string>> GetAvailableCategories();

    /// <summary>
    /// Gets model information and performance metrics.
    /// </summary>
    /// <returns>Result containing model information or error information</returns>
    Result<ModelInfo> GetModelInfo();

    /// <summary>
    /// Checks if the ML model is loaded and ready for predictions.
    /// </summary>
    /// <returns>True if model is ready, false otherwise</returns>
    bool IsModelReady();
}