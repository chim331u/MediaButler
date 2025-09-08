using Microsoft.AspNetCore.Mvc;
using MediaButler.Services.Interfaces;
using MediaButler.ML.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
    private readonly HealthCheckService _healthCheckService;
    private readonly IPredictionService? _predictionService;

    /// <summary>
    /// Initializes a new instance of the HealthController.
    /// </summary>
    /// <param name="statsService">Service for retrieving system statistics</param>
    /// <param name="healthCheckService">Health check service for system components</param>
    /// <param name="predictionService">Optional ML prediction service for graceful degradation</param>
    public HealthController(
        IStatsService statsService,
        HealthCheckService healthCheckService,
        IPredictionService? predictionService = null)
    {
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _predictionService = predictionService; // Optional for graceful degradation
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

            // Check ML service health with graceful degradation
            var mlHealthStatus = await GetMLHealthStatusAsync();

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
                },
                MachineLearning = mlHealthStatus
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

    /// <summary>
    /// Gets machine learning service health status.
    /// </summary>
    /// <returns>ML service health information</returns>
    /// <response code="200">ML health status retrieved successfully</response>
    /// <response code="503">ML services are unavailable</response>
    [HttpGet("ml")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMLHealth()
    {
        var mlHealthStatus = await GetMLHealthStatusAsync();
        
        if (mlHealthStatus.GetType().GetProperty("Status")?.GetValue(mlHealthStatus)?.ToString() == "Unavailable")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, mlHealthStatus);
        }

        return Ok(mlHealthStatus);
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets ML health status with graceful degradation support.
    /// </summary>
    /// <returns>ML health status object</returns>
    private async Task<object> GetMLHealthStatusAsync()
    {
        try
        {
            // Check if ML services are available
            if (_predictionService == null)
            {
                return new
                {
                    Status = "Unavailable",
                    Timestamp = DateTime.UtcNow,
                    Reason = "ML services not registered",
                    GracefulDegradation = true,
                    Impact = "File classification will be disabled, manual categorization required"
                };
            }

            // Run ML-specific health checks
            var mlHealthResult = await _healthCheckService.CheckHealthAsync(
                predicate: (check) => check.Tags.Contains("ml"));

            var healthData = new Dictionary<string, object>
            {
                {"Timestamp", DateTime.UtcNow}
            };

            bool allHealthy = true;
            bool anyDegraded = false;

            foreach (var entry in mlHealthResult.Entries)
            {
                var checkData = new Dictionary<string, object>
                {
                    {"Status", entry.Value.Status.ToString()},
                    {"Description", entry.Value.Description ?? "No description"}
                };

                if (entry.Value.Data?.Count > 0)
                {
                    checkData["Details"] = entry.Value.Data;
                }

                if (entry.Value.Exception != null)
                {
                    checkData["Error"] = entry.Value.Exception.Message;
                }

                healthData[entry.Key] = checkData;

                if (entry.Value.Status == HealthStatus.Unhealthy)
                {
                    allHealthy = false;
                }
                else if (entry.Value.Status == HealthStatus.Degraded)
                {
                    anyDegraded = true;
                }
            }

            // Determine overall status
            string overallStatus;
            string impact = "None";

            if (allHealthy && !anyDegraded)
            {
                overallStatus = "Healthy";
            }
            else if (!allHealthy)
            {
                overallStatus = "Unhealthy";
                impact = "File classification may be slower or unavailable";
            }
            else
            {
                overallStatus = "Degraded";
                impact = "File classification performance may be reduced";
            }

            return new
            {
                Status = overallStatus,
                Timestamp = DateTime.UtcNow,
                GracefulDegradation = !allHealthy,
                Impact = impact,
                Checks = healthData
            };
        }
        catch (Exception ex)
        {
            return new
            {
                Status = "Unavailable",
                Timestamp = DateTime.UtcNow,
                Reason = $"Health check failed: {ex.Message}",
                GracefulDegradation = true,
                Impact = "File classification will be disabled, manual categorization required"
            };
        }
    }

    #endregion
}