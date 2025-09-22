using MediaButler.Core.Common;
using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Service interface for file action operations including batch processing.
/// Provides methods for organizing multiple files and tracking job progress.
/// </summary>
public interface IFileActionsService
{
    /// <summary>
    /// Organizes multiple files in a background batch operation.
    /// Validates the request, queues a background job, and returns tracking information.
    /// </summary>
    /// <param name="request">The batch organization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A result containing the batch job response or error information</returns>
    Task<Result<BatchJobResponse>> OrganizeBatchAsync(
        BatchOrganizeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current status and progress of a batch job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the batch job</param>
    /// <param name="includeDetails">Whether to include detailed results for individual files</param>
    /// <returns>A result containing the current job status or error information</returns>
    Task<Result<BatchJobResponse>> GetBatchStatusAsync(
        string jobId,
        bool includeDetails = false);

    /// <summary>
    /// Cancels a running or queued batch job.
    /// Files already processed will remain in their new locations.
    /// </summary>
    /// <param name="jobId">The unique identifier of the batch job to cancel</param>
    /// <returns>A result indicating success or failure of the cancellation request</returns>
    Task<Result<string>> CancelBatchJobAsync(string jobId);

    /// <summary>
    /// Retrieves a list of batch jobs with optional filtering and pagination.
    /// </summary>
    /// <param name="status">Optional status filter</param>
    /// <param name="limit">Maximum number of jobs to return</param>
    /// <param name="offset">Number of jobs to skip for pagination</param>
    /// <returns>A result containing the list of batch jobs or error information</returns>
    Task<Result<IEnumerable<BatchJobResponse>>> GetBatchJobsAsync(
        string? status = null,
        int limit = 50,
        int offset = 0);

    /// <summary>
    /// Validates a batch organize request without executing it.
    /// Useful for pre-flight validation and cost estimation.
    /// </summary>
    /// <param name="request">The batch organization request to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A result containing validation results and estimates</returns>
    Task<Result<object>> ValidateBatchAsync(
        BatchOrganizeRequest request,
        CancellationToken cancellationToken = default);
}