using MediaButler.Core.Common;
using MediaButler.Core.Entities;

namespace MediaButler.Services.Background;

/// <summary>
/// Service interface for coordinating file processing operations across the system.
/// Orchestrates ML classification pipeline with batch processing, prioritization, and resource management.
/// Follows "Simple Made Easy" principles with clear separation between coordination and execution.
/// </summary>
public interface IProcessingCoordinator
{
    /// <summary>
    /// Processes a batch of files through the ML classification pipeline.
    /// </summary>
    /// <param name="files">Files to process in this batch</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing batch processing statistics</returns>
    Task<Result<BatchProcessingResult>> ProcessBatchAsync(
        IEnumerable<TrackedFile> files, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes files with high priority (user-requested operations).
    /// </summary>
    /// <param name="files">Files to process with high priority</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing processing statistics</returns>
    Task<Result<BatchProcessingResult>> ProcessHighPriorityBatchAsync(
        IEnumerable<TrackedFile> files,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current processing coordinator statistics.
    /// </summary>
    /// <returns>Result containing current processing metrics and status</returns>
    Task<Result<ProcessingCoordinatorStats>> GetStatisticsAsync();

    /// <summary>
    /// Starts the processing coordinator background operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the processing coordinator and completes pending operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the coordinator is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event triggered when batch processing starts.
    /// </summary>
    event EventHandler<BatchProcessingStartedEventArgs> BatchProcessingStarted;

    /// <summary>
    /// Event triggered when batch processing completes.
    /// </summary>
    event EventHandler<BatchProcessingCompletedEventArgs> BatchProcessingCompleted;

    /// <summary>
    /// Event triggered when processing progress updates.
    /// </summary>
    event EventHandler<ProcessingProgressEventArgs> ProcessingProgress;
}

/// <summary>
/// Result of batch processing operations.
/// </summary>
public record BatchProcessingResult
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int SuccessfulClassifications { get; init; }
    public int FailedClassifications { get; init; }
    public TimeSpan ProcessingDuration { get; init; }
    public double AverageConfidence { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Processing coordinator statistics and metrics.
/// </summary>
public record ProcessingCoordinatorStats
{
    public int TotalBatchesProcessed { get; init; }
    public int TotalFilesProcessed { get; init; }
    public int CurrentQueueSize { get; init; }
    public int CurrentHighPriorityQueueSize { get; init; }
    public double AverageProcessingTimeMs { get; init; }
    public double SuccessRate { get; init; }
    public DateTime LastProcessingTime { get; init; }
    public bool IsThrottling { get; init; }
    public double MemoryUsageMB { get; init; }
}

/// <summary>
/// Event arguments for batch processing started events.
/// </summary>
public class BatchProcessingStartedEventArgs : EventArgs
{
    public int BatchSize { get; init; }
    public bool IsHighPriority { get; init; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for batch processing completed events.
/// </summary>
public class BatchProcessingCompletedEventArgs : EventArgs
{
    public BatchProcessingResult Result { get; init; } = new();
    public bool IsHighPriority { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for processing progress events.
/// </summary>
public class ProcessingProgressEventArgs : EventArgs
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int CurrentBatchSize { get; init; }
    public string? CurrentOperation { get; init; }
    public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100.0 : 0.0;
}