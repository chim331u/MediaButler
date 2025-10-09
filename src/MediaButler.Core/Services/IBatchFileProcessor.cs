using MediaButler.Core.Models;

namespace MediaButler.Core.Services;

/// <summary>
/// Interface for batch file processing operations.
/// Implemented by Hangfire background jobs in MediaButler.Batch project.
/// </summary>
public interface IBatchFileProcessor
{
    /// <summary>
    /// Processes a batch of file organization operations.
    /// </summary>
    /// <param name="operations">List of file operations to process</param>
    /// <param name="batchName">Human-readable name for the batch</param>
    /// <param name="jobId">Unique identifier for this job</param>
    /// <param name="continueOnError">Whether to continue processing if individual files fail</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="context">Hangfire perform context (optional, injected by Hangfire)</param>
    Task ProcessBatchAsync(
        List<FileOrganizeOperation> operations,
        string batchName,
        string jobId,
        bool continueOnError,
        CancellationToken cancellationToken);
}
