using MediaButler.Web.Models;

namespace MediaButler.Web.Services.System;

/// <summary>
/// Service for retrieving comprehensive system status information
/// </summary>
public interface ISystemStatusService
{
    /// <summary>
    /// Gets comprehensive system status including files, health, processing, and storage metrics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>System status information</returns>
    Task<SystemStatusModel> GetSystemStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets system health check result
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>True if system is healthy, false otherwise</returns>
    Task<bool> IsSystemHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current processing queue status
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Queue status information</returns>
    Task<ProcessingQueueStatus> GetProcessingQueueStatusAsync(CancellationToken cancellationToken = default);
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