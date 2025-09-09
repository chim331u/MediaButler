using MediaButler.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaButler.API.Controllers;

/// <summary>
/// API controller for system metrics and monitoring data.
/// Provides endpoints for real-time metrics, performance data, and system health.
/// Following "Simple Made Easy" principles with clear, focused responsibilities.
/// </summary>
/// <remarks>
/// This controller provides:
/// - Real-time system health and status information
/// - Processing queue metrics and throughput data
/// - ML classification performance and accuracy metrics
/// - Error rates and system alerting information
/// - Performance counters and resource utilization
/// 
/// All endpoints return JSON data optimized for monitoring dashboards and alerting systems.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsCollectionService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsCollectionService metricsService,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets comprehensive system health summary.
    /// </summary>
    /// <returns>System health summary with all metrics and alerts.</returns>
    /// <response code="200">System health summary retrieved successfully</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemHealthSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSystemHealthAsync()
    {
        try
        {
            var healthSummary = await _metricsService.GetSystemHealthSummaryAsync();
            return Ok(healthSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system health summary");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve system health", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets current processing queue metrics and throughput data.
    /// </summary>
    /// <param name="timeWindowHours">Time window in hours for metrics aggregation (default: 1)</param>
    /// <returns>Queue metrics including depth, throughput, and processing times.</returns>
    /// <response code="200">Queue metrics retrieved successfully</response>
    /// <response code="400">Invalid time window parameter</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(QueueMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQueueMetricsAsync([FromQuery] int timeWindowHours = 1)
    {
        if (timeWindowHours < 1 || timeWindowHours > 168) // Max 1 week
        {
            return BadRequest(new { error = "Time window must be between 1 and 168 hours" });
        }

        try
        {
            var timeWindow = TimeSpan.FromHours(timeWindowHours);
            var queueMetrics = await _metricsService.GetQueueMetricsAsync(timeWindow);
            return Ok(queueMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving queue metrics for time window {TimeWindow} hours", timeWindowHours);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve queue metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets ML classification performance metrics and accuracy data.
    /// </summary>
    /// <param name="timeWindowHours">Time window in hours for metrics aggregation (default: 24)</param>
    /// <returns>Classification metrics including accuracy rates and confidence distributions.</returns>
    /// <response code="200">Classification metrics retrieved successfully</response>
    /// <response code="400">Invalid time window parameter</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet("classification")]
    [ProducesResponseType(typeof(ClassificationMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetClassificationMetricsAsync([FromQuery] int timeWindowHours = 24)
    {
        if (timeWindowHours < 1 || timeWindowHours > 168) // Max 1 week
        {
            return BadRequest(new { error = "Time window must be between 1 and 168 hours" });
        }

        try
        {
            var timeWindow = TimeSpan.FromHours(timeWindowHours);
            var classificationMetrics = await _metricsService.GetClassificationMetricsAsync(timeWindow);
            return Ok(classificationMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving classification metrics for time window {TimeWindow} hours", timeWindowHours);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve classification metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets error rate metrics and failure analysis data.
    /// </summary>
    /// <param name="timeWindowHours">Time window in hours for metrics aggregation (default: 24)</param>
    /// <returns>Error metrics including error rates, types, and intervention requirements.</returns>
    /// <response code="200">Error metrics retrieved successfully</response>
    /// <response code="400">Invalid time window parameter</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet("errors")]
    [ProducesResponseType(typeof(ErrorMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetErrorMetricsAsync([FromQuery] int timeWindowHours = 24)
    {
        if (timeWindowHours < 1 || timeWindowHours > 168) // Max 1 week
        {
            return BadRequest(new { error = "Time window must be between 1 and 168 hours" });
        }

        try
        {
            var timeWindow = TimeSpan.FromHours(timeWindowHours);
            var errorMetrics = await _metricsService.GetErrorMetricsAsync(timeWindow);
            return Ok(errorMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving error metrics for time window {TimeWindow} hours", timeWindowHours);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve error metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets performance metrics and resource utilization data.
    /// </summary>
    /// <param name="timeWindowHours">Time window in hours for metrics aggregation (default: 1)</param>
    /// <returns>Performance metrics including processing times and resource usage.</returns>
    /// <response code="200">Performance metrics retrieved successfully</response>
    /// <response code="400">Invalid time window parameter</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet("performance")]
    [ProducesResponseType(typeof(PerformanceMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPerformanceMetricsAsync([FromQuery] int timeWindowHours = 1)
    {
        if (timeWindowHours < 1 || timeWindowHours > 168) // Max 1 week
        {
            return BadRequest(new { error = "Time window must be between 1 and 168 hours" });
        }

        try
        {
            var timeWindow = TimeSpan.FromHours(timeWindowHours);
            var performanceMetrics = await _metricsService.GetPerformanceMetricsAsync(timeWindow);
            return Ok(performanceMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics for time window {TimeWindow} hours", timeWindowHours);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve performance metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets current system status in a simplified format for health checks.
    /// </summary>
    /// <returns>Simplified system status for monitoring and alerting.</returns>
    /// <response code="200">System is healthy</response>
    /// <response code="503">System has warnings or critical issues</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetSystemStatusAsync()
    {
        try
        {
            var healthSummary = await _metricsService.GetSystemHealthSummaryAsync();
            
            var status = new
            {
                status = healthSummary.OverallStatus,
                timestamp = healthSummary.GeneratedAt,
                queueDepth = healthSummary.QueueStatus.CurrentQueueDepth,
                errorRate = healthSummary.ErrorStatus.ErrorRate,
                memoryUsageMB = healthSummary.SystemPerformance.CurrentMemoryUsageMB,
                alertCount = healthSummary.Alerts.Count,
                alerts = healthSummary.Alerts
            };

            // Return 503 for Warning or Critical status
            if (healthSummary.OverallStatus == "Critical")
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, status);
            }

            if (healthSummary.OverallStatus == "Warning")
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, status);
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system status");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new 
                { 
                    status = "Critical", 
                    error = "Failed to retrieve system status", 
                    timestamp = DateTime.UtcNow,
                    details = ex.Message 
                });
        }
    }

    /// <summary>
    /// Endpoint for external monitoring systems to check service availability.
    /// Returns minimal response optimized for health check probes.
    /// </summary>
    /// <returns>Simple health check response.</returns>
    /// <response code="200">Service is available</response>
    /// <response code="503">Service is unavailable</response>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PingAsync()
    {
        try
        {
            // Simple availability check
            var healthSummary = await _metricsService.GetSystemHealthSummaryAsync();
            
            return Ok(new 
            { 
                status = "ok", 
                timestamp = DateTime.UtcNow,
                version = "1.0.0" // Could be injected from configuration
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new 
                { 
                    status = "error", 
                    timestamp = DateTime.UtcNow,
                    error = ex.Message 
                });
        }
    }
}