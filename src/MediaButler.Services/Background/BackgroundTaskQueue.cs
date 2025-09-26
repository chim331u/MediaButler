using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Lightweight in-memory background task queue implementation.
/// Optimized for ARM32 with minimal memory footprint and simple job tracking.
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<QueuedWorkItem> _queue;
    private readonly ILogger<BackgroundTaskQueue> _logger;

    // Job tracking for status monitoring
    private readonly ConcurrentDictionary<string, BackgroundJobInfo> _jobs = new();

    // Queue statistics
    private int _queuedJobs;
    private int _activeJobs;
    private int _completedJobs;
    private int _failedJobs;
    private DateTime _lastActivity = DateTime.UtcNow;

    public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger, int capacity = 100)
    {
        _logger = logger;

        // Create bounded channel for ARM32 memory management
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false, // Allow multiple workers if needed
            SingleWriter = false  // Allow multiple producers
        };

        _queue = Channel.CreateBounded<QueuedWorkItem>(options);
    }

    public void QueueBackgroundWorkItem(
        Func<IServiceProvider, CancellationToken, Task> workItem,
        string jobId,
        string jobName)
    {
        if (workItem == null)
            throw new ArgumentNullException(nameof(workItem));

        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("Job ID cannot be null or empty", nameof(jobId));

        var queuedItem = new QueuedWorkItem
        {
            JobId = jobId,
            JobName = jobName ?? "Unnamed Job",
            WorkItem = workItem,
            QueuedAt = DateTime.UtcNow
        };

        // Add job tracking
        var jobInfo = new BackgroundJobInfo
        {
            JobId = jobId,
            JobName = queuedItem.JobName,
            Status = JobStatus.Queued,
            QueuedAt = queuedItem.QueuedAt
        };

        _jobs[jobId] = jobInfo;

        if (!_queue.Writer.TryWrite(queuedItem))
        {
            _logger.LogWarning("Failed to queue background job {JobId} - queue may be full", jobId);
            jobInfo.Status = JobStatus.Failed;
            jobInfo.ErrorMessage = "Queue full";
            jobInfo.CompletedAt = DateTime.UtcNow;
            throw new InvalidOperationException("Background task queue is full");
        }

        Interlocked.Increment(ref _queuedJobs);
        _lastActivity = DateTime.UtcNow;

        _logger.LogInformation("Queued background job {JobId}: {JobName}", jobId, queuedItem.JobName);
    }

    public async Task<QueuedWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);

        // Update job status to running
        if (_jobs.TryGetValue(workItem.JobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Running;
            jobInfo.StartedAt = DateTime.UtcNow;
        }

        Interlocked.Decrement(ref _queuedJobs);
        Interlocked.Increment(ref _activeJobs);
        _lastActivity = DateTime.UtcNow;

        _logger.LogDebug("Dequeued background job {JobId} for processing", workItem.JobId);

        return workItem;
    }

    public QueueStatus GetQueueStatus()
    {
        return new QueueStatus
        {
            QueuedJobs = _queuedJobs,
            ActiveJobs = _activeJobs,
            CompletedJobs = _completedJobs,
            FailedJobs = _failedJobs,
            LastActivity = _lastActivity
        };
    }

    /// <summary>
    /// Marks a job as completed successfully.
    /// </summary>
    public void MarkJobCompleted(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Completed;
            jobInfo.CompletedAt = DateTime.UtcNow;
        }

        Interlocked.Decrement(ref _activeJobs);
        Interlocked.Increment(ref _completedJobs);
        _lastActivity = DateTime.UtcNow;

        _logger.LogInformation("Background job {JobId} completed successfully", jobId);
    }

    /// <summary>
    /// Marks a job as failed with error information.
    /// </summary>
    public void MarkJobFailed(string jobId, string errorMessage)
    {
        if (_jobs.TryGetValue(jobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Failed;
            jobInfo.ErrorMessage = errorMessage;
            jobInfo.CompletedAt = DateTime.UtcNow;
        }

        Interlocked.Decrement(ref _activeJobs);
        Interlocked.Increment(ref _failedJobs);
        _lastActivity = DateTime.UtcNow;

        _logger.LogError("Background job {JobId} failed: {ErrorMessage}", jobId, errorMessage);
    }

    /// <summary>
    /// Gets information about a specific job.
    /// </summary>
    public BackgroundJobInfo? GetJobInfo(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var jobInfo) ? jobInfo : null;
    }

    /// <summary>
    /// Attempts to cancel a queued or running job.
    /// </summary>
    public bool TryCancelJob(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var jobInfo))
        {
            // Only allow cancellation if job is queued or running
            if (jobInfo.Status == JobStatus.Queued || jobInfo.Status == JobStatus.Running)
            {
                jobInfo.Status = JobStatus.Failed;
                jobInfo.CompletedAt = DateTime.UtcNow;
                jobInfo.ErrorMessage = "Job was cancelled by user request";

                // Update counters
                if (jobInfo.Status == JobStatus.Running)
                {
                    Interlocked.Decrement(ref _activeJobs);
                }
                else
                {
                    Interlocked.Decrement(ref _queuedJobs);
                }
                Interlocked.Increment(ref _failedJobs);

                _logger.LogInformation("Background job {JobId} was cancelled", jobId);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Cleans up old job tracking data to prevent memory leaks.
    /// Keeps jobs for 24 hours after completion.
    /// </summary>
    public void CleanupOldJobs()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var jobsToRemove = new List<string>();

        foreach (var kvp in _jobs)
        {
            var job = kvp.Value;
            if (job.CompletedAt.HasValue && job.CompletedAt.Value < cutoff)
            {
                jobsToRemove.Add(kvp.Key);
            }
        }

        foreach (var jobId in jobsToRemove)
        {
            _jobs.TryRemove(jobId, out _);
        }

        if (jobsToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old job records", jobsToRemove.Count);
        }
    }
}

/// <summary>
/// Information about a background job for tracking and monitoring.
/// </summary>
public class BackgroundJobInfo
{
    public required string JobId { get; set; }
    public required string JobName { get; set; }
    public JobStatus Status { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Job metadata for tracking file processing
    public Dictionary<string, object> Metadata { get; set; } = new();

    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    // Helper properties for batch job tracking
    public int TotalFiles => GetMetadataValue<int>("totalFiles");
    public int ProcessedFiles => GetMetadataValue<int>("processedFiles");
    public int SuccessfulFiles => GetMetadataValue<int>("successfulFiles");
    public int FailedFiles => GetMetadataValue<int>("failedFiles");

    private T GetMetadataValue<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    public void UpdateFileCount(string key, int value)
    {
        Metadata[key] = value;
    }
}

/// <summary>
/// Status of a background job.
/// </summary>
public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}