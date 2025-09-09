using MediaButler.Core.Entities;

namespace MediaButler.Services.Background;

/// <summary>
/// Defines a queue for file processing operations that supports asynchronous work item management
/// with cancellation support and priority handling.
/// </summary>
public interface IFileProcessingQueue
{
    /// <summary>
    /// Enqueues a file for processing with normal priority.
    /// </summary>
    /// <param name="file">The tracked file to process</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    Task EnqueueAsync(TrackedFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a file for processing with high priority (user-requested).
    /// </summary>
    /// <param name="file">The tracked file to process</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    Task EnqueueHighPriorityAsync(TrackedFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next file for processing, waiting if no items are available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The next tracked file to process, or null if cancellation was requested</returns>
    Task<TrackedFile?> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current count of items in the processing queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the current count of high priority items in the processing queue.
    /// </summary>
    int HighPriorityCount { get; }

    /// <summary>
    /// Clears all items from the processing queue.
    /// </summary>
    void Clear();
}