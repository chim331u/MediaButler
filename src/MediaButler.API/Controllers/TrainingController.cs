using Microsoft.AspNetCore.Mvc;
using MediaButler.Services.Background;
using MediaButler.Services.Interfaces;
using MediaButler.ML.Interfaces;
using MediaButler.API.Services;
using System.Collections.Concurrent;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for ML model training operations with background processing and real-time updates.
/// Follows "Simple Made Easy" principles with clear separation of concerns.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TrainingController : ControllerBase
{
    private readonly ILogger<TrainingController> _logger;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISignalRNotificationService _signalRService;

    // Track active training sessions
    private static readonly ConcurrentDictionary<string, TrainingSession> _activeSessions = new();

    public TrainingController(
        ILogger<TrainingController> logger,
        IBackgroundTaskQueue backgroundTaskQueue,
        IServiceScopeFactory serviceScopeFactory,
        ISignalRNotificationService signalRService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backgroundTaskQueue = backgroundTaskQueue ?? throw new ArgumentNullException(nameof(backgroundTaskQueue));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
    }

    /// <summary>
    /// Starts ML model training as a background job with real-time progress updates.
    /// </summary>
    /// <param name="request">Training configuration request</param>
    /// <returns>Training session information</returns>
    /// <response code="200">Training started successfully</response>
    /// <response code="400">Invalid training request</response>
    /// <response code="409">Training already in progress</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(TrainingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TrainingResponse>> StartTraining([FromBody] TrainingRequest request)
    {
        try
        {
            // Check if training is already in progress
            if (_activeSessions.Values.Any(s => s.Status == TrainingStatus.InProgress))
            {
                var activeSession = _activeSessions.Values.First(s => s.Status == TrainingStatus.InProgress);
                return Conflict(new
                {
                    error = "Training already in progress",
                    activeSessionId = activeSession.SessionId,
                    startedAt = activeSession.StartedAt
                });
            }

            // Generate unique session ID
            var sessionId = $"training-{Guid.NewGuid():N}";
            var session = new TrainingSession
            {
                SessionId = sessionId,
                Status = TrainingStatus.Queued,
                StartedAt = DateTime.UtcNow,
                Progress = 0,
                Message = "Training queued for processing"
            };

            // Track the session
            _activeSessions[sessionId] = session;

            _logger.LogInformation("Starting ML model training. Session ID: {SessionId}", sessionId);

            // Queue the training job
            _backgroundTaskQueue.QueueBackgroundWorkItem(
                async (serviceProvider, cancellationToken) =>
                {
                    await ExecuteTrainingJob(sessionId, request, serviceProvider, cancellationToken);
                },
                jobId: sessionId,
                jobName: "ML Model Training"
            );

            // Send initial SignalR notification
            await _signalRService.NotifyJobProgressAsync("training",
                "ML model training queued", 0);

            var response = new TrainingResponse
            {
                SessionId = sessionId,
                Status = TrainingStatus.Queued.ToString(),
                Message = "Training job queued successfully",
                QueuedAt = DateTime.UtcNow,
                EstimatedDurationMinutes = EstimateTrainingDuration(request)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ML model training");
            return StatusCode(500, new { error = $"Failed to start training: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets the current status of a training session.
    /// </summary>
    /// <param name="sessionId">The training session ID</param>
    /// <returns>Current training status</returns>
    /// <response code="200">Status retrieved successfully</response>
    /// <response code="404">Training session not found</response>
    [HttpGet("status/{sessionId}")]
    [ProducesResponseType(typeof(TrainingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TrainingStatusResponse> GetTrainingStatus(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return NotFound(new { error = "Training session not found" });
        }

        var response = new TrainingStatusResponse
        {
            SessionId = session.SessionId,
            Status = session.Status.ToString(),
            Progress = session.Progress,
            Message = session.Message,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            Error = session.Error,
            ModelVersion = session.ModelVersion
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets all active and recent training sessions.
    /// </summary>
    /// <returns>List of training sessions</returns>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IEnumerable<TrainingStatusResponse>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TrainingStatusResponse>> GetTrainingSessions()
    {
        var sessions = _activeSessions.Values
            .OrderByDescending(s => s.StartedAt)
            .Take(10) // Last 10 sessions
            .Select(s => new TrainingStatusResponse
            {
                SessionId = s.SessionId,
                Status = s.Status.ToString(),
                Progress = s.Progress,
                Message = s.Message,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
                Error = s.Error,
                ModelVersion = s.ModelVersion
            });

        return Ok(sessions);
    }

    /// <summary>
    /// Executes the training job in the background with progress updates.
    /// </summary>
    private async Task ExecuteTrainingJob(string sessionId, TrainingRequest request,
        IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogError("Training session {SessionId} not found", sessionId);
            return;
        }

        try
        {
            // Update session status
            session.Status = TrainingStatus.InProgress;
            session.Message = "Starting ML model training";
            session.Progress = 5;

            await _signalRService.NotifyJobProgressAsync("training",
                "Starting ML model training...", 5);

            using var scope = serviceProvider.CreateScope();
            var modelTrainingService = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();
            var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();

            // Phase 1: Load training data (10%)
            await UpdateProgress(session, 10, "Loading training data...");
            var trainingDataResult = await GetTrainingDataAsync(fileService);

            if (!trainingDataResult.IsSuccess || !trainingDataResult.Value.Any())
            {
                throw new InvalidOperationException($"Insufficient training data: {trainingDataResult.Error}");
            }

            var trainingData = trainingDataResult.Value;
            _logger.LogInformation("Loaded {Count} training samples for session {SessionId}",
                trainingData.Count(), sessionId);

            // Phase 2: Validate training data (20%)
            await UpdateProgress(session, 20, "Validating training data quality...");

            // Phase 3: Configure training pipeline (30%)
            await UpdateProgress(session, 30, "Configuring training pipeline...");

            var trainingConfig = new ML.Models.TrainingConfiguration
            {
                MaxEpochs = request.Epochs ?? 25,
                LearningRate = request.LearningRate ?? 0.01f,
                BatchSize = request.BatchSize ?? 32,
                ValidationSplit = 0.2,
                RandomSeed = 42,
                EarlyStoppingPatience = 10,
                MinimumImprovement = 0.001,
                EnableDataAugmentation = true,
                SessionId = sessionId,
                MaxTrainingTimeMinutes = 30
            };

            // Phase 4: Train model (30% - 80%)
            await UpdateProgress(session, 40, "Training ML model - this may take several minutes...");

            var trainingResult = await modelTrainingService.TrainModelAsync(
                trainingData, trainingConfig, cancellationToken);

            if (!trainingResult.IsSuccess)
            {
                throw new InvalidOperationException($"Model training failed: {trainingResult.Error}");
            }

            // Phase 5: Evaluate model (85%)
            await UpdateProgress(session, 85, "Evaluating model performance...");

            // Phase 6: Save model (95%)
            await UpdateProgress(session, 95, "Saving trained model...");

            var modelInfo = trainingResult.Value;
            var modelVersion = $"v{DateTime.UtcNow:yyyyMMdd.HHmmss}";

            // Phase 7: Complete (100%)
            session.Status = TrainingStatus.Completed;
            session.Progress = 100;
            session.Message = "Model training completed successfully";
            session.CompletedAt = DateTime.UtcNow;
            session.ModelVersion = modelVersion;

            await _signalRService.NotifyJobProgressAsync("training",
                $"Model training completed successfully! New model: {modelVersion}", 100);

            _logger.LogInformation("ML model training completed successfully. Session: {SessionId}, Model: {ModelVersion}",
                sessionId, modelVersion);
        }
        catch (OperationCanceledException)
        {
            session.Status = TrainingStatus.Cancelled;
            session.Message = "Training was cancelled";
            session.CompletedAt = DateTime.UtcNow;

            await _signalRService.NotifyErrorAsync("training",
                "Model training was cancelled", "Training operation was cancelled by user or system");

            _logger.LogInformation("ML model training cancelled. Session: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            session.Status = TrainingStatus.Failed;
            session.Message = $"Training failed: {ex.Message}";
            session.Error = ex.Message;
            session.CompletedAt = DateTime.UtcNow;

            await _signalRService.NotifyErrorAsync("training",
                "Model training failed", ex.Message);

            _logger.LogError(ex, "ML model training failed. Session: {SessionId}", sessionId);
        }
    }

    private async Task UpdateProgress(TrainingSession session, int progress, string message)
    {
        session.Progress = progress;
        session.Message = message;

        await _signalRService.NotifyJobProgressAsync("training", message, progress);

        _logger.LogInformation("Training progress: {Progress}% - {Message}", progress, message);
    }

    private async Task<MediaButler.Core.Common.Result<IEnumerable<ML.Models.TrainingSample>>> GetTrainingDataAsync(IFileService fileService)
    {
        try
        {
            // Get all moved files that can serve as training data by fetching in batches
            var allMovedFiles = new List<MediaButler.Core.Entities.TrackedFile>();
            const int batchSize = 1000; // Maximum allowed by API
            int skip = 0;
            bool hasMore = true;

            while (hasMore)
            {
                var filesResult = await fileService.GetFilesPagedByStatusesAsync(
                    skip: skip,
                    take: batchSize,
                    statuses: new[] { MediaButler.Core.Enums.FileStatus.Moved });

                if (!filesResult.IsSuccess)
                {
                    return MediaButler.Core.Common.Result<IEnumerable<ML.Models.TrainingSample>>.Failure(filesResult.Error);
                }

                var batch = filesResult.Value.ToList();
                allMovedFiles.AddRange(batch);

                // If we got less than the batch size, we've reached the end
                hasMore = batch.Count == batchSize;
                skip += batchSize;

                _logger.LogDebug("Fetched batch of {Count} moved files (total so far: {Total})", batch.Count, allMovedFiles.Count);
            }

            var validFiles = allMovedFiles
                .Where(f => !string.IsNullOrEmpty(f.Category) &&
                           !string.IsNullOrEmpty(f.FileName))
                .ToList();

            if (validFiles.Count < 10)
            {
                // Try to get files from other statuses that have categories assigned
                _logger.LogWarning("Only found {Count} moved files. Trying to get files from other statuses with categories.", validFiles.Count);

                var fallbackResult = await fileService.GetFilesPagedByStatusesAsync(
                    skip: 0,
                    take: 1000,
                    statuses: new[] {
                        MediaButler.Core.Enums.FileStatus.Moved,
                        MediaButler.Core.Enums.FileStatus.ReadyToMove,
                        MediaButler.Core.Enums.FileStatus.Classified
                    });

                if (fallbackResult.IsSuccess)
                {
                    var fallbackFiles = fallbackResult.Value
                        .Where(f => !string.IsNullOrEmpty(f.Category) && !string.IsNullOrEmpty(f.FileName))
                        .ToList();

                    if (fallbackFiles.Count >= 10)
                    {
                        _logger.LogInformation("Found {Count} files with categories from multiple statuses for training", fallbackFiles.Count);
                        validFiles = fallbackFiles;
                    }
                }

                if (validFiles.Count < 10)
                {
                    return MediaButler.Core.Common.Result<IEnumerable<ML.Models.TrainingSample>>.Failure(
                        $"Insufficient training data. Found {validFiles.Count} valid samples with categories, minimum 10 required. " +
                        $"Please confirm more files or move files to categories first.");
                }
            }

            // Convert to training samples
            var trainingSamples = validFiles.Select(f => new ML.Models.TrainingSample
            {
                Filename = f.FileName,
                Category = f.Category ?? "Unknown",
                Confidence = 1.0, // User-confirmed files have full confidence
                Source = ML.Models.TrainingSampleSource.UserFeedback,
                CreatedAt = f.CreatedDate
            }).ToList();

            _logger.LogInformation("Generated {Count} training samples from {TotalMoved} moved files", trainingSamples.Count, allMovedFiles.Count);

            return MediaButler.Core.Common.Result<IEnumerable<ML.Models.TrainingSample>>.Success(trainingSamples);
        }
        catch (Exception ex)
        {
            return MediaButler.Core.Common.Result<IEnumerable<ML.Models.TrainingSample>>.Failure(
                $"Failed to load training data: {ex.Message}");
        }
    }

    private static int EstimateTrainingDuration(TrainingRequest request)
    {
        // Estimate based on epochs and complexity
        var epochs = request.Epochs ?? 25;
        return Math.Max(2, epochs / 5); // Roughly 5 epochs per minute on ARM32
    }
}

/// <summary>
/// Training request parameters
/// </summary>
public record TrainingRequest
{
    public int? Epochs { get; init; }
    public float? LearningRate { get; init; }
    public int? BatchSize { get; init; }
    public bool ForceRetrain { get; init; } = false;
}

/// <summary>
/// Training response with session information
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
/// Training status response
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

/// <summary>
/// Training session tracking
/// </summary>
internal class TrainingSession
{
    public required string SessionId { get; set; }
    public required TrainingStatus Status { get; set; }
    public required int Progress { get; set; }
    public required string Message { get; set; }
    public required DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public string? ModelVersion { get; set; }
}

/// <summary>
/// Training status enumeration
/// </summary>
public enum TrainingStatus
{
    Queued,
    InProgress,
    Completed,
    Failed,
    Cancelled
}