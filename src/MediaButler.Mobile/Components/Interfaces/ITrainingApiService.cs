using MediaButler.Mobile.Models;
using MediaButler.Shared.UI.Models;

namespace MediaButler.Mobile.Components.Interfaces;

/// <summary>
/// Training API service for ML model operations.
/// Single responsibility: Model training and evaluation only.
/// </summary>
public interface ITrainingApiService
{
    /// <summary>
    /// Triggers ML model training with accumulated training data.
    /// Returns session ID for tracking progress.
    /// </summary>
    Task<Result<TrainingSessionDto>> TrainModelAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a training session.
    /// </summary>
    Task<Result<TrainingSessionDto>> GetTrainingStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets training history (recent sessions).
    /// </summary>
    Task<Result<IReadOnlyList<TrainingSessionDto>>> GetTrainingHistoryAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);
}
