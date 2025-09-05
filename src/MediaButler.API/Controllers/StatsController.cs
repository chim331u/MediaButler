using Microsoft.AspNetCore.Mvc;
using MediaButler.Services.Interfaces;

namespace MediaButler.API.Controllers;

/// <summary>
/// Provides monitoring and analytics endpoints for MediaButler system statistics.
/// Handles processing metrics, performance data, and system insights.
/// Follows "Simple Made Easy" principles with clear statistical boundaries.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    /// <summary>
    /// Initializes a new instance of the StatsController.
    /// </summary>
    /// <param name="statsService">Service for statistics and monitoring</param>
    public StatsController(IStatsService statsService)
    {
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
    }

    /// <summary>
    /// Gets comprehensive processing statistics for the MediaButler system.
    /// </summary>
    /// <returns>Processing statistics including file counts and performance metrics</returns>
    /// <response code="200">Processing statistics retrieved successfully</response>
    /// <response code="500">Failed to retrieve processing statistics</response>
    [HttpGet("processing")]
    [ProducesResponseType(typeof(ProcessingStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProcessingStats()
    {
        var result = await _statsService.GetProcessingStatsAsync();
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets ML classification performance metrics and accuracy statistics.
    /// </summary>
    /// <returns>ML classification performance data</returns>
    /// <response code="200">ML performance statistics retrieved successfully</response>
    /// <response code="500">Failed to retrieve ML performance statistics</response>
    [HttpGet("ml-performance")]
    [ProducesResponseType(typeof(MLPerformanceStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMLPerformanceStats()
    {
        var result = await _statsService.GetMLPerformanceStatsAsync();
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets system health metrics including error rates and processing performance.
    /// </summary>
    /// <returns>System health data optimized for ARM32 monitoring</returns>
    /// <response code="200">System health metrics retrieved successfully</response>
    /// <response code="500">Failed to retrieve system health metrics</response>
    [HttpGet("system-health")]
    [ProducesResponseType(typeof(SystemHealthStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSystemHealthStats()
    {
        var result = await _statsService.GetSystemHealthStatsAsync();
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets file processing activity for a specific date range.
    /// </summary>
    /// <param name="startDate">Start date for the range (format: yyyy-MM-dd)</param>
    /// <param name="endDate">End date for the range (format: yyyy-MM-dd)</param>
    /// <returns>Activity statistics for the specified date range</returns>
    /// <response code="200">Activity statistics retrieved successfully</response>
    /// <response code="400">Invalid date range specified</response>
    /// <response code="500">Failed to retrieve activity statistics</response>
    [HttpGet("activity")]
    [ProducesResponseType(typeof(ActivityStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActivityStats(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var start = DateTime.Today.AddDays(-7); // Default to last 7 days
        var end = DateTime.Today.AddDays(1);   // Include today

        if (!string.IsNullOrWhiteSpace(startDate) && !DateTime.TryParse(startDate, out start))
        {
            return BadRequest(new { Error = "Invalid start date format. Use yyyy-MM-dd." });
        }

        if (!string.IsNullOrWhiteSpace(endDate) && !DateTime.TryParse(endDate, out end))
        {
            return BadRequest(new { Error = "Invalid end date format. Use yyyy-MM-dd." });
        }

        if (start >= end)
        {
            return BadRequest(new { Error = "Start date must be before end date." });
        }

        var result = await _statsService.GetActivityStatsAsync(start, end);
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets category distribution showing how files are organized.
    /// </summary>
    /// <returns>Category distribution with counts and percentages</returns>
    /// <response code="200">Category distribution retrieved successfully</response>
    /// <response code="500">Failed to retrieve category distribution</response>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(CategoryStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCategoryDistribution()
    {
        var result = await _statsService.GetCategoryDistributionAsync();
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets processing throughput metrics showing files processed over time.
    /// </summary>
    /// <param name="hours">Number of hours to analyze (default: 24)</param>
    /// <returns>Throughput statistics for the specified period</returns>
    /// <response code="200">Throughput statistics retrieved successfully</response>
    /// <response code="400">Invalid hours parameter</response>
    /// <response code="500">Failed to retrieve throughput statistics</response>
    [HttpGet("throughput")]
    [ProducesResponseType(typeof(ThroughputStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetThroughputStats([FromQuery] int hours = 24)
    {
        if (hours < 1 || hours > 168) // Max 1 week
        {
            return BadRequest(new { Error = "Hours must be between 1 and 168 (1 week)." });
        }

        var result = await _statsService.GetThroughputStatsAsync(hours);
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets error analysis showing common errors and failure patterns.
    /// </summary>
    /// <param name="days">Number of days to analyze (default: 7)</param>
    /// <returns>Error analysis statistics</returns>
    /// <response code="200">Error statistics retrieved successfully</response>
    /// <response code="400">Invalid days parameter</response>
    /// <response code="500">Failed to retrieve error statistics</response>
    [HttpGet("errors")]
    [ProducesResponseType(typeof(ErrorStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetErrorAnalysis([FromQuery] int days = 7)
    {
        if (days < 1 || days > 90)
        {
            return BadRequest(new { Error = "Days must be between 1 and 90." });
        }

        var result = await _statsService.GetErrorAnalysisAsync(days);
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets file size distribution statistics to understand storage patterns.
    /// </summary>
    /// <returns>File size distribution data</returns>
    /// <response code="200">File size statistics retrieved successfully</response>
    /// <response code="500">Failed to retrieve file size statistics</response>
    [HttpGet("file-sizes")]
    [ProducesResponseType(typeof(FileSizeStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFileSizeDistribution()
    {
        var result = await _statsService.GetFileSizeDistributionAsync();
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets historical trends for processing metrics over time.
    /// </summary>
    /// <param name="days">Number of days of history to include (default: 30, max: 365)</param>
    /// <returns>Historical trend analysis data</returns>
    /// <response code="200">Trend statistics retrieved successfully</response>
    /// <response code="400">Invalid days parameter</response>
    /// <response code="500">Failed to retrieve trend statistics</response>
    [HttpGet("trends")]
    [ProducesResponseType(typeof(TrendStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHistoricalTrends([FromQuery] int days = 30)
    {
        if (days < 1 || days > 365)
        {
            return BadRequest(new { Error = "Days must be between 1 and 365." });
        }

        var result = await _statsService.GetHistoricalTrendsAsync(days);
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets a comprehensive dashboard summary with key metrics.
    /// </summary>
    /// <returns>Dashboard summary with essential system metrics</returns>
    /// <response code="200">Dashboard summary retrieved successfully</response>
    /// <response code="500">Failed to retrieve dashboard summary</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDashboardSummary()
    {
        var result = await _statsService.GetDashboardSummaryAsync();
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }

    /// <summary>
    /// Gets system performance metrics including memory usage and processing speeds.
    /// </summary>
    /// <returns>System performance data optimized for ARM32 monitoring</returns>
    /// <response code="200">Performance metrics retrieved successfully</response>
    [HttpGet("performance")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetPerformanceMetrics()
    {
        var memoryUsage = GC.GetTotalMemory(false);
        var workingSet = Environment.WorkingSet;
        
        var performance = new
        {
            Timestamp = DateTime.UtcNow,
            Memory = new
            {
                ManagedMemoryMB = Math.Round(memoryUsage / 1024.0 / 1024.0, 2),
                WorkingSetMB = Math.Round(workingSet / 1024.0 / 1024.0, 2),
                TargetLimitMB = 300, // ARM32 memory constraint
                MemoryPressure = memoryUsage > 250 * 1024 * 1024 ? "High" : "Normal"
            },
            Process = new
            {
                UptimeSeconds = Environment.TickCount64 / 1000,
                ProcessorCount = Environment.ProcessorCount,
                ThreadPoolThreads = ThreadPool.ThreadCount,
                CompletedWorkItems = ThreadPool.CompletedWorkItemCount
            },
            System = new
            {
                OSVersion = Environment.OSVersion.ToString(),
                MachineName = Environment.MachineName,
                Framework = Environment.Version.ToString(),
                Is64BitProcess = Environment.Is64BitProcess
            }
        };

        return Ok(performance);
    }
}