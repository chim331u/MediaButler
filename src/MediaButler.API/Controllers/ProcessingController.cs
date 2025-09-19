using Microsoft.AspNetCore.Mvc;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for processing queue operations and status
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly ILogger<ProcessingController> _logger;

    public ProcessingController(ILogger<ProcessingController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
}

/// <summary>
/// Processing queue status information
/// </summary>
public record ProcessingQueueStatus
{
    public int QueueSize { get; init; }
    public int ActiveJobs { get; init; }
    public int CompletedToday { get; init; }
    public int FailedToday { get; init; }
    public int AvgProcessingTimeMs { get; init; }
    public DateTime LastActivity { get; init; } = DateTime.UtcNow;
}