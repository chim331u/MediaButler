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
            var customJobId = Guid.NewGuid().ToString("N")[..12]; // 12-character job ID for our tracking

            // Enqueue job to Hangfire (will be picked up by MediaButler.Batch worker)
            // We use Hangfire's expression-based API which will serialize the method call
            var hangfireJobId = _backgroundJobClient.Enqueue<IBatchFileProcessor>(
                processor => processor.ProcessBatchAsync(
                    fileOperations,
                    batchName,
                    customJobId,
                    request.ContinueOnError,
                    CancellationToken.None));

            // 7. Create response
            var response = new BatchJobResponse
            {
                JobId = customJobId,
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
                customJobId, fileOperations.Count);

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
        // TODO: Implement Hangfire monitoring API in Step 3.3
        _logger.LogWarning("GetBatchStatusAsync not yet implemented for Hangfire jobs. JobId: {JobId}", jobId);
        await Task.CompletedTask; // Remove warning

        return Result<BatchJobResponse>.Failure(
            "Job status tracking will be implemented in Step 3.3 using Hangfire monitoring API");
    }

    /// <inheritdoc />
    public async Task<Result<string>> CancelBatchJobAsync(string jobId)
    {
        // TODO: Implement Hangfire job cancellation in Step 3.3
        _logger.LogWarning("CancelBatchJobAsync not yet implemented for Hangfire jobs. JobId: {JobId}", jobId);
        await Task.CompletedTask; // Remove warning

        return Result<string>.Failure(
            "Job cancellation will be implemented in Step 3.3 using Hangfire BackgroundJob.Delete");
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

}