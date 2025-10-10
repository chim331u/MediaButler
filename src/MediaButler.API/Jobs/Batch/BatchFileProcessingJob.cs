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
/// Processes multiple files sequentially with ARM32 optimization and real-time progress updates via SignalR.
/// </summary>
[Queue("default")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
public class BatchFileProcessingJob : IBatchFileProcessor
{
    private readonly IFileOrganizationService _fileOrganizationService;
    private readonly SignalRNotificationClient _signalRClient;
    private readonly ILogger<BatchFileProcessingJob> _logger;

    public BatchFileProcessingJob(
        IFileOrganizationService fileOrganizationService,
        SignalRNotificationClient signalRClient,
        ILogger<BatchFileProcessingJob> logger)
    {
        _fileOrganizationService = fileOrganizationService;
        _signalRClient = signalRClient;
        _logger = logger;
    }

    /// <summary>
    /// Processes a batch of file organization operations.
    /// Sends progress notifications every 5 files for real-time Web UI updates.
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
            // Notify batch started
            await _signalRClient.NotifyBatchJobStartedAsync(
                jobId,
                "batch.file.processing",
                totalFiles);

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

                // Send progress notification every 5 files
                if (processedCount % 5 == 0 || processedCount == totalFiles)
                {
                    await _signalRClient.NotifyBatchJobProgressAsync(
                        jobId,
                        processedCount,
                        totalFiles,
                        operation.TrackedFile.FileName);
                }

                // ARM32 optimization: Add delay between files to prevent resource exhaustion
                if (processedCount < totalFiles)
                {
                    await Task.Delay(50, cancellationToken);
                }
            }

            stopwatch.Stop();

            // Notify batch completed
            await _signalRClient.NotifyBatchJobCompletedAsync(
                jobId,
                "batch.file.processing",
                totalFiles,
                stopwatch.Elapsed);

            _logger.LogInformation(
                "Batch job {JobId} completed: {Success} succeeded, {Failed} failed out of {Total} files in {Duration}ms",
                jobId, successCount, failedCount, totalFiles, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Batch job {JobId} was cancelled", jobId);
            await _signalRClient.NotifyBatchJobFailedAsync(
                jobId,
                "batch.file.processing",
                "Job was cancelled",
                processedCount);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch job {JobId} failed with error: {Error}", jobId, ex.Message);
            await _signalRClient.NotifyBatchJobFailedAsync(
                jobId,
                "batch.file.processing",
                ex.Message,
                processedCount);
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
