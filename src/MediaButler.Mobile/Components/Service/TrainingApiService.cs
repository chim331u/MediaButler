using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Models;
using MediaButler.Shared.UI.Models;

namespace MediaButler.Mobile.Components.Service;

/// <summary>
/// Training API service for ML model training operations.
/// Following "Simple Made Easy": Single responsibility - training operations only.
/// Composes with IHttpClientService without braiding.
/// </summary>
public class TrainingApiService : ITrainingApiService
{
    private readonly IHttpClientService _httpClient;

    public TrainingApiService(IHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Triggers ML model training with accumulated training data.
    /// Returns session ID for tracking progress.
    /// </summary>
    public async Task<Result<TrainingSessionDto>> TrainModelAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<TrainingSessionResponse>(
                "/api/training/trainModel",
                cancellationToken);

            if (!result.IsSuccess)
                return Result<TrainingSessionDto>.Failure(result.Error, result.StatusCode);

            var session = MapToTrainingSessionDto(result.Value!);
            return Result<TrainingSessionDto>.Success(session);
        }
        catch (Exception ex)
        {
            return Result<TrainingSessionDto>.Failure($"Failed to train model: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the status of a training session.
    /// </summary>
    public async Task<Result<TrainingSessionDto>> GetTrainingStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<TrainingSessionResponse>(
                $"/api/training/status/{sessionId}",
                cancellationToken);

            if (!result.IsSuccess)
                return Result<TrainingSessionDto>.Failure(result.Error, result.StatusCode);

            var session = MapToTrainingSessionDto(result.Value!);
            return Result<TrainingSessionDto>.Success(session);
        }
        catch (Exception ex)
        {
            return Result<TrainingSessionDto>.Failure($"Failed to get training status: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets training history (recent sessions).
    /// </summary>
    public async Task<Result<IReadOnlyList<TrainingSessionDto>>> GetTrainingHistoryAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<List<TrainingSessionResponse>>(
                $"/api/training/history?limit={limit}",
                cancellationToken);

            if (!result.IsSuccess)
                return Result<IReadOnlyList<TrainingSessionDto>>.Failure(result.Error, result.StatusCode);

            var sessions = result.Value!.Select(MapToTrainingSessionDto).ToList();
            return Result<IReadOnlyList<TrainingSessionDto>>.Success(sessions);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TrainingSessionDto>>.Failure($"Failed to get training history: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps API response DTO to domain DTO.
    /// Pure function - same input produces same output.
    /// </summary>
    private static TrainingSessionDto MapToTrainingSessionDto(TrainingSessionResponse response)
    {
        return new TrainingSessionDto
        {
            SessionId = response.SessionId,
            Status = response.Status,
            Message = response.Message,
            StartedAt = response.StartedAt,
            Accuracy = response.Accuracy,
            TrainingSampleCount = response.TrainingSampleCount,
            CategoryCount = response.CategoryCount,
            ModelVersion = response.ModelVersion
        };
    }
}

/// <summary>
/// API response DTO for training session data.
/// Matches TrainingStartResponse from MediaButler.API/Controllers/TrainingController.cs
/// </summary>
public class TrainingSessionResponse
{
    public required string SessionId { get; set; }
    public required string Status { get; set; }
    public required string Message { get; set; }
    public DateTime StartedAt { get; set; }
    public double? Accuracy { get; set; }
    public int? TrainingSampleCount { get; set; }
    public int? CategoryCount { get; set; }
    public int? ModelVersion { get; set; }
}
