using Microsoft.AspNetCore.Mvc;
using MediaButler.Services.ML;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for ML model training operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TrainingController : ControllerBase
{
    private readonly ILogger<TrainingController> _logger;
    private readonly IDatabaseTrainingService _trainingService;

    public TrainingController(
        ILogger<TrainingController> logger,
        IDatabaseTrainingService trainingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trainingService = trainingService ?? throw new ArgumentNullException(nameof(trainingService));
    }

    /// <summary>
    /// Starts training a new ML model from database TrackedFiles data
    /// </summary>
    /// <param name="request">Training request parameters</param>
    /// <returns>Training session information</returns>
    /// <response code="200">Training started successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="500">Training failed</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(TrainingStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TrainingStartResponse>> StartTraining(
        [FromBody] TrainingStartRequest? request)
    {
        try
        {
            var sessionId = request?.SessionId ?? Guid.NewGuid().ToString();
            _logger.LogInformation("Starting ML model training from database (session: {SessionId})", sessionId);

            // Train model from database
            var trainingResult = await _trainingService.TrainModelFromDatabaseAsync(
                sessionId,
                HttpContext.RequestAborted);

            if (trainingResult.IsFailure)
            {
                _logger.LogError("Training failed: {Error}", trainingResult.Error);
                return StatusCode(500, new { error = $"Training failed: {trainingResult.Error}" });
            }

            var trainedModel = trainingResult.Value;

            var response = new TrainingStartResponse
            {
                SessionId = sessionId,
                Status = "Completed",
                Message = $"Model trained successfully with {trainedModel.ValidationMetrics.Accuracy:P2} accuracy",
                StartedAt = DateTime.UtcNow,
                Accuracy = trainedModel.ValidationMetrics.Accuracy,
                TrainingSampleCount = trainedModel.TrainingSampleCount,
                CategoryCount = trainedModel.ValidationMetrics.PerCategoryMetrics?.Count ?? 0,
                ModelVersion = trainedModel.ModelVersion
            };

            _logger.LogInformation("Training completed successfully. Accuracy: {Accuracy:P2}, Samples: {Samples}",
                trainedModel.ValidationMetrics.Accuracy, trainedModel.TrainingSampleCount);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting model training");
            return StatusCode(500, new { error = $"Training failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Starts training a new ML model from database TrackedFiles data
    /// </summary>
    /// <returns>Training session information</returns>
    /// <response code="200">Training started successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="500">Training failed</response>
    [HttpPost("trainModel")]
    [ProducesResponseType(typeof(TrainingStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TrainingStartResponse>> StartTraining()
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting ML model training from database (session: {SessionId})", sessionId);

            // Train model from database
            var trainingResult = await _trainingService.TrainModelFromDatabaseAsync(
                sessionId,
                HttpContext.RequestAborted);

            if (trainingResult.IsFailure)
            {
                _logger.LogError("Training failed: {Error}", trainingResult.Error);
                return StatusCode(500, new { error = $"Training failed: {trainingResult.Error}" });
            }

            var trainedModel = trainingResult.Value;

            var response = new TrainingStartResponse
            {
                SessionId = sessionId,
                Status = "Completed",
                Message = $"Model trained successfully with {trainedModel.ValidationMetrics.Accuracy:P2} accuracy",
                StartedAt = DateTime.UtcNow,
                Accuracy = trainedModel.ValidationMetrics.Accuracy,
                TrainingSampleCount = trainedModel.TrainingSampleCount,
                CategoryCount = trainedModel.ValidationMetrics.PerCategoryMetrics?.Count ?? 0,
                ModelVersion = trainedModel.ModelVersion
            };

            _logger.LogInformation("Training completed successfully. Accuracy: {Accuracy:P2}, Samples: {Samples}",
                trainedModel.ValidationMetrics.Accuracy, trainedModel.TrainingSampleCount);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting model training");
            return StatusCode(500, new { error = $"Training failed: {ex.Message}" });
        }
    }
    
    /// <summary>
    /// Gets training statistics from the database
    /// </summary>
    /// <returns>Training data statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(TrainingStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainingStatsResponse>> GetTrainingStats()
    {
        try
        {
            var samplesResult = await _trainingService.LoadTrainingSamplesFromDatabaseAsync(
                HttpContext.RequestAborted);

            if (samplesResult.IsFailure)
            {
                return StatusCode(500, new { error = samplesResult.Error });
            }

            var samples = samplesResult.Value;
            var categoryGroups = samples
                .GroupBy(s => s.Category)
                .Select(g => new CategoryStats
                {
                    Category = g.Key,
                    SampleCount = g.Count()
                })
                .OrderByDescending(c => c.SampleCount)
                .ToList();

            var response = new TrainingStatsResponse
            {
                TotalSamples = samples.Count,
                CategoryCount = categoryGroups.Count,
                Categories = categoryGroups,
                LastUpdated = samples.Max(s => s.CreatedAt)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving training stats");
            return StatusCode(500, new { error = $"Failed to retrieve stats: {ex.Message}" });
        }
    }
}

public record TrainingStartRequest
{
    public string? SessionId { get; init; }
}

public record TrainingStartResponse
{
    public required string SessionId { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public DateTime StartedAt { get; init; }
    public double? Accuracy { get; init; }
    public int? TrainingSampleCount { get; init; }
    public int? CategoryCount { get; init; }
    public string? ModelVersion { get; init; }
}

public record TrainingStatsResponse
{
    public int TotalSamples { get; init; }
    public int CategoryCount { get; init; }
    public required List<CategoryStats> Categories { get; init; }
    public DateTime LastUpdated { get; init; }
}

public record CategoryStats
{
    public required string Category { get; init; }
    public int SampleCount { get; init; }
}
