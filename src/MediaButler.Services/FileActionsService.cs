using Microsoft.Extensions.Logging;
using MediaButler.Core.Common;
using MediaButler.Core.Models;
using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;
using MediaButler.Data.Repositories;
using MediaButler.Services.Interfaces;
using MediaButler.Services.FileOperations;
using MediaButler.Services.Background;
using MediaButler.Services.Extensions;

namespace MediaButler.Services;

/// <summary>
/// Service implementation for file action operations including batch processing.
/// Coordinates between repository layer, background jobs, and path generation services.
/// </summary>
public class FileActionsService : IFileActionsService
{
    private readonly ITrackedFileRepository _fileRepository;
    private readonly IPathGenerationService _pathGenerationService;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly ILogger<FileActionsService> _logger;

    /// <summary>
    /// Initializes a new instance of the FileActionsService.
    /// </summary>
    public FileActionsService(
        ITrackedFileRepository fileRepository,
        IPathGenerationService pathGenerationService,
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<FileActionsService> logger)
    {
        _fileRepository = fileRepository;
        _pathGenerationService = pathGenerationService;
        _backgroundTaskQueue = backgroundTaskQueue;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<BatchJobResponse>> OrganizeBatchAsync(
        BatchOrganizeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting batch organize for {FileCount} files. BatchName: {BatchName}, DryRun: {DryRun}",
                request.Files.Count, request.BatchName ?? "Unnamed", request.DryRun);

            // 1. Extract file hashes and validate request
            var fileHashes = request.Files.Select(f => f.Hash).ToList();

            if (fileHashes.Count == 0)
            {
                return Result<BatchJobResponse>.Failure("No files specified for batch operation");
            }

            if (fileHashes.Count > 1000) // ARM32 optimization
            {
                return Result<BatchJobResponse>.Failure("Maximum 1000 files per batch operation");
            }

            // 2. Batch validate files exist in database
            _logger.LogDebug("Validating {FileCount} files exist in database", fileHashes.Count);
            var existingFiles = await GetFilesByHashesAsync(fileHashes, cancellationToken);

            // 3. Check for missing files
            var missingHashes = fileHashes.Except(existingFiles.Keys).ToList();
            if (missingHashes.Any())
            {
                _logger.LogWarning("Found {MissingCount} missing files: {MissingHashes}",
                    missingHashes.Count, string.Join(", ", missingHashes.Take(5)));

                if (!request.ContinueOnError)
                {
                    return Result<BatchJobResponse>.Failure(
                        $"Missing files (use ContinueOnError=true to skip): {string.Join(", ", missingHashes.Take(10))}");
                }
            }

            // 4. Generate target paths and create operations
            var fileOperations = new List<FileOrganizeOperation>();
            var validationErrors = new List<string>();

            foreach (var fileAction in request.Files.Where(f => existingFiles.ContainsKey(f.Hash)))
            {
                try
                {
                    var file = existingFiles[fileAction.Hash];

                    // Generate target path (use custom path if provided)
                    string targetPath;
                    if (!string.IsNullOrWhiteSpace(fileAction.CustomTargetPath))
                    {
                        targetPath = fileAction.CustomTargetPath;
                    }
                    else
                    {
                        var pathResult = await _pathGenerationService.GenerateTargetPathAsync(
                            file, fileAction.ConfirmedCategory);

                        if (!pathResult.IsSuccess)
                        {
                            validationErrors.Add($"Failed to generate path for {file.FileName}: {pathResult.Error}");
                            continue;
                        }

                        targetPath = pathResult.Value;
                    }

                    var operation = FileOrganizeOperation.Create(file, fileAction.ConfirmedCategory, targetPath);
                    operation.Metadata = fileAction.Metadata;

                    // Validate the operation
                    if (request.ValidateTargetPaths && !operation.Validate())
                    {
                        validationErrors.Add($"Invalid operation for {file.FileName}: {operation.ValidationError}");
                        continue;
                    }

                    fileOperations.Add(operation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error preparing operation for file {Hash}", fileAction.Hash);
                    validationErrors.Add($"Error preparing file {fileAction.Hash}: {ex.Message}");
                }
            }

            // 5. Check if we have any valid operations
            if (fileOperations.Count == 0)
            {
                var errorMessage = validationErrors.Any()
                    ? $"No valid operations created. Errors: {string.Join("; ", validationErrors)}"
                    : "No valid operations created";
                return Result<BatchJobResponse>.Failure(errorMessage);
            }

            // 6. Queue background job using custom task queue
            _logger.LogInformation("Queueing background job for {OperationCount} file operations", fileOperations.Count);

            var jobId = _backgroundTaskQueue.QueueBatchFileProcessing(fileOperations, request);

            // 7. Create response
            var response = new BatchJobResponse
            {
                JobId = jobId,
                Status = "Queued",
                QueuedAt = DateTime.UtcNow,
                TotalFiles = fileOperations.Count,
                ProcessedFiles = 0,
                SuccessfulFiles = 0,
                FailedFiles = 0,
                Metadata = new Dictionary<string, object>
                {
                    ["batchName"] = request.BatchName ?? "Unnamed Batch",
                    ["continueOnError"] = request.ContinueOnError,
                    ["dryRun"] = request.DryRun,
                    ["validateTargetPaths"] = request.ValidateTargetPaths,
                    ["createDirectories"] = request.CreateDirectories,
                    ["missingFiles"] = missingHashes.Count,
                    ["validationErrors"] = validationErrors.Count,
                    ["maxConcurrency"] = request.MaxConcurrency
                }
            };

            if (validationErrors.Any())
            {
                response.Errors.AddRange(validationErrors);
            }

            _logger.LogInformation("Batch job {JobId} queued successfully for {FileCount} files",
                jobId, fileOperations.Count);

            return Result<BatchJobResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error organizing batch");
            return Result<BatchJobResponse>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<BatchJobResponse>> GetBatchStatusAsync(
        string jobId,
        bool includeDetails = false)
    {
        try
        {
            _logger.LogDebug("Retrieving status for batch job {JobId}", jobId);

            // Get job information from custom task queue
            if (_backgroundTaskQueue is BackgroundTaskQueue queue)
            {
                var jobInfo = queue.GetJobInfo(jobId);
                if (jobInfo == null)
                {
                    return Result<BatchJobResponse>.Failure($"Job {jobId} not found");
                }

                // Create response from custom queue job info
                var response = new BatchJobResponse
                {
                    JobId = jobId,
                    Status = MapCustomJobStatus(jobInfo.Status),
                    QueuedAt = jobInfo.QueuedAt,
                    StartedAt = jobInfo.StartedAt,
                    CompletedAt = jobInfo.CompletedAt,
                    TotalFiles = jobInfo.TotalFiles,
                    ProcessedFiles = jobInfo.ProcessedFiles,
                    SuccessfulFiles = jobInfo.SuccessfulFiles,
                    FailedFiles = jobInfo.FailedFiles,
                    // Note: ErrorMessage moved to Errors list for BatchJobResponse
                };

                // Add error message to errors list if present
                if (!string.IsNullOrEmpty(jobInfo.ErrorMessage))
                {
                    response.Errors.Add(jobInfo.ErrorMessage);
                }

                return Result<BatchJobResponse>.Success(response);
            }

            // Fallback if queue is not BackgroundTaskQueue
            return Result<BatchJobResponse>.Failure("Unable to retrieve job status from task queue");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for batch job {JobId}", jobId);
            return Result<BatchJobResponse>.Failure($"Error retrieving job status: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> CancelBatchJobAsync(string jobId)
    {
        try
        {
            _logger.LogInformation("Attempting to cancel batch job {JobId}", jobId);

            // Check if job exists in custom task queue
            if (_backgroundTaskQueue is BackgroundTaskQueue queue)
            {
                var jobInfo = queue.GetJobInfo(jobId);
                if (jobInfo == null)
                {
                    return Result<string>.Failure($"Job {jobId} not found");
                }

                // Check if job can be cancelled
                if (jobInfo.Status == JobStatus.Completed || jobInfo.Status == JobStatus.Failed)
                {
                    return Result<string>.Failure($"Job {jobId} cannot be cancelled as it has already {jobInfo.Status.ToString().ToLower()}");
                }

                // Mark job as cancelled (custom queue implementation would need this method)
                var cancelled = queue.TryCancelJob(jobId);

                if (cancelled)
                {
                    _logger.LogInformation("Batch job {JobId} cancelled successfully", jobId);
                    return Result<string>.Success($"Job {jobId} has been cancelled");
                }
                else
                {
                    _logger.LogWarning("Failed to cancel batch job {JobId}", jobId);
                    return Result<string>.Failure($"Failed to cancel job {jobId}");
                }
            }

            return Result<string>.Failure("Unable to cancel job - task queue does not support cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling batch job {JobId}", jobId);
            return Result<string>.Failure($"Error cancelling job: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<BatchJobResponse>>> GetBatchJobsAsync(
        string? status = null,
        int limit = 50,
        int offset = 0)
    {
        try
        {
            _logger.LogDebug("Retrieving batch jobs: Status={Status}, Limit={Limit}, Offset={Offset}",
                status, limit, offset);

            var jobs = new List<BatchJobResponse>();

            // For now, return a simple implementation noting that full job listing requires more complex setup
            // This can be enhanced later with proper job tracking storage
            _logger.LogInformation("Getting batch jobs - simplified implementation returns empty list for status: {Status}", status);

            // TODO: Implement proper job tracking with a separate storage mechanism
            // The in-memory Hangfire storage doesn't provide easy access to job history

            return Result<IEnumerable<BatchJobResponse>>.Success(jobs.OrderByDescending(j => j.QueuedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch jobs");
            return Result<IEnumerable<BatchJobResponse>>.Failure($"Error retrieving jobs: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<object>> ValidateBatchAsync(
        BatchOrganizeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating batch request for {FileCount} files", request.Files.Count);

            var fileHashes = request.Files.Select(f => f.Hash).ToList();
            var existingFiles = await GetFilesByHashesAsync(fileHashes, cancellationToken);

            var missingFiles = fileHashes.Except(existingFiles.Keys).ToList();
            var validationErrors = new List<string>();
            var estimates = new List<object>();

            // Validate each file operation
            foreach (var fileAction in request.Files)
            {
                if (!existingFiles.ContainsKey(fileAction.Hash))
                {
                    validationErrors.Add($"File not found: {fileAction.Hash}");
                    continue;
                }

                var file = existingFiles[fileAction.Hash];

                try
                {
                    // Validate path generation
                    var pathResult = await _pathGenerationService.GenerateTargetPathAsync(
                        file, fileAction.ConfirmedCategory);

                    if (!pathResult.IsSuccess)
                    {
                        validationErrors.Add($"Path generation failed for {file.FileName}: {pathResult.Error}");
                        continue;
                    }

                    estimates.Add(new
                    {
                        FileHash = fileAction.Hash,
                        FileName = file.FileName,
                        SourcePath = file.OriginalPath,
                        TargetPath = pathResult.Value,
                        Category = fileAction.ConfirmedCategory,
                        FileSizeBytes = file.FileSize,
                        Valid = true
                    });
                }
                catch (Exception ex)
                {
                    validationErrors.Add($"Validation error for {file.FileName}: {ex.Message}");
                }
            }

            var result = new
            {
                IsValid = validationErrors.Count == 0,
                TotalFiles = request.Files.Count,
                ValidFiles = estimates.Count,
                InvalidFiles = validationErrors.Count,
                MissingFiles = missingFiles.Count,
                ValidationErrors = validationErrors,
                MissingFileHashes = missingFiles,
                EstimatedOperations = estimates,
                EstimatedTotalSizeBytes = estimates.Cast<dynamic>().Sum(e => (long)e.FileSizeBytes),
                Recommendations = GenerateRecommendations(request, validationErrors.Count, missingFiles.Count)
            };

            return Result<object>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating batch request");
            return Result<object>.Failure($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to get files by their hashes with error handling.
    /// </summary>
    private async Task<Dictionary<string, Core.Entities.TrackedFile>> GetFilesByHashesAsync(
        List<string> hashes,
        CancellationToken cancellationToken)
    {
        try
        {
            var files = await _fileRepository.FindAsync(
                f => hashes.Contains(f.Hash) && f.IsActive,
                cancellationToken);

            return files.ToDictionary(f => f.Hash, f => f);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving files by hashes");
            throw;
        }
    }


    /// <summary>
    /// Generates recommendations based on validation results.
    /// </summary>
    private static List<string> GenerateRecommendations(
        BatchOrganizeRequest request,
        int errorCount,
        int missingCount)
    {
        var recommendations = new List<string>();

        if (errorCount > 0)
        {
            recommendations.Add("Consider using ContinueOnError=true to process valid files despite errors");
        }

        if (missingCount > 0)
        {
            recommendations.Add("Some files were not found in the database - they may need to be scanned first");
        }

        if (request.Files.Count > 100)
        {
            recommendations.Add("Large batch detected - consider splitting into smaller batches for better performance");
        }

        if (!request.DryRun)
        {
            recommendations.Add("Consider running with DryRun=true first to preview the operations");
        }

        return recommendations;
    }

    /// <summary>
    /// Maps custom job status to API response status.
    /// </summary>
    private static string MapCustomJobStatus(JobStatus status)
    {
        return status switch
        {
            JobStatus.Queued => "Queued",
            JobStatus.Running => "Processing",
            JobStatus.Completed => "Completed",
            JobStatus.Failed => "Failed",
            _ => "Unknown"
        };
    }
}