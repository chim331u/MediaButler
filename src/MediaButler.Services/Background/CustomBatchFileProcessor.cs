using MediaButler.Core.Models;
using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;
using MediaButler.Core.Services;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MediaButler.Services.Background;

/// <summary>
/// Custom batch file processor that works with the lightweight background task queue.
/// Replaces Hangfire-based BatchFileProcessor with similar functionality.
/// </summary>
public class CustomBatchFileProcessor
{
    private readonly ILogger<CustomBatchFileProcessor> _logger;

    public CustomBatchFileProcessor(ILogger<CustomBatchFileProcessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a background task for processing batch file operations.
    /// This replaces the Hangfire job queuing mechanism.
    /// </summary>
    public static Func<IServiceProvider, CancellationToken, Task> CreateBatchProcessingTask(
        List<FileOrganizeOperation> operations,
        BatchOrganizeRequest originalRequest,
        string jobId)
    {
        return async (serviceProvider, cancellationToken) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<CustomBatchFileProcessor>>();
            var notificationService = serviceProvider.GetRequiredService<INotificationService>();
            var fileOrganizationService = serviceProvider.GetRequiredService<IFileOrganizationService>();

            var processor = new CustomBatchFileProcessor(logger);
            await processor.ProcessBatchInternalAsync(
                operations,
                originalRequest,
                jobId,
                notificationService,
                fileOrganizationService,
                serviceProvider,
                cancellationToken);
        };
    }

