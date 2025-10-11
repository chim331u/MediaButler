namespace MediaButler.API.Services;

/// <summary>
/// Interface for reporting progress of long-running batch operations.
/// Separates progress tracking concern from job execution logic following "Simple Made Easy" principles.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Reports that a batch job has started.
    /// </summary>
    /// <param name="jobId">Unique identifier for the job</param>
    /// <param name="jobType">Type of the job (e.g., "batch.file.processing")</param>
    /// <param name="totalItems">Total number of items to process</param>
    Task ReportJobStartedAsync(string jobId, string jobType, int totalItems);

    /// <summary>
    /// Reports progress during job execution.
    /// </summary>
    /// <param name="jobId">Unique identifier for the job</param>
    /// <param name="current">Number of items processed so far</param>
    /// <param name="total">Total number of items</param>
    /// <param name="currentItem">Optional name/identifier of the current item being processed</param>
    Task ReportProgressAsync(string jobId, int current, int total, string? currentItem = null);

    /// <summary>
    /// Reports successful completion of a batch job.
    /// </summary>
    /// <param name="jobId">Unique identifier for the job</param>
    /// <param name="jobType">Type of the job</param>
    /// <param name="totalItems">Total number of items processed</param>
    /// <param name="duration">Time taken to complete the job</param>
    Task ReportJobCompletedAsync(string jobId, string jobType, int totalItems, TimeSpan duration);

    /// <summary>
    /// Reports failure of a batch job.
    /// </summary>
    /// <param name="jobId">Unique identifier for the job</param>
    /// <param name="jobType">Type of the job</param>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="itemsProcessed">Number of items successfully processed before failure</param>
    Task ReportJobFailedAsync(string jobId, string jobType, string errorMessage, int itemsProcessed);
}
