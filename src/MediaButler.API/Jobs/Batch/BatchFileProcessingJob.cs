using Hangfire;
using Hangfire.Server;
using MediaButler.API.Services;
using MediaButler.Core.Models;
using MediaButler.Core.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MediaButler.API.Jobs.Batch;

/// <summary>
/// Hangfire job for processing batch file organization operations.
/// Simplified following "Simple Made Easy" principles - delegates progress reporting and throttling to dedicated services.
/// Focuses solely on orchestrating file organization operations.
/// </summary>
[Queue("default")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
public class BatchFileProcessingJob : IBatchFileProcessor
{
    private readonly IFileOrganizationService _fileOrganizationService;
    private readonly IProgressReporter _progressReporter;
    private readonly IBatchThrottler _throttler;
    private readonly ILogger<BatchFileProcessingJob> _logger;

    public BatchFileProcessingJob(
        IFileOrganizationService fileOrganizationService,
        IProgressReporter progressReporter,
        IBatchThrottler throttler,
        ILogger<BatchFileProcessingJob> logger)
    {
        _fileOrganizationService = fileOrganizationService ?? throw new ArgumentNullException(nameof(fileOrganizationService));
        _progressReporter = progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
        _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a batch of file organization operations.
    /// Delegates progress reporting and throttling to specialized services.
    /// </summary>
    /// <param name="operations">List of file operations to process</param>
    /// <param name="batchName">Human-readable name for the batch</param>
    /// <param name="jobId">Unique identifier for this job</param>
    /// <param name="continueOnError">Whether to continue processing if individual files fail</param>
    /// <param name="cancellationToken">Cancellation token from Hangfire</param>
    [JobDisplayName("Batch File Processing: {1}")]
    public async Task ProcessBatchAsync(
        List<FileOrganizeOperation> operations,
        string batchName,
        string jobId,
        bool continueOnError,
        CancellationToken cancellationToken)
    {
        const string JobType = "batch.file.processing";
        var stopwatch = Stopwatch.StartNew();
        var totalFiles = operations.Count;
        var processedCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var errors = new List<string>();

        _logger.LogInformation(
            "Starting batch file processing job {JobId}: {BatchName} with {TotalFiles} files",
            jobId, batchName, totalFiles);

        try
        {
            // Report job started
            await _progressReporter.ReportJobStartedAsync(jobId, JobType, totalFiles);

            foreach (var operation in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _logger.LogDebug(
                        "Processing file {Current}/{Total}: {FileName}",
                        processedCount + 1, totalFiles, operation.TrackedFile.FileName);

                    // Execute file organization
                    var result = await _fileOrganizationService.OrganizeFileAsync(
                        operation.TrackedFile.Hash,
                        operation.ConfirmedCategory);

                    if (result.IsSuccess)
                    {
                        successCount++;
                        _logger.LogDebug(
                            "Successfully organized file: {FileName} -> {Category}",
                            operation.TrackedFile.FileName, operation.ConfirmedCategory);
                    }
                    else
                    {
                        failedCount++;
                        var error = $"{operation.TrackedFile.FileName}: {result.Error}";
                        errors.Add(error);
                        _logger.LogWarning("Failed to organize file: {Error}", error);

                        if (!continueOnError)
                        {
                            throw new InvalidOperationException($"File processing failed: {result.Error}");
                        }
                    }
                }
                catch (Exception ex) when (continueOnError)
                {
                    failedCount++;
                    var error = $"{operation.TrackedFile.FileName}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, "Error processing file: {FileName}", operation.TrackedFile.FileName);
                }

                processedCount++;

                // Report progress after every file
                await _progressReporter.ReportProgressAsync(
                    jobId,
                    processedCount,
                    totalFiles,
                    operation.TrackedFile.FileName);

                // Apply throttling between operations
                if (_throttler.ShouldThrottle(processedCount, totalFiles))
                {
                    await _throttler.ThrottleAsync(cancellationToken);
                }
            }

            stopwatch.Stop();

            // Report successful completion
            await _progressReporter.ReportJobCompletedAsync(jobId, JobType, totalFiles, stopwatch.Elapsed);

            _logger.LogInformation(
                "Batch job {JobId} completed: {Success} succeeded, {Failed} failed out of {Total} files in {Duration}ms",
                jobId, successCount, failedCount, totalFiles, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Batch job {JobId} was cancelled", jobId);
            await _progressReporter.ReportJobFailedAsync(jobId, JobType, "Job was cancelled", processedCount);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch job {JobId} failed with error: {Error}", jobId, ex.Message);
            await _progressReporter.ReportJobFailedAsync(jobId, JobType, ex.Message, processedCount);
            throw;
        }
    }

    /// <summary>
    /// Creates a job display name for Hangfire dashboard.
    /// </summary>
    private static string GetJobDisplayName(List<FileOrganizeOperation> operations, string batchName)
    {
        return $"Batch: {batchName} ({operations.Count} files)";
    }
}