    /// <summary>
    /// Internal method that processes the batch operations.
    /// Similar to the original BatchFileProcessor.ProcessBatchAsync but optimized for the custom queue.
    /// </summary>
    private async Task ProcessBatchInternalAsync(
        List<FileOrganizeOperation> operations,
        BatchOrganizeRequest originalRequest,
        string jobId,
        INotificationService notificationService,
        IFileOrganizationService fileOrganizationService,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<FileProcessingResult>();
        var successCount = 0;
        var failureCount = 0;

        _logger.LogInformation("Starting batch processing job {JobId} with {FileCount} files",
            jobId, operations.Count);

        // Initialize job metadata for tracking
        if (serviceProvider.GetService<IBackgroundTaskQueue>() is BackgroundTaskQueue taskQueue)
        {
            var jobInfo = taskQueue.GetJobInfo(jobId);
            if (jobInfo != null)
            {
                jobInfo.UpdateFileCount("totalFiles", operations.Count);
                jobInfo.UpdateFileCount("processedFiles", 0);
                jobInfo.UpdateFileCount("successfulFiles", 0);
                jobInfo.UpdateFileCount("failedFiles", 0);
            }
        }

        try
        {
            // Send job started notification
            await SendJobStartedNotification(operations, originalRequest, notificationService, cancellationToken);

            // Get optimal concurrency for ARM32
            var maxConcurrency = GetOptimalConcurrency(originalRequest.MaxConcurrency);

            // Process files with controlled concurrency
            await ProcessOperationsInParallel(operations, originalRequest, fileOrganizationService,
                jobId, results, maxConcurrency, notificationService, cancellationToken);

            // Calculate final counts
            successCount = results.Count(r => r.Success);
            failureCount = results.Count(r => !r.Success);

            // Update job metadata with final counts
            if (serviceProvider.GetService<IBackgroundTaskQueue>() is BackgroundTaskQueue updateQueue)
            {
                var jobInfo = updateQueue.GetJobInfo(jobId);
                if (jobInfo != null)
                {
                    jobInfo.UpdateFileCount("processedFiles", results.Count);
                    jobInfo.UpdateFileCount("successfulFiles", successCount);
                    jobInfo.UpdateFileCount("failedFiles", failureCount);
                }
            }

            stopwatch.Stop();

            // Send completion notification
            if (failureCount == 0 || originalRequest.ContinueOnError)
            {
                await SendJobCompletedNotification(jobId, operations.Count, successCount, failureCount,
                    results, stopwatch.Elapsed, originalRequest.DryRun, notificationService, cancellationToken);
            }
            else
            {
                await SendJobFailedNotification(jobId, $"Processing failed with {failureCount} errors",
                    stopwatch.Elapsed, notificationService, cancellationToken);
            }

            _logger.LogInformation("Batch processing job {JobId} completed: {SuccessCount} successful, {FailureCount} failed",
                jobId, successCount, failureCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Batch processing job {JobId} failed with exception: {Error}", jobId, ex.Message);

            await SendJobFailedNotification(jobId, ex.Message, stopwatch.Elapsed, notificationService, cancellationToken);
            throw;
        }
    }

    private async Task ProcessOperationsInParallel(
        List<FileOrganizeOperation> operations,
        BatchOrganizeRequest originalRequest,
        IFileOrganizationService fileOrganizationService,
        string jobId,
        List<FileProcessingResult> results,
        int maxConcurrency,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = new List<Task>();

        for (int i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            var currentIndex = i + 1;

            var task = ProcessSingleOperationAsync(operation, currentIndex, operations.Count,
                originalRequest, fileOrganizationService, jobId, results, semaphore,
                notificationService, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessSingleOperationAsync(
        FileOrganizeOperation operation,
        int currentIndex,
        int totalCount,
        BatchOrganizeRequest originalRequest,
        IFileOrganizationService fileOrganizationService,
        string jobId,
        List<FileProcessingResult> results,
        SemaphoreSlim semaphore,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            // Send file processing started notification
            await SendFileProcessingStartedNotification(jobId, operation, currentIndex, totalCount,
                notificationService, cancellationToken);

            var result = new FileProcessingResult
            {
                FileHash = operation.TrackedFile.Hash,
                FileName = operation.TrackedFile.FileName,
                Success = false, // Will be set based on processing result
                TargetPath = operation.TargetPath,
                ProcessedAt = DateTime.UtcNow,
                IsDryRun = originalRequest.DryRun
            };

            var processingStopwatch = Stopwatch.StartNew();

            try
            {
                if (originalRequest.DryRun)
                {
                    // Dry run - just validate
                    var validationResult = await fileOrganizationService
                        .ValidateOrganizationSafetyAsync(operation.TrackedFile.Hash, operation.TargetPath);

                    result.Success = validationResult.IsSuccess;
                    result.Error = validationResult.IsSuccess ? null : validationResult.Error;
                    result.ActualPath = operation.TargetPath;
                }
                else
                {
                    // Actual file organization
                    var organizationResult = await fileOrganizationService.OrganizeFileAsync(
                        operation.TrackedFile.Hash, operation.ConfirmedCategory);

                    if (organizationResult.IsSuccess)
                    {
                        result.Success = true;
                        result.ActualPath = organizationResult.Value.ActualPath;
                        result.Metadata = new Dictionary<string, object>
                        {
                            ["originalPath"] = operation.TrackedFile.OriginalPath,
                            ["organizationDetails"] = organizationResult.Value
                        };
                    }
                    else
                    {
                        result.Success = false;
                        result.Error = organizationResult.Error;

                        if (!originalRequest.ContinueOnError)
                        {
                            _logger.LogError("File processing failed for {FileHash}: {Error}. Stopping batch due to ContinueOnError=false",
                                operation.TrackedFile.Hash, organizationResult.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _logger.LogError(ex, "Exception processing file {FileHash}: {Error}", operation.TrackedFile.Hash, ex.Message);
            }

            processingStopwatch.Stop();
            result.ProcessingTime = processingStopwatch.Elapsed;

            // Thread-safe add to results
            lock (results)
            {
                results.Add(result);
            }

            // Send file processing completed notification
            await SendFileProcessingCompletedNotification(jobId, result, notificationService, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static int GetOptimalConcurrency(int? requestedConcurrency)
    {
        // ARM32 optimization - limit concurrency based on system resources
        var systemOptimal = Math.Max(1, Math.Min(Environment.ProcessorCount, 2));

        if (requestedConcurrency.HasValue)
        {
            return Math.Min(requestedConcurrency.Value, systemOptimal);
        }

        return systemOptimal;
    }

    #region Notification Methods

    private async Task SendJobStartedNotification(
        List<FileOrganizeOperation> operations,
        BatchOrganizeRequest originalRequest,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var batchName = originalRequest.BatchName ?? "Unnamed Batch";
            var message = $"Batch operation '{batchName}' started with {operations.Count} files (DryRun: {originalRequest.DryRun})";
            await notificationService.NotifySystemStatusAsync(message, NotificationSeverity.Info, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send job started notification");
        }
    }

    private async Task SendFileProcessingStartedNotification(
        string jobId,
        FileOrganizeOperation operation,
        int currentIndex,
        int totalCount,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var progress = (currentIndex * 100) / totalCount;
            var message = $"Processing file {currentIndex}/{totalCount} ({progress}%): {operation.TrackedFile.FileName} → {operation.ConfirmedCategory}";
            await notificationService.NotifyOperationStartedAsync(operation.TrackedFile.Hash, message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send file processing started notification");
        }
    }

    private async Task SendFileProcessingCompletedNotification(
        string jobId,
        FileProcessingResult result,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        try
        {
            if (result.Success)
            {
                var message = $"Successfully processed {result.FileName} in {result.ProcessingTime?.TotalMilliseconds:F0}ms";
                if (result.IsDryRun)
                    message += " (dry run)";
                if (!string.IsNullOrEmpty(result.ActualPath))
                    message += $" → {result.ActualPath}";

                await notificationService.NotifyOperationCompletedAsync(result.FileHash, message, cancellationToken);
            }
            else
            {
                var error = string.IsNullOrEmpty(result.Error) ? "Unknown error" : result.Error;
                await notificationService.NotifyOperationFailedAsync(result.FileHash, error, true, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send file processing completed notification");
        }
    }

    private async Task SendJobCompletedNotification(
        string jobId,
        int totalFiles,
        int successCount,
        int failureCount,
        List<FileProcessingResult> results,
        TimeSpan duration,
        bool isDryRun,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var dryRunText = isDryRun ? " (dry run)" : "";
            var message = $"Batch operation completed{dryRunText}: {successCount} successful, {failureCount} failed out of {totalFiles} files in {duration:mm\\:ss}";
            var severity = failureCount == 0 ? NotificationSeverity.Info : NotificationSeverity.Warning;

            await notificationService.NotifySystemStatusAsync(message, severity, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send job completed notification");
        }
    }

    private async Task SendJobFailedNotification(
        string jobId,
        string error,
        TimeSpan duration,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = $"Batch operation failed after {duration:mm\\:ss}: {error}";
            await notificationService.NotifySystemStatusAsync(message, NotificationSeverity.Error, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send job failed notification");
        }
    }

    #endregion
}