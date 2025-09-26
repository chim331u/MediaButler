using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Hosted service that processes background tasks from the queue.
/// Optimized for ARM32 with configurable concurrency and resource management.
/// </summary>
public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueuedHostedService> _logger;

    // ARM32 optimization - limit concurrent tasks
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly int _maxConcurrency;

    // Cleanup timer for old job tracking
    private readonly Timer _cleanupTimer;

    public QueuedHostedService(
        IBackgroundTaskQueue taskQueue,
        IServiceProvider serviceProvider,
        ILogger<QueuedHostedService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // ARM32 optimization - max 2 concurrent background tasks
        _maxConcurrency = Math.Max(1, Math.Min(Environment.ProcessorCount, 2));
        _concurrencySemaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

        // Setup cleanup timer to run every hour
        _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        _logger.LogInformation("QueuedHostedService started with max concurrency: {MaxConcurrency}", _maxConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background task processing service is starting");

        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                // Wait for available concurrency slot
                await _concurrencySemaphore.WaitAsync(stoppingToken);

                // Process the work item in a separate task to allow concurrent processing
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessWorkItemAsync(workItem, stoppingToken);
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing background task queue");

                // Small delay before continuing to prevent tight error loops
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task ProcessWorkItemAsync(QueuedWorkItem workItem, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing background job {JobId}: {JobName}",
                workItem.JobId, workItem.JobName);

            // Create a scope for dependency injection
            using var scope = _serviceProvider.CreateScope();

            // Execute the work item
            await workItem.WorkItem(scope.ServiceProvider, cancellationToken);

            stopwatch.Stop();

            // Mark job as completed
            if (_taskQueue is BackgroundTaskQueue queue)
            {
                queue.MarkJobCompleted(workItem.JobId);
            }

            _logger.LogInformation("Background job {JobId} completed successfully in {Duration}ms",
                workItem.JobId, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background job {JobId} was cancelled", workItem.JobId);

            if (_taskQueue is BackgroundTaskQueue queue)
            {
                queue.MarkJobFailed(workItem.JobId, "Job was cancelled");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Background job {JobId} failed after {Duration}ms: {Error}",
                workItem.JobId, stopwatch.ElapsedMilliseconds, ex.Message);

            // Mark job as failed
            if (_taskQueue is BackgroundTaskQueue queue)
            {
                queue.MarkJobFailed(workItem.JobId, ex.Message);
            }
        }
    }

    private void CleanupCallback(object? state)
    {
        try
        {
            if (_taskQueue is BackgroundTaskQueue queue)
            {
                queue.CleanupOldJobs();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during job cleanup");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background task processing service is stopping");

        await base.StopAsync(stoppingToken);

        // Wait for any running tasks to complete (with timeout)
        var timeout = TimeSpan.FromSeconds(30);
        var completed = await WaitForRunningTasksAsync(timeout);

        if (!completed)
        {
            _logger.LogWarning("Some background tasks did not complete within {Timeout} seconds", timeout.TotalSeconds);
        }
    }

    private async Task<bool> WaitForRunningTasksAsync(TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var status = _taskQueue.GetQueueStatus();
            if (status.ActiveJobs == 0)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    public override void Dispose()
    {
        _concurrencySemaphore?.Dispose();
        _cleanupTimer?.Dispose();
        base.Dispose();
    }
}