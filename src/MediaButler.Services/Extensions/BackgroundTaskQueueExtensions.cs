using MediaButler.Services.Background;
using MediaButler.Core.Models;
using MediaButler.Core.Models.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Extensions;

/// <summary>
/// Extension methods for registering the custom background task queue services.
/// Provides a lightweight alternative to Hangfire for ARM32 optimization.
/// </summary>
public static class BackgroundTaskQueueExtensions
{
    /// <summary>
    /// Adds the custom background task queue services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="queueCapacity">Maximum number of queued tasks (default: 100)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCustomBackgroundTaskQueue(
        this IServiceCollection services,
        int queueCapacity = 100)
    {
        // Register the task queue as singleton for shared state
        services.AddSingleton<IBackgroundTaskQueue>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<BackgroundTaskQueue>>();
            return new BackgroundTaskQueue(logger, queueCapacity);
        });

        // Register the hosted service that processes the queue
        services.AddHostedService<QueuedHostedService>();

        // Register the custom batch processor
        services.AddTransient<CustomBatchFileProcessor>();

        return services;
    }

    /// <summary>
    /// Helper method to queue a batch file processing job.
    /// Replaces Hangfire.BackgroundJob.Enqueue functionality.
    /// </summary>
    public static string QueueBatchFileProcessing(
        this IBackgroundTaskQueue taskQueue,
        List<FileOrganizeOperation> operations,
        BatchOrganizeRequest originalRequest)
    {
        var jobId = Guid.NewGuid().ToString("N")[..8]; // Short job ID
        var jobName = $"Batch: {originalRequest.BatchName ?? "Unnamed"} ({operations.Count} files)";

        var workItem = CustomBatchFileProcessor.CreateBatchProcessingTask(operations, originalRequest, jobId);

        taskQueue.QueueBackgroundWorkItem(workItem, jobId, jobName);

        return jobId;
    }
}