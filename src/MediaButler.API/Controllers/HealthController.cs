using Microsoft.AspNetCore.Mvc;
using MediaButler.Services.Interfaces;

namespace MediaButler.API.Controllers;

/// <summary>
/// Provides health check and system monitoring endpoints for MediaButler API.
/// Follows "Simple Made Easy" principles with clear, single-purpose endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IStatsService _statsService;

    /// <summary>
    /// Initializes a new instance of the HealthController.
    /// </summary>
    /// <param name="statsService">Service for retrieving system statistics</param>
    public HealthController(IStatsService statsService)
    {
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
    }

    /// <summary>
    /// Gets basic health status of the MediaButler API.
    /// </summary>
    /// <returns>Health status with timestamp</returns>
    /// <response code="200">API is healthy and operational</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new 
        { 
            Status = "Healthy", 
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Service = "MediaButler.API"
        });
    }

    /// <summary>
    /// Gets detailed system health including database connectivity and performance metrics.
    /// </summary>
    /// <returns>Detailed health status with system metrics</returns>
    /// <response code="200">Detailed health information retrieved successfully</response>
    /// <response code="500">System health check failed</response>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDetailedHealth()
    {
        try
        {
            var statsResult = await _statsService.GetProcessingStatsAsync();
            
            if (!statsResult.IsSuccess)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Error = "Failed to retrieve system statistics",
                    Details = statsResult.Error
                });
            }

            var memoryUsage = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;

            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Service = "MediaButler.API",
                Database = new
                {
                    Status = "Connected",
                    TotalFiles = statsResult.Value.TotalFiles,
                    ProcessedToday = statsResult.Value.ProcessedToday
                },
                Memory = new
                {
                    ManagedMemoryMB = Math.Round(memoryUsage / 1024.0 / 1024.0, 2),
                    WorkingSetMB = Math.Round(workingSet / 1024.0 / 1024.0, 2),
                    TargetLimitMB = 300 // ARM32 memory constraint
                },
                Processing = new
                {
                    StatusCounts = statsResult.Value.StatusCounts,
                    AverageProcessingTimeMinutes = statsResult.Value.AverageProcessingTimeMinutes
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Error = "Health check failed",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets readiness status indicating if the service is ready to handle requests.
    /// </summary>
    /// <returns>Readiness status</returns>
    /// <response code="200">Service is ready to handle requests</response>
    /// <response code="503">Service is not ready</response>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            // Simple database connectivity check
            var statsResult = await _statsService.GetProcessingStatsAsync();
            
            if (!statsResult.IsSuccess)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    Status = "NotReady",
                    Timestamp = DateTime.UtcNow,
                    Reason = "Database connectivity issues"
                });
            }

            return Ok(new
            {
                Status = "Ready",
                Timestamp = DateTime.UtcNow,
                DatabaseConnected = true
            });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Status = "NotReady",
                Timestamp = DateTime.UtcNow,
                Reason = "Service initialization incomplete"
            });
        }
    }

    /// <summary>
    /// Gets liveness status indicating if the service is alive and running.
    /// </summary>
    /// <returns>Liveness status</returns>
    /// <response code="200">Service is alive</response>
    [HttpGet("live")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        return Ok(new
        {
            Status = "Alive",
            Timestamp = DateTime.UtcNow,
            Uptime = Environment.TickCount64 / 1000 // seconds since start
        });
    }
}