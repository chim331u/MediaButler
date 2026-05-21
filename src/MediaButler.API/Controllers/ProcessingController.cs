using Microsoft.AspNetCore.Mvc;
using MediaButler.Core.Enums;
using MediaButler.Services.Interfaces;
using MediaButler.ML.Interfaces;
using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for processing queue operations and status
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly ILogger<ProcessingController> _logger;
    private readonly IMlOrchestrationService _mlOrchestrationService;

    public ProcessingController(ILogger<ProcessingController> logger, IMlOrchestrationService mlOrchestrationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mlOrchestrationService = mlOrchestrationService ?? throw new ArgumentNullException(nameof(mlOrchestrationService));
    }

    /// <summary>
    /// Gets the current processing queue status
    /// </summary>
    /// <returns>Processing queue status information</returns>
    [HttpGet("queue/status")]
    public async Task<ActionResult<ProcessingQueueStatus>> GetQueueStatus()
    {
        try
        {
            // For now, return mock data since we need to implement the actual processing service
            var status = new ProcessingQueueStatus
            {
                QueueSize = 5,
                ActiveJobs = 2,
                CompletedToday = 15,
                FailedToday = 1,
                AvgProcessingTimeMs = 2500,
                LastActivity = DateTime.UtcNow.AddMinutes(-5)
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving processing queue status");
            return StatusCode(500, new { error = $"Failed to retrieve queue status: {ex.Message}" });
        }
    }

    /// <summary>
    /// Queues files for ML evaluation/re-evaluation based on specified statuses.
    /// Updates SuggestedCategory field for files in New, Classified, and ReadyToMove status.
    /// </summary>
    /// <param name="request">ML evaluation request parameters</param>
    /// <returns>Result of the ML evaluation operation</returns>
    /// <response code="200">ML evaluation queued successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("ml-evaluation/queue")]
    [ProducesResponseType(typeof(MlEvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MlEvaluationResponse>> QueueMlEvaluation([FromBody] MlEvaluationRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            _logger.LogInformation("Delegating ML evaluation queue operation to Orchestration Service");

            var response = await _mlOrchestrationService.ProcessMlEvaluationBatchAsync(request);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while queuing files for ML evaluation");
            return StatusCode(500, new { error = $"Failed to queue ML evaluation: {ex.Message}" });
        }
    }

    /// <summary>
    /// Classifies a single filename using ML and returns top 5 category predictions with confidence scores
    /// </summary>
    /// <param name="request">Classification request containing the filename to classify</param>
    /// <returns>Classification result with top 5 category predictions and confidence scores</returns>
    /// <response code="200">Classification successful</response>
    /// <response code="400">Invalid filename provided</response>
    /// <response code="503">ML model not ready</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("classify")]
    [ProducesResponseType(typeof(ClassificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ClassificationResponse>> ClassifyFilename([FromBody] ClassificationRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Filename))
            {
                return BadRequest(new { error = "Filename is required" });
            }

            _logger.LogInformation("Delegating classification for {Filename} to Orchestration Service", request.Filename);

            var response = await _mlOrchestrationService.ClassifyFilenameAsync(request.Filename);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while classifying filename: {Filename}", request?.Filename);
            return StatusCode(500, new { error = $"Classification failed: {ex.Message}" });
        }
    }
}