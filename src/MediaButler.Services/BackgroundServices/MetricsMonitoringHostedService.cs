using MediaButler.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.BackgroundServices;

/// <summary>
/// Background service for periodic metrics collection and system health monitoring.
/// Follows "Simple Made Easy" principles with clear separation of monitoring from business logic.
/// Optimized for ARM32 deployment with minimal resource overhead.
/// </summary>
/// <remarks>
/// This service provides:
/// - Periodic system health checks and alerting
/// - Metrics aggregation and cleanup
/// - Performance monitoring and resource tracking
/// - Alert generation for critical conditions
/// 
/// Runs independently from processing services without complecting concerns.
/// </remarks>
public class MetricsMonitoringHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsMonitoringHostedService> _logger;
    
    // Monitoring intervals optimized for ARM32
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MetricsCleanupInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AlertCheckInterval = TimeSpan.FromMinutes(2);

    private readonly PeriodicTimer _healthCheckTimer;
    private readonly PeriodicTimer _metricsCleanupTimer;
    private readonly PeriodicTimer _alertCheckTimer;

    public MetricsMonitoringHostedService(
        IServiceProvider serviceProvider,
        ILogger<MetricsMonitoringHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _healthCheckTimer = new PeriodicTimer(HealthCheckInterval);
        _metricsCleanupTimer = new PeriodicTimer(MetricsCleanupInterval);
        _alertCheckTimer = new PeriodicTimer(AlertCheckInterval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics monitoring service started");

        // Start monitoring tasks concurrently
        var tasks = new[]
        {
            PerformHealthChecks(stoppingToken),
            PerformMetricsCleanup(stoppingToken),
            CheckForAlerts(stoppingToken)
        };

        try
        {
            await Task.WhenAny(tasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Metrics monitoring service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metrics monitoring service encountered an error");
            throw;
        }
    }

    /// <summary>
    /// Performs periodic system health checks and logs overall system status.
    /// </summary>
    private async Task PerformHealthChecks(CancellationToken cancellationToken)
    {
        while (await _healthCheckTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsCollectionService>();

                var healthSummary = await metricsService.GetSystemHealthSummaryAsync();

                // Log health status
                _logger.LogInformation(
                    "System Health Check: Status={Status}, Queue={QueueDepth}, Memory={MemoryMB}MB, Errors={ErrorCount}",
                    healthSummary.OverallStatus,
                    healthSummary.QueueStatus.CurrentQueueDepth,
                    healthSummary.SystemPerformance.CurrentMemoryUsageMB,
                    healthSummary.ErrorStatus.TotalErrors);

                // Log performance metrics
                if (healthSummary.QueueStatus.ThroughputFilesPerHour > 0)
                {
                    _logger.LogDebug(
                        "Processing Metrics: Throughput={ThroughputPerHour:F1} files/hour, " +
                        "Avg Classification Time={AvgClassificationMs:F0}ms, " +
                        "ML Accuracy={MLAccuracy:P1}",
                        healthSummary.QueueStatus.ThroughputFilesPerHour,
                        healthSummary.SystemPerformance.AverageClassificationTimeMs,
                        healthSummary.MLPerformance.AccuracyRate);
                }

                // Log alerts if any
                foreach (var alert in healthSummary.Alerts)
                {
                    _logger.LogWarning("System Alert: {Alert}", alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health check");
            }
        }
    }

    /// <summary>
    /// Performs periodic cleanup of old metrics data to manage memory usage.
    /// </summary>
    private async Task PerformMetricsCleanup(CancellationToken cancellationToken)
    {
        while (await _metricsCleanupTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                // Force garbage collection to free up memory (ARM32 optimization)
                var beforeCleanup = GC.GetTotalMemory(false) / (1024 * 1024);
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var afterCleanup = GC.GetTotalMemory(false) / (1024 * 1024);
                var freedMemory = beforeCleanup - afterCleanup;

                if (freedMemory > 0)
                {
                    _logger.LogDebug("Memory cleanup completed: Freed {FreedMB}MB (Before: {BeforeMB}MB, After: {AfterMB}MB)",
                        freedMemory, beforeCleanup, afterCleanup);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing metrics cleanup");
            }
        }
    }

    /// <summary>
    /// Checks for critical system conditions and generates alerts.
    /// </summary>
    private async Task CheckForAlerts(CancellationToken cancellationToken)
    {
        while (await _alertCheckTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsCollectionService>();

                // Check for critical conditions
                await CheckQueueBacklog(metricsService);
                await CheckErrorRates(metricsService);
                await CheckResourceUsage(metricsService);
                await CheckMLPerformance(metricsService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for alerts");
            }
        }
    }

    private async Task CheckQueueBacklog(IMetricsCollectionService metricsService)
    {
        var queueMetrics = await metricsService.GetQueueMetricsAsync(TimeSpan.FromMinutes(10));
        
        // Alert if queue is backing up
        if (queueMetrics.CurrentQueueDepth > 50)
        {
            _logger.LogWarning(
                "Queue backlog detected: {QueueDepth} files pending, throughput: {Throughput:F1} files/hour",
                queueMetrics.CurrentQueueDepth, queueMetrics.ThroughputFilesPerHour);
        }

        // Alert if processing has stalled
        if (queueMetrics.CurrentQueueDepth > 10 && queueMetrics.ThroughputFilesPerHour == 0)
        {
            _logger.LogError(
                "Processing appears stalled: {QueueDepth} files pending with zero throughput",
                queueMetrics.CurrentQueueDepth);
        }
    }

    private async Task CheckErrorRates(IMetricsCollectionService metricsService)
    {
        var errorMetrics = await metricsService.GetErrorMetricsAsync(TimeSpan.FromMinutes(30));
        
        // Alert on high error rates
        if (errorMetrics.ErrorRate > 0.2) // 20% error rate
        {
            _logger.LogError(
                "High error rate detected: {ErrorRate:P1} ({TotalErrors} errors)",
                errorMetrics.ErrorRate, errorMetrics.TotalErrors);
        }

        // Alert on files requiring intervention
        if (errorMetrics.FilesRequiringIntervention > 0)
        {
            _logger.LogWarning(
                "{InterventionCount} files require manual intervention",
                errorMetrics.FilesRequiringIntervention);
        }
    }

    private async Task CheckResourceUsage(IMetricsCollectionService metricsService)
    {
        var performanceMetrics = await metricsService.GetPerformanceMetricsAsync(TimeSpan.FromMinutes(5));
        
        // Alert on high memory usage (ARM32 specific)
        if (performanceMetrics.CurrentMemoryUsageMB > 280) // Close to 300MB limit
        {
            _logger.LogWarning(
                "High memory usage: {MemoryMB}MB (target: <300MB for ARM32)",
                performanceMetrics.CurrentMemoryUsageMB);
        }

        // Alert on very slow performance
        if (performanceMetrics.AverageClassificationTimeMs > 10000) // 10 seconds
        {
            _logger.LogWarning(
                "Slow ML classification performance: {AvgTimeMs:F0}ms average",
                performanceMetrics.AverageClassificationTimeMs);
        }
    }

    private async Task CheckMLPerformance(IMetricsCollectionService metricsService)
    {
        var classificationMetrics = await metricsService.GetClassificationMetricsAsync(TimeSpan.FromHours(1));
        
        // Alert on poor ML accuracy
        if (classificationMetrics.TotalClassifications > 10 && classificationMetrics.AccuracyRate < 0.6)
        {
            _logger.LogWarning(
                "Low ML classification accuracy: {AccuracyRate:P1} ({AcceptedCount}/{TotalCount})",
                classificationMetrics.AccuracyRate,
                classificationMetrics.AcceptedSuggestions,
                classificationMetrics.TotalClassifications);
        }

        // Alert on too many low confidence classifications
        var lowConfidenceRate = classificationMetrics.TotalClassifications > 0 
            ? (double)classificationMetrics.LowConfidenceClassifications / classificationMetrics.TotalClassifications 
            : 0;

        if (classificationMetrics.TotalClassifications > 10 && lowConfidenceRate > 0.5)
        {
            _logger.LogWarning(
                "High rate of low confidence classifications: {LowConfidenceRate:P1} ({LowCount}/{TotalCount})",
                lowConfidenceRate,
                classificationMetrics.LowConfidenceClassifications,
                classificationMetrics.TotalClassifications);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping metrics monitoring service...");

        _healthCheckTimer.Dispose();
        _metricsCleanupTimer.Dispose();
        _alertCheckTimer.Dispose();

        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("Metrics monitoring service stopped");
    }
}