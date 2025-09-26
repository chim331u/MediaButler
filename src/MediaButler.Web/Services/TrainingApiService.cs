using MediaButler.Web.Interfaces;
using MediaButler.Web.Models;

namespace MediaButler.Web.Services;

/// <summary>
/// API service for ML model training operations.
/// Follows "Simple Made Easy" principles with clear, focused responsibilities.
/// </summary>
public interface ITrainingApiService
{
    /// <summary>
    /// Starts ML model training with specified parameters.
    /// </summary>
    Task<Result<TrainingResponse>> StartTrainingAsync(TrainingRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a training session.
    /// </summary>
    Task<Result<TrainingStatusResponse>> GetTrainingStatusAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all training sessions.
    /// </summary>
    Task<Result<IEnumerable<TrainingStatusResponse>>> GetTrainingSessionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of training API service.
/// Composes with IHttpClientService without braiding concerns.
/// </summary>
public class TrainingApiService : ITrainingApiService
{
    private readonly IHttpClientService _httpClient;
    private readonly ILogger<TrainingApiService> _logger;

    public TrainingApiService(IHttpClientService httpClient, ILogger<TrainingApiService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<TrainingResponse>> StartTrainingAsync(TrainingRequest? request = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting ML model training via API");

            var trainingRequest = request ?? new TrainingRequest();
            var result = await _httpClient.PostAsync<TrainingResponse>(
                "/api/training/start", trainingRequest, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Training started successfully. Session: {SessionId}", result.Value!.SessionId);
            }
            else
            {
                _logger.LogWarning("Training start failed: {Error}", result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            var error = $"Failed to start training: {ex.Message}";
            _logger.LogError(ex, error);
            return Result<TrainingResponse>.Failure(error);
        }
    }

    public async Task<Result<TrainingStatusResponse>> GetTrainingStatusAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return Result<TrainingStatusResponse>.Failure("Session ID is required");

            _logger.LogDebug("Getting training status for session: {SessionId}", sessionId);

            var result = await _httpClient.GetAsync<TrainingStatusResponse>(
                $"/api/training/status/{sessionId}", cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            var error = $"Failed to get training status: {ex.Message}";
            _logger.LogError(ex, error);
            return Result<TrainingStatusResponse>.Failure(error);
        }
    }

    public async Task<Result<IEnumerable<TrainingStatusResponse>>> GetTrainingSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting all training sessions");

            var result = await _httpClient.GetAsync<IEnumerable<TrainingStatusResponse>>(
                "/api/training/sessions", cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            var error = $"Failed to get training sessions: {ex.Message}";
            _logger.LogError(ex, error);
            return Result<IEnumerable<TrainingStatusResponse>>.Failure(error);
        }
    }
}

/// <summary>
/// Training request model for API calls
/// </summary>
public record TrainingRequest
{
    public int? Epochs { get; init; } = 25;
    public float? LearningRate { get; init; } = 0.01f;
    public int? BatchSize { get; init; } = 32;
    public bool ForceRetrain { get; init; } = false;
}

/// <summary>
/// Training response model
/// </summary>
public record TrainingResponse
{
    public required string SessionId { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public required DateTime QueuedAt { get; init; }
    public required int EstimatedDurationMinutes { get; init; }
}

/// <summary>
/// Training status response model
/// </summary>
public record TrainingStatusResponse
{
    public required string SessionId { get; init; }
    public required string Status { get; init; }
    public required int Progress { get; init; }
    public required string Message { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Error { get; init; }
    public string? ModelVersion { get; init; }
}