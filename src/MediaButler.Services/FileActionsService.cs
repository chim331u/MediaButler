using Microsoft.Extensions.Logging;
using MediaButler.Core.Common;
using MediaButler.Core.Models;
using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;
using MediaButler.Core.Services;
using MediaButler.Data.Repositories;
using MediaButler.Services.Interfaces;
using MediaButler.Services.FileOperations;
using MediaButler.Services.Background;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Hangfire.Common;
using Hangfire.States;

namespace MediaButler.Services;

/// <summary>
/// Service implementation for file action operations including batch processing.
/// Coordinates between repository layer, background jobs, and path generation services.
/// </summary>
public class FileActionsService : IFileActionsService
{
    private readonly ITrackedFileRepository _fileRepository;
    private readonly IPathGenerationService _pathGenerationService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<FileActionsService> _logger;

    /// <summary>
    /// Initializes a new instance of the FileActionsService.
    /// </summary>
    public FileActionsService(
        ITrackedFileRepository fileRepository,
        IPathGenerationService pathGenerationService,
        IBackgroundJobClient backgroundJobClient,
        ILogger<FileActionsService> logger)
    {
        _fileRepository = fileRepository;
        _pathGenerationService = pathGenerationService;
        _backgroundJobClient = backgroundJobClient;
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

            // 6. Enqueue Hangfire background job
            _logger.LogInformation("Enqueueing Hangfire job for {OperationCount} file operations", fileOperations.Count);

            var batchName = request.BatchName ?? $"Batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            // Enqueue job to Hangfire using runtime type resolution to avoid circular dependency
            // Load the concrete BatchFileProcessingJob type at runtime
            var jobType = Type.GetType("MediaButler.Batch.Jobs.Batch.BatchFileProcessingJob, MediaButler.Batch")
                ?? throw new InvalidOperationException("Failed to load BatchFileProcessingJob type");

            var method = jobType.GetMethod("ProcessBatchAsync")
                ?? throw new InvalidOperationException("Failed to find ProcessBatchAsync method");

            // Create Hangfire Job instance with concrete type
            var job = new Job(jobType, method, fileOperations, batchName, batchName, request.ContinueOnError, CancellationToken.None);

            // Enqueue the job
            var hangfireJobId = _backgroundJobClient.Create(job, new EnqueuedState());

            // 7. Create response
            var response = new BatchJobResponse
            {
                JobId = hangfireJobId, // Use Hangfire's job ID for tracking
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
                    ["maxConcurrency"] = request.MaxConcurrency ?? 1
                }
            };

            if (validationErrors.Any())
            {
                response.Errors.AddRange(validationErrors);
            }

            _logger.LogInformation("Batch job {JobId} queued successfully for {FileCount} files",
                hangfireJobId, fileOperations.Count);

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
            _logger.LogDebug("Retrieving status for Hangfire job {JobId}", jobId);

            // Get Hangfire monitoring API
            using var connection = JobStorage.Current.GetConnection();
            var monitoringApi = JobStorage.Current.GetMonitoringApi();

            // Get job data
            var jobData = connection.GetJobData(jobId);
            if (jobData == null)
            {
                return Result<BatchJobResponse>.Failure($"Job {jobId} not found");
            }

            // Map Hangfire state to our status
            var status = MapHangfireState(jobData.State);

            // Get job details if available
            var response = new BatchJobResponse
            {
                JobId = jobId,
                Status = status,
                QueuedAt = jobData.CreatedAt,
                TotalFiles = 0,
                ProcessedFiles = 0,
                SuccessfulFiles = 0,
                FailedFiles = 0,
                Metadata = new Dictionary<string, object>()
            };

            // Extract metadata from job arguments if available
            if (jobData.Job?.Args != null && jobData.Job.Args.Count >= 2)
            {
                // Args: [0] = operations list, [1] = batchName, [2] = jobId, [3] = continueOnError
                if (jobData.Job.Args[0] is List<FileOrganizeOperation> operations)
                {
                    response.TotalFiles = operations.Count;
                }

                if (jobData.Job.Args[1] is string batchName)
                {
                    response.Metadata["batchName"] = batchName;
                }

                if (jobData.Job.Args.Count > 3 && jobData.Job.Args[3] is bool continueOnError)
                {
                    response.Metadata["continueOnError"] = continueOnError;
                }
            }

            // Add state history if includeDetails (using monitoring API)
            if (includeDetails)
            {
                var jobDetails = monitoringApi.JobDetails(jobId);
                if (jobDetails != null && jobDetails.History != null)
                {
                    response.Metadata["stateHistory"] = jobDetails.History.Select(s => new
                    {
                        s.StateName,
                        s.CreatedAt,
                        s.Reason,
                        Data = s.Data
                    }).ToList();
                }
            }

            // Add exception information if failed
            if (status == "Failed" && jobData.State == "Failed")
            {
                var jobDetails = monitoringApi.JobDetails(jobId);
                if (jobDetails?.History != null)
                {
                    var failedState = jobDetails.History.FirstOrDefault(s => s.StateName == "Failed");
                    if (failedState?.Data != null && failedState.Data.TryGetValue("ExceptionMessage", out var exceptionMessage))
                    {
                        response.Errors.Add(exceptionMessage);
                    }
                }
            }

            await Task.CompletedTask; // Satisfy async signature
            return Result<BatchJobResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch job status for {JobId}", jobId);
            return Result<BatchJobResponse>.Failure($"Error retrieving job status: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> CancelBatchJobAsync(string jobId)
    {
        try
        {
            _logger.LogInformation("Attempting to cancel Hangfire job {JobId}", jobId);

            // Check if job exists
            using var connection = JobStorage.Current.GetConnection();
            var jobData = connection.GetJobData(jobId);

            if (jobData == null)
            {
                return Result<string>.Failure($"Job {jobId} not found");
            }

            // Check if job is already in a terminal state
            if (jobData.State == "Succeeded" || jobData.State == "Deleted")
            {
                return Result<string>.Failure($"Job {jobId} is already {jobData.State.ToLower()} and cannot be cancelled");
            }

            // Delete the job (this will cancel it if running or remove it if queued)
            var deleted = BackgroundJob.Delete(jobId);

            if (deleted)
            {
                _logger.LogInformation("Successfully cancelled job {JobId}", jobId);
                await Task.CompletedTask; // Satisfy async signature
                return Result<string>.Success($"Job {jobId} has been cancelled");
            }
            else
            {
                _logger.LogWarning("Failed to cancel job {JobId}", jobId);
                return Result<string>.Failure($"Failed to cancel job {jobId}");
            }
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
            var monitoringApi = JobStorage.Current.GetMonitoringApi();

            // Retrieve jobs based on status filter
            if (string.IsNullOrEmpty(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // Get jobs from all states
                var enqueuedJobs = monitoringApi.EnqueuedJobs("default", offset, limit);
                var processingJobs = monitoringApi.ProcessingJobs(offset, limit);
                var succeededJobs = monitoringApi.SucceededJobs(offset, limit);
                var failedJobs = monitoringApi.FailedJobs(offset, limit);

                jobs.AddRange(enqueuedJobs.Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.EnqueuedAt, "Queued")));
                jobs.AddRange(processingJobs.Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.StartedAt, "Processing")));
                jobs.AddRange(succeededJobs.Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.SucceededAt, "Completed")));
                jobs.AddRange(failedJobs.Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.FailedAt, "Failed")));
            }
            else
            {
                // Get jobs for specific status
                var hangfireState = MapStatusToHangfireState(status);
                var stateJobs = hangfireState switch
                {
                    "Enqueued" => monitoringApi.EnqueuedJobs("default", offset, limit)
                        .Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.EnqueuedAt, "Queued")),
                    "Processing" => monitoringApi.ProcessingJobs(offset, limit)
                        .Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.StartedAt, "Processing")),
                    "Succeeded" => monitoringApi.SucceededJobs(offset, limit)
                        .Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.SucceededAt, "Completed")),
                    "Failed" => monitoringApi.FailedJobs(offset, limit)
                        .Select(j => CreateJobResponse(j.Key, j.Value.Job, j.Value.FailedAt, "Failed")),
                    _ => Enumerable.Empty<BatchJobResponse>()
                };

                jobs.AddRange(stateJobs);
            }

            await Task.CompletedTask; // Satisfy async signature
            return Result<IEnumerable<BatchJobResponse>>.Success(
                jobs.OrderByDescending(j => j.QueuedAt).Take(limit));
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
    /// Maps Hangfire job state to our status string.
    /// </summary>
    private static string MapHangfireState(string? hangfireState)
    {
        return hangfireState switch
        {
            "Enqueued" => "Queued",
            "Processing" => "Processing",
            "Succeeded" => "Completed",
            "Failed" => "Failed",
            "Deleted" => "Cancelled",
            "Scheduled" => "Scheduled",
            "Awaiting" => "Waiting",
            null => "Unknown",
            _ => hangfireState
        };
    }

    /// <summary>
    /// Maps our status string to Hangfire job state.
    /// </summary>
    private static string MapStatusToHangfireState(string status)
    {
        return status.ToLower() switch
        {
            "queued" => "Enqueued",
            "processing" => "Processing",
            "completed" => "Succeeded",
            "failed" => "Failed",
            "cancelled" => "Deleted",
            "scheduled" => "Scheduled",
            _ => status
        };
    }

    /// <summary>
    /// Creates a BatchJobResponse from Hangfire job data.
    /// </summary>
    private static BatchJobResponse CreateJobResponse(string jobId, Job? job, DateTime? timestamp, string status)
    {
        var response = new BatchJobResponse
        {
            JobId = jobId,
            Status = status,
            QueuedAt = timestamp ?? DateTime.UtcNow,
            TotalFiles = 0,
            ProcessedFiles = 0,
            SuccessfulFiles = 0,
            FailedFiles = 0,
            Metadata = new Dictionary<string, object>()
        };

        // Extract batch name from job if available
        if (job?.Args != null && job.Args.Count >= 2)
        {
            if (job.Args[0] is List<FileOrganizeOperation> operations)
            {
                response.TotalFiles = operations.Count;
            }

            if (job.Args[1] is string batchName)
            {
                response.Metadata["batchName"] = batchName;
            }
        }

        return response;
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

}