using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Orchestrates ML-related business logic across multiple services.
/// Decouples controllers from complex batch processing and mapping logic.
/// </summary>
public interface IMlOrchestrationService
{
    /// <summary>
    /// Processes a batch of files for ML evaluation and updates their status.
    /// </summary>
    /// <param name="request">Evaluation parameters</param>
    /// <returns>Summary of the evaluation process</returns>
    Task<MlEvaluationResponse> ProcessMlEvaluationBatchAsync(MlEvaluationRequest request);

    /// <summary>
    /// Classifies a filename and returns structured predictions.
    /// </summary>
    /// <param name="filename">Filename to classify</param>
    /// <returns>Structured classification response with top predictions</returns>
    Task<ClassificationResponse> ClassifyFilenameAsync(string filename);
}
