using Microsoft.AspNetCore.Mvc;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for system status and resource monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly ILogger<SystemController> _logger;

    public SystemController(ILogger<SystemController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current storage status
    /// </summary>
    /// <returns>Storage information including usage and capacity</returns>
    [HttpGet("storage")]
    public async Task<ActionResult<StorageStatus>> GetStorageStatus()
    {
        try
        {
            // Get real disk usage for the current drive
            var currentDrive = new DriveInfo(Directory.GetCurrentDirectory());
            var totalBytes = currentDrive.TotalSize;
            var availableBytes = currentDrive.AvailableFreeSpace;
            var usedBytes = totalBytes - availableBytes;

            var status = new StorageStatus
            {
                UsedGB = Math.Round(usedBytes / (1024.0 * 1024.0 * 1024.0), 2),
                TotalGB = Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 2),
                FilesTotalSizeGB = 0.5 // Mock value - would need to calculate from actual files
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving storage status");
            return StatusCode(500, new { error = $"Failed to retrieve storage status: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets the current memory usage
    /// </summary>
    /// <returns>Memory usage information</returns>
    [HttpGet("memory")]
    public async Task<ActionResult<MemoryInfo>> GetMemoryInfo()
    {
        try
        {
            // Get current process memory usage
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryUsageMB = (int)(process.WorkingSet64 / (1024 * 1024));

            var memoryInfo = new MemoryInfo
            {
                UsageMB = memoryUsageMB
            };

            return Ok(memoryInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving memory information");
            return StatusCode(500, new { error = $"Failed to retrieve memory info: {ex.Message}" });
        }
    }
}

/// <summary>
/// Storage status information
/// </summary>
public record StorageStatus
{
    public double UsedGB { get; init; }
    public double TotalGB { get; init; }
    public double FilesTotalSizeGB { get; init; }
}

/// <summary>
/// Memory usage information
/// </summary>
public record MemoryInfo
{
    public int UsageMB { get; init; }
}