using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaButler.Services.Background;

/// <summary>
/// Interface for queuing background tasks without external dependencies.
/// Provides lightweight alternative to Hangfire for ARM32 optimization.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Queues a background work item for execution.
    /// </summary>
    /// <param name="workItem">The work item to execute</param>
    /// <param name="jobId">Unique identifier for tracking the job</param>
    /// <param name="jobName">Human-readable name for the job</param>
    void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem, string jobId, string jobName);

    /// <summary>
    /// Dequeues a background work item for execution.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Work item with job information</returns>
    Task<QueuedWorkItem> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current queue status.
    /// </summary>
    QueueStatus GetQueueStatus();
}

/// <summary>
/// Represents a queued work item with job tracking information.
/// </summary>
public record QueuedWorkItem
{
    public required string JobId { get; init; }
    public required string JobName { get; init; }
    public required Func<IServiceProvider, CancellationToken, Task> WorkItem { get; init; }
    public DateTime QueuedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Current status of the background task queue.
/// </summary>
public record QueueStatus
{
    public int QueuedJobs { get; init; }
    public int ActiveJobs { get; init; }
    public int CompletedJobs { get; init; }
    public int FailedJobs { get; init; }
    public DateTime LastActivity { get; init; }
}