using Microsoft.AspNetCore.Mvc;
using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;
using MediaButler.Services.Interfaces;
using MediaButler.API.Filters;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for file action operations including batch file organization.
/// Provides endpoints for queuing background jobs and tracking their progress.
/// </summary>
[ApiController]
[Route("api/v1/file-actions")]
[Produces("application/json")]
public class FileActionsController : ControllerBase
{
    private readonly IFileActionsService _fileActionsService;
    private readonly IFileService _fileService;
    private readonly ILogger<FileActionsController> _logger;

    /// <summary>
    /// Initializes a new instance of the FileActionsController.
    /// </summary>
    /// <param name="fileActionsService">Service for handling file actions</param>
    /// <param name="fileService">Service for handling individual file operations</param>
    /// <param name="logger">Logger instance</param>
    public FileActionsController(
        IFileActionsService fileActionsService,
        IFileService fileService,
        ILogger<FileActionsController> logger)
    {
        _fileActionsService = fileActionsService;
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Organizes multiple files in a background batch operation.
    /// Files are processed according to their confirmed categories and moved to appropriate locations.
    /// </summary>
    /// <param name="request">The batch organization request containing files and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the job ID and initial status</returns>
    /// <response code="200">Batch job successfully queued</response>
    /// <response code="400">Invalid request data or validation errors</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("organize-batch")]
    [ModelValidationFilter]
    [ProducesResponseType(typeof(BatchJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> OrganizeBatchAsync(
        [FromBody] BatchOrganizeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received batch organize request for {FileCount} files. BatchName: {BatchName}, DryRun: {DryRun}",
                request.Files.Count, request.BatchName ?? "Unnamed", request.DryRun);

            var result = await _fileActionsService.OrganizeBatchAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Batch job {JobId} successfully queued for {FileCount} files",
                    result.Value.JobId, result.Value.TotalFiles);

                return Ok(result.Value);
            }

            _logger.LogWarning("Batch organize request failed: {Error}", result.Error);
            return BadRequest(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing batch organize request");
            return StatusCode(500, "An unexpected error occurred while processing the request");
        }
    }

    /// <summary>
    /// Gets the current status of a batch job.
    /// Provides detailed progress information and results if available.
    /// </summary>
    /// <param name="jobId">The unique identifier of the batch job</param>
    /// <param name="includeDetails">Whether to include detailed results for individual files</param>
    /// <returns>The current status and progress of the batch job</returns>
    /// <response code="200">Job status successfully retrieved</response>
    /// <response code="404">Job not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("batch-status/{jobId}")]
    [ProducesResponseType(typeof(BatchJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBatchStatusAsync(
        string jobId,
        [FromQuery] bool includeDetails = false)
    {
        try
        {
            _logger.LogDebug("Retrieving status for batch job {JobId}, IncludeDetails: {IncludeDetails}",
                jobId, includeDetails);

            var result = await _fileActionsService.GetBatchStatusAsync(jobId, includeDetails);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            _logger.LogWarning("Batch job {JobId} not found: {Error}", jobId, result.Error);
            return NotFound(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving status for batch job {JobId}", jobId);
            return StatusCode(500, "An unexpected error occurred while retrieving job status");
        }
    }

    /// <summary>
    /// Cancels a running or queued batch job.
    /// Files that have already been processed will remain in their new locations.
    /// </summary>
    /// <param name="jobId">The unique identifier of the batch job to cancel</param>
    /// <returns>Confirmation of cancellation request</returns>
    /// <response code="200">Job cancellation requested successfully</response>
    /// <response code="404">Job not found</response>
    /// <response code="409">Job cannot be cancelled (already completed)</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("batch-cancel/{jobId}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelBatchJobAsync(string jobId)
    {
        try
        {
            _logger.LogInformation("Cancellation requested for batch job {JobId}", jobId);

            var result = await _fileActionsService.CancelBatchJobAsync(jobId);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Batch job {JobId} cancellation requested successfully", jobId);
                return Ok(result.Value);
            }

            _logger.LogWarning("Failed to cancel batch job {JobId}: {Error}", jobId, result.Error);

            // Determine appropriate HTTP status based on error type
            if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(result.Error);
            if (result.Error.Contains("cannot be cancelled", StringComparison.OrdinalIgnoreCase))
                return Conflict(result.Error);

            return BadRequest(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error cancelling batch job {JobId}", jobId);
            return StatusCode(500, "An unexpected error occurred while cancelling the job");
        }
    }

    /// <summary>
    /// Gets a list of recent batch jobs for monitoring and management.
    /// Supports pagination and filtering by status.
    /// </summary>
    /// <param name="status">Filter by job status (optional)</param>
    /// <param name="limit">Maximum number of jobs to return (default: 50, max: 200)</param>
    /// <param name="offset">Number of jobs to skip for pagination (default: 0)</param>
    /// <returns>A list of batch jobs with summary information</returns>
    /// <response code="200">Job list successfully retrieved</response>
    /// <response code="400">Invalid pagination parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("batch-jobs")]
    [ProducesResponseType(typeof(IEnumerable<BatchJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBatchJobsAsync(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            if (limit <= 0 || limit > 200)
            {
                return BadRequest("Limit must be between 1 and 200");
            }

            if (offset < 0)
            {
                return BadRequest("Offset must be non-negative");
            }

            _logger.LogDebug("Retrieving batch jobs: Status={Status}, Limit={Limit}, Offset={Offset}",
                status, limit, offset);

            var result = await _fileActionsService.GetBatchJobsAsync(status, limit, offset);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            _logger.LogWarning("Failed to retrieve batch jobs: {Error}", result.Error);
            return BadRequest(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving batch jobs");
            return StatusCode(500, "An unexpected error occurred while retrieving batch jobs");
        }
    }

    /// <summary>
    /// Validates a batch organize request without actually executing it.
    /// Useful for pre-flight validation and cost estimation.
    /// </summary>
    /// <param name="request">The batch organization request to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation results and estimated processing information</returns>
    /// <response code="200">Validation completed successfully</response>
    /// <response code="400">Validation failed or invalid request</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("validate-batch")]
    [ModelValidationFilter]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateBatchAsync(
        [FromBody] BatchOrganizeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating batch request for {FileCount} files", request.Files.Count);

            var result = await _fileActionsService.ValidateBatchAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            _logger.LogWarning("Batch validation failed: {Error}", result.Error);
            return BadRequest(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating batch request");
            return StatusCode(500, "An unexpected error occurred while validating the request");
        }
    }

    /// <summary>
    /// Marks a file as ignored, preventing it from being processed further.
    /// This is a terminal state operation that cannot be undone through the API.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file to ignore</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated file information</returns>
    /// <response code="200">File successfully marked as ignored</response>
    /// <response code="400">Invalid hash or file cannot be ignored</response>
    /// <response code="404">File not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("ignore/{hash}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> IgnoreFileAsync(
        string hash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return BadRequest("Hash parameter cannot be empty");
            }

            _logger.LogInformation("Received request to ignore file with hash: {Hash}", hash);

            var result = await _fileService.IgnoreFileAsync(hash, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully marked file {Hash} as ignored", hash);
                return Ok(new
                {
                    message = "File successfully marked as ignored",
                    hash = result.Value.Hash,
                    fileName = result.Value.FileName,
                    status = result.Value.Status.ToString(),
                    updatedAt = result.Value.LastUpdateDate
                });
            }

            _logger.LogWarning("Failed to ignore file {Hash}: {Error}", hash, result.Error);

            // Determine appropriate HTTP status based on error type
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(result.Error);

            return BadRequest(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error ignoring file with hash {Hash}", hash);
            return StatusCode(500, "An unexpected error occurred while ignoring the file");
        }
    }
}