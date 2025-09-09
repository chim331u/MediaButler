using MediaButler.Core.Services;
using MediaButler.Data.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using MediaButler.Core.Enums;

namespace MediaButler.Services.Monitoring;

/// <summary>
/// Implementation of metrics collection service following "Simple Made Easy" principles.
/// Maintains separate concerns for collection, aggregation, and reporting.
/// Uses in-memory storage optimized for ARM32 resource constraints.
/// </summary>
/// <remarks>
/// This service provides lightweight metrics collection without complecting:
/// - Collection logic from reporting logic
/// - Real-time metrics from historical analysis
/// - Memory management from metric calculation
/// 
/// Optimized for ARM32 NAS deployment with minimal memory overhead.
/// </remarks>
public class MetricsCollectionService : IMetricsCollectionService
{
    private readonly ITrackedFileRepository _fileRepository;
    private readonly IProcessingLogRepository _processingLogRepository;
    private readonly ILogger<MetricsCollectionService> _logger;

    // In-memory metrics storage for real-time data (ARM32 optimized)
    private readonly ConcurrentQueue<ProcessingEvent> _processingEvents = new();
    private readonly ConcurrentQueue<ClassificationResult> _classificationResults = new();
    private readonly ConcurrentQueue<ErrorEvent> _errorEvents = new();
    private readonly ConcurrentQueue<PerformanceData> _performanceData = new();

    // Configuration constants optimized for ARM32
    private const int MaxInMemoryEvents = 1000;
    private const int CleanupIntervalMinutes = 15;
    private readonly Timer _cleanupTimer;

    public MetricsCollectionService(
        ITrackedFileRepository fileRepository,
        IProcessingLogRepository processingLogRepository,
        ILogger<MetricsCollectionService> logger)
    {
        _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
        _processingLogRepository = processingLogRepository ?? throw new ArgumentNullException(nameof(processingLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Setup periodic cleanup to manage memory usage
        _cleanupTimer = new Timer(CleanupOldEvents, null, 
            TimeSpan.FromMinutes(CleanupIntervalMinutes), 
            TimeSpan.FromMinutes(CleanupIntervalMinutes));
    }

    public async Task RecordProcessingEventAsync(ProcessingEventType eventType, string fileHash, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return;

        var processingEvent = new ProcessingEvent
        {
            EventType = eventType,
            FileHash = fileHash,
            Category = category,
            Timestamp = DateTime.UtcNow
        };

        _processingEvents.Enqueue(processingEvent);

        // Manage memory by keeping only recent events
        await TrimQueueIfNeeded(_processingEvents, MaxInMemoryEvents);

        _logger.LogDebug("Recorded processing event {EventType} for file {FileHash}", 
            eventType, fileHash);
    }

    public async Task RecordClassificationResultAsync(string fileHash, string suggestedCategory, decimal confidence, bool? wasAccepted = null)
    {
        if (string.IsNullOrWhiteSpace(fileHash) || string.IsNullOrWhiteSpace(suggestedCategory))
            return;

        var classificationResult = new ClassificationResult
        {
            FileHash = fileHash,
            SuggestedCategory = suggestedCategory,
            Confidence = confidence,
            WasAccepted = wasAccepted,
            Timestamp = DateTime.UtcNow
        };

        _classificationResults.Enqueue(classificationResult);

        // Manage memory by keeping only recent results
        await TrimQueueIfNeeded(_classificationResults, MaxInMemoryEvents);

        _logger.LogDebug("Recorded classification result for file {FileHash}: {Category} (confidence: {Confidence:F2})", 
            fileHash, suggestedCategory, confidence);
    }

    public async Task RecordErrorEventAsync(ErrorType errorType, string fileHash, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return;

        var errorEvent = new ErrorEvent
        {
            ErrorType = errorType,
            FileHash = fileHash,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        _errorEvents.Enqueue(errorEvent);

        // Manage memory by keeping only recent errors
        await TrimQueueIfNeeded(_errorEvents, MaxInMemoryEvents);

        _logger.LogDebug("Recorded error event {ErrorType} for file {FileHash}: {ErrorMessage}", 
            errorType, fileHash, errorMessage);
    }

    public async Task RecordPerformanceDataAsync(OperationType operationType, TimeSpan duration, ResourceUsageData? resourceUsage = null)
    {
        var performanceData = new PerformanceData
        {
            OperationType = operationType,
            Duration = duration,
            ResourceUsage = resourceUsage,
            Timestamp = DateTime.UtcNow
        };

        _performanceData.Enqueue(performanceData);

        // Manage memory by keeping only recent performance data
        await TrimQueueIfNeeded(_performanceData, MaxInMemoryEvents);

        _logger.LogDebug("Recorded performance data for {OperationType}: {Duration}ms", 
            operationType, duration.TotalMilliseconds);
    }

    public async Task<QueueMetrics> GetQueueMetricsAsync(TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? TimeSpan.FromHours(1);
        var cutoffTime = DateTime.UtcNow - window;

        // Get current queue status from repository
        var stats = await _fileRepository.GetProcessingStatsAsync();
        
        // Get throughput from in-memory events
        var recentEvents = _processingEvents
            .Where(e => e.Timestamp >= cutoffTime)
            .ToList();

        var filesProcessedLastHour = recentEvents
            .Count(e => e.EventType == ProcessingEventType.ProcessingFailed || 
                       e.EventType == ProcessingEventType.MoveCompleted);

        var avgProcessingTime = CalculateAverageProcessingTime(recentEvents);
        var throughputPerHour = filesProcessedLastHour > 0 ? filesProcessedLastHour / window.TotalHours : 0;

        return new QueueMetrics
        {
            CurrentQueueDepth = stats.GetValueOrDefault(FileStatus.New, 0) + 
                               stats.GetValueOrDefault(FileStatus.Processing, 0),
            FilesProcessedLastHour = filesProcessedLastHour,
            FilesAwaitingConfirmation = stats.GetValueOrDefault(FileStatus.Classified, 0),
            FilesReadyToMove = stats.GetValueOrDefault(FileStatus.ReadyToMove, 0),
            AverageProcessingTimeSeconds = avgProcessingTime.TotalSeconds,
            ThroughputFilesPerHour = throughputPerHour,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<ClassificationMetrics> GetClassificationMetricsAsync(TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? TimeSpan.FromHours(24);
        var cutoffTime = DateTime.UtcNow - window;

        var recentResults = _classificationResults
            .Where(r => r.Timestamp >= cutoffTime)
            .ToList();

        if (recentResults.Count == 0)
        {
            return new ClassificationMetrics { LastUpdated = DateTime.UtcNow };
        }

        var accepted = recentResults.Count(r => r.WasAccepted == true);
        var rejected = recentResults.Count(r => r.WasAccepted == false);
        var pending = recentResults.Count(r => r.WasAccepted == null);

        var accuracyRate = accepted + rejected > 0 ? (double)accepted / (accepted + rejected) : 0;
        var avgConfidence = recentResults.Average(r => (double)r.Confidence);

        var confidenceBreakdown = ClassifyByConfidence(recentResults);
        var categoryDistribution = recentResults
            .GroupBy(r => r.SuggestedCategory)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ClassificationMetrics
        {
            TotalClassifications = recentResults.Count,
            AcceptedSuggestions = accepted,
            RejectedSuggestions = rejected,
            AccuracyRate = accuracyRate,
            AverageConfidence = avgConfidence,
            HighConfidenceClassifications = confidenceBreakdown.High,
            MediumConfidenceClassifications = confidenceBreakdown.Medium,
            LowConfidenceClassifications = confidenceBreakdown.Low,
            CategoryDistribution = categoryDistribution,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<ErrorMetrics> GetErrorMetricsAsync(TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? TimeSpan.FromHours(24);
        var cutoffTime = DateTime.UtcNow - window;

        var recentErrors = _errorEvents
            .Where(e => e.Timestamp >= cutoffTime)
            .ToList();

        var totalProcessingEvents = _processingEvents
            .Count(e => e.Timestamp >= cutoffTime);

        var errorRate = totalProcessingEvents > 0 ? (double)recentErrors.Count / totalProcessingEvents : 0;
        
        var errorsByType = recentErrors
            .GroupBy(e => e.ErrorType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get additional error info from repository
        var filesNeedingIntervention = await _fileRepository.GetFilesExceedingRetryLimitAsync(3);
        var retryStats = await GetRetryStatistics(cutoffTime);

        return new ErrorMetrics
        {
            TotalErrors = recentErrors.Count,
            ErrorRate = errorRate,
            ErrorsByType = errorsByType,
            FilesRequiringIntervention = filesNeedingIntervention.Count(),
            RetriesAttempted = retryStats.Attempted,
            RetriesSuccessful = retryStats.Successful,
            LastErrorTime = recentErrors.LastOrDefault()?.Timestamp ?? DateTime.MinValue,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? TimeSpan.FromHours(1);
        var cutoffTime = DateTime.UtcNow - window;

        var recentPerformanceData = _performanceData
            .Where(p => p.Timestamp >= cutoffTime)
            .ToList();

        var avgClassificationTime = CalculateAverageOperationTime(recentPerformanceData, OperationType.MLClassification);
        var avgFileMoveTime = CalculateAverageOperationTime(recentPerformanceData, OperationType.FileMove);
        var avgDbOpTime = CalculateAverageOperationTime(recentPerformanceData, OperationType.DatabaseOperation);

        // Get current resource usage (simplified for ARM32)
        var currentResourceUsage = GetCurrentResourceUsage();

        return new PerformanceMetrics
        {
            AverageClassificationTimeMs = avgClassificationTime.TotalMilliseconds,
            AverageFileMoveTimeMs = avgFileMoveTime.TotalMilliseconds,
            AverageDatabaseOpTimeMs = avgDbOpTime.TotalMilliseconds,
            CurrentMemoryUsageMB = currentResourceUsage.MemoryUsageMB,
            CurrentCpuUsagePercent = currentResourceUsage.CpuUsagePercent,
            ActiveThreadCount = currentResourceUsage.ThreadCount,
            DiskIOThroughputMBps = currentResourceUsage.DiskIOThroughputMBps,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<SystemHealthSummary> GetSystemHealthSummaryAsync()
    {
        var queueMetrics = await GetQueueMetricsAsync();
        var classificationMetrics = await GetClassificationMetricsAsync();
        var errorMetrics = await GetErrorMetricsAsync();
        var performanceMetrics = await GetPerformanceMetricsAsync();

        var alerts = GenerateHealthAlerts(queueMetrics, errorMetrics, performanceMetrics);
        var overallStatus = DetermineOverallStatus(alerts);

        return new SystemHealthSummary
        {
            OverallStatus = overallStatus,
            QueueStatus = queueMetrics,
            MLPerformance = classificationMetrics,
            ErrorStatus = errorMetrics,
            SystemPerformance = performanceMetrics,
            Alerts = alerts,
            GeneratedAt = DateTime.UtcNow
        };
    }

    #region Private Helper Methods

    private async Task TrimQueueIfNeeded<T>(ConcurrentQueue<T> queue, int maxSize)
    {
        if (queue.Count <= maxSize) return;

        // Remove old items to maintain memory limits
        var itemsToRemove = queue.Count - maxSize;
        for (int i = 0; i < itemsToRemove; i++)
        {
            queue.TryDequeue(out _);
        }
    }

    private void CleanupOldEvents(object? state)
    {
        var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(24);
        
        // Clean up old events to manage memory
        CleanupQueue(_processingEvents, e => e.Timestamp >= cutoffTime);
        CleanupQueue(_classificationResults, r => r.Timestamp >= cutoffTime);
        CleanupQueue(_errorEvents, e => e.Timestamp >= cutoffTime);
        CleanupQueue(_performanceData, p => p.Timestamp >= cutoffTime);

        _logger.LogDebug("Cleaned up old metric events");
    }

    private void CleanupQueue<T>(ConcurrentQueue<T> queue, Func<T, bool> keepPredicate)
    {
        var itemsToKeep = new List<T>();
        
        while (queue.TryDequeue(out var item))
        {
            if (keepPredicate(item))
            {
                itemsToKeep.Add(item);
            }
        }

        foreach (var item in itemsToKeep)
        {
            queue.Enqueue(item);
        }
    }

    private TimeSpan CalculateAverageProcessingTime(List<ProcessingEvent> events)
    {
        var completedFiles = events
            .GroupBy(e => e.FileHash)
            .Where(g => g.Any(e => e.EventType == ProcessingEventType.FileDiscovered) &&
                       g.Any(e => e.EventType == ProcessingEventType.MoveCompleted ||
                                 e.EventType == ProcessingEventType.ProcessingFailed))
            .ToList();

        if (completedFiles.Count == 0) return TimeSpan.Zero;

        var processingTimes = completedFiles
            .Select(g => {
                var start = g.Where(e => e.EventType == ProcessingEventType.FileDiscovered).Min(e => e.Timestamp);
                var end = g.Where(e => e.EventType == ProcessingEventType.MoveCompleted || 
                                      e.EventType == ProcessingEventType.ProcessingFailed).Max(e => e.Timestamp);
                return end - start;
            })
            .ToList();

        return TimeSpan.FromTicks((long)processingTimes.Average(ts => ts.Ticks));
    }

    private (int High, int Medium, int Low) ClassifyByConfidence(List<ClassificationResult> results)
    {
        var high = results.Count(r => r.Confidence > 0.85m);
        var medium = results.Count(r => r.Confidence >= 0.5m && r.Confidence <= 0.85m);
        var low = results.Count(r => r.Confidence < 0.5m);
        
        return (high, medium, low);
    }

    private TimeSpan CalculateAverageOperationTime(List<PerformanceData> data, OperationType operationType)
    {
        var operationData = data.Where(d => d.OperationType == operationType).ToList();
        
        if (operationData.Count == 0) return TimeSpan.Zero;
        
        return TimeSpan.FromTicks((long)operationData.Average(d => d.Duration.Ticks));
    }

    private async Task<(int Attempted, int Successful)> GetRetryStatistics(DateTime cutoffTime)
    {
        // Get retry statistics from processing logs (simplified implementation)
        // In a full implementation, we would add GetByOperationAsync to IProcessingLogRepository
        var attempted = 0;
        var successful = 0;
        
        // TODO: Implement proper retry statistics when GetByOperationAsync is added to repository
        
        return (attempted, successful);
    }

    private (long MemoryUsageMB, double CpuUsagePercent, int ThreadCount, double DiskIOThroughputMBps) GetCurrentResourceUsage()
    {
        // Simplified resource monitoring for ARM32
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var memoryMB = process.WorkingSet64 / (1024 * 1024);
        var threadCount = process.Threads.Count;
        
        // CPU and disk I/O would require more complex monitoring
        // For now, return simplified values
        return (memoryMB, 0.0, threadCount, 0.0);
    }

    private List<string> GenerateHealthAlerts(QueueMetrics queue, ErrorMetrics errors, PerformanceMetrics performance)
    {
        var alerts = new List<string>();

        // Queue depth alerts
        if (queue.CurrentQueueDepth > 100)
            alerts.Add($"High queue depth: {queue.CurrentQueueDepth} files pending");

        // Error rate alerts  
        if (errors.ErrorRate > 0.1)
            alerts.Add($"High error rate: {errors.ErrorRate:P1}");

        if (errors.FilesRequiringIntervention > 0)
            alerts.Add($"{errors.FilesRequiringIntervention} files require manual intervention");

        // Performance alerts (ARM32 specific)
        if (performance.CurrentMemoryUsageMB > 250)
            alerts.Add($"High memory usage: {performance.CurrentMemoryUsageMB}MB (target: <300MB)");

        if (performance.AverageClassificationTimeMs > 5000)
            alerts.Add($"Slow ML classification: {performance.AverageClassificationTimeMs:F0}ms average");

        return alerts;
    }

    private string DetermineOverallStatus(List<string> alerts)
    {
        if (alerts.Count == 0) return "Healthy";
        if (alerts.Count <= 2) return "Warning";
        return "Critical";
    }

    #endregion

    #region Internal Data Structures

    private record ProcessingEvent
    {
        public ProcessingEventType EventType { get; init; }
        public string FileHash { get; init; } = string.Empty;
        public string? Category { get; init; }
        public DateTime Timestamp { get; init; }
    }

    private record ClassificationResult
    {
        public string FileHash { get; init; } = string.Empty;
        public string SuggestedCategory { get; init; } = string.Empty;
        public decimal Confidence { get; init; }
        public bool? WasAccepted { get; init; }
        public DateTime Timestamp { get; init; }
    }

    private record ErrorEvent
    {
        public ErrorType ErrorType { get; init; }
        public string FileHash { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
    }

    private record PerformanceData
    {
        public OperationType OperationType { get; init; }
        public TimeSpan Duration { get; init; }
        public ResourceUsageData? ResourceUsage { get; init; }
        public DateTime Timestamp { get; init; }
    }

    #endregion
}