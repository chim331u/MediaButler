using MediaButler.Core.Enums;

namespace MediaButler.Core.Services;

/// <summary>
/// Service for collecting and tracking system metrics following "Simple Made Easy" principles.
/// Provides clear separation between metric collection and reporting without complecting concerns.
/// </summary>
/// <remarks>
/// This service collects metrics for:
/// - Processing queue status and throughput
/// - ML classification accuracy and confidence scores
/// - Error rates and retry statistics
/// - Performance counters and resource utilization
/// 
/// Metrics are kept simple with clear value semantics rather than complex state management.
/// Each metric type has a single responsibility without braiding concerns.
/// </remarks>
public interface IMetricsCollectionService
{
    /// <summary>
    /// Records a file processing event for queue metrics.
    /// Tracks throughput and queue depth over time.
    /// </summary>
    /// <param name="eventType">Type of processing event that occurred</param>
    /// <param name="fileHash">Hash of the file being processed</param>
    /// <param name="category">Optional category for classification metrics</param>
    Task RecordProcessingEventAsync(ProcessingEventType eventType, string fileHash, string? category = null);

    /// <summary>
    /// Records ML classification result for accuracy tracking.
    /// Maintains success rates and confidence score distributions.
    /// </summary>
    /// <param name="fileHash">Hash of the classified file</param>
    /// <param name="suggestedCategory">Category suggested by ML model</param>
    /// <param name="confidence">Confidence score from ML model (0.0 to 1.0)</param>
    /// <param name="wasAccepted">Whether the suggestion was accepted by user</param>
    Task RecordClassificationResultAsync(string fileHash, string suggestedCategory, decimal confidence, bool? wasAccepted = null);

    /// <summary>
    /// Records an error event for error rate monitoring.
    /// Tracks error patterns and failure modes.
    /// </summary>
    /// <param name="errorType">Type of error that occurred</param>
    /// <param name="fileHash">Hash of the file associated with error</param>
    /// <param name="errorMessage">Error message for debugging</param>
    Task RecordErrorEventAsync(ErrorType errorType, string fileHash, string errorMessage);

    /// <summary>
    /// Records performance data for throughput analysis.
    /// Measures processing times and resource usage.
    /// </summary>
    /// <param name="operationType">Type of operation being measured</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="resourceUsage">Optional resource usage data</param>
    Task RecordPerformanceDataAsync(OperationType operationType, TimeSpan duration, ResourceUsageData? resourceUsage = null);

    /// <summary>
    /// Gets current processing queue metrics.
    /// Provides real-time queue status and throughput.
    /// </summary>
    /// <param name="timeWindow">Time window for metric aggregation (default: last hour)</param>
    Task<QueueMetrics> GetQueueMetricsAsync(TimeSpan? timeWindow = null);

    /// <summary>
    /// Gets ML classification performance metrics.
    /// Provides accuracy rates and confidence distributions.
    /// </summary>
    /// <param name="timeWindow">Time window for metric aggregation (default: last 24 hours)</param>
    Task<ClassificationMetrics> GetClassificationMetricsAsync(TimeSpan? timeWindow = null);

    /// <summary>
    /// Gets error rate metrics and trending.
    /// Provides error rates and failure analysis.
    /// </summary>
    /// <param name="timeWindow">Time window for metric aggregation (default: last 24 hours)</param>
    Task<ErrorMetrics> GetErrorMetricsAsync(TimeSpan? timeWindow = null);

    /// <summary>
    /// Gets performance metrics and system health indicators.
    /// Provides throughput and resource utilization data.
    /// </summary>
    /// <param name="timeWindow">Time window for metric aggregation (default: last hour)</param>
    Task<PerformanceMetrics> GetPerformanceMetricsAsync(TimeSpan? timeWindow = null);

    /// <summary>
    /// Gets comprehensive system health summary.
    /// Aggregates all metric types for dashboard display.
    /// </summary>
    Task<SystemHealthSummary> GetSystemHealthSummaryAsync();
}

/// <summary>
/// Types of processing events for queue metrics.
/// </summary>
public enum ProcessingEventType
{
    FileDiscovered,
    QueuedForClassification,
    ClassificationStarted,
    ClassificationCompleted,
    AwaitingConfirmation,
    CategoryConfirmed,
    QueuedForMove,
    MoveStarted,
    MoveCompleted,
    ProcessingFailed
}

/// <summary>
/// Types of errors for error rate tracking.
/// </summary>
public enum ErrorType
{
    ClassificationError,
    FileAccessError,
    DatabaseError,
    NetworkError,
    ValidationError,
    ConcurrencyError,
    ResourceExhaustionError
}

/// <summary>
/// Types of operations for performance measurement.
/// </summary>
public enum OperationType
{
    FileHashCalculation,
    MLClassification,
    DatabaseOperation,
    FileMove,
    BatchProcessing,
    QueueProcessing
}

/// <summary>
/// Resource usage data for performance tracking.
/// </summary>
public record ResourceUsageData
{
    public long MemoryUsageBytes { get; init; }
    public double CpuUsagePercent { get; init; }
    public long DiskIOBytes { get; init; }
    public int ThreadCount { get; init; }
}

/// <summary>
/// Queue metrics for processing status.
/// </summary>
public record QueueMetrics
{
    public int CurrentQueueDepth { get; init; }
    public int FilesProcessedLastHour { get; init; }
    public int FilesAwaitingConfirmation { get; init; }
    public int FilesReadyToMove { get; init; }
    public double AverageProcessingTimeSeconds { get; init; }
    public double ThroughputFilesPerHour { get; init; }
    public DateTime LastUpdated { get; init; }
}

/// <summary>
/// ML classification performance metrics.
/// </summary>
public record ClassificationMetrics
{
    public int TotalClassifications { get; init; }
    public int AcceptedSuggestions { get; init; }
    public int RejectedSuggestions { get; init; }
    public double AccuracyRate { get; init; }
    public double AverageConfidence { get; init; }
    public int HighConfidenceClassifications { get; init; }  // > 0.85
    public int MediumConfidenceClassifications { get; init; } // 0.5 - 0.85
    public int LowConfidenceClassifications { get; init; }   // < 0.5
    public Dictionary<string, int> CategoryDistribution { get; init; } = new();
    public DateTime LastUpdated { get; init; }
}

/// <summary>
/// Error rate and failure metrics.
/// </summary>
public record ErrorMetrics
{
    public int TotalErrors { get; init; }
    public double ErrorRate { get; init; }
    public Dictionary<ErrorType, int> ErrorsByType { get; init; } = new();
    public int FilesRequiringIntervention { get; init; }
    public int RetriesAttempted { get; init; }
    public int RetriesSuccessful { get; init; }
    public DateTime LastErrorTime { get; init; }
    public DateTime LastUpdated { get; init; }
}

/// <summary>
/// Performance and throughput metrics.
/// </summary>
public record PerformanceMetrics
{
    public double AverageClassificationTimeMs { get; init; }
    public double AverageFileMoveTimeMs { get; init; }
    public double AverageDatabaseOpTimeMs { get; init; }
    public long CurrentMemoryUsageMB { get; init; }
    public double CurrentCpuUsagePercent { get; init; }
    public int ActiveThreadCount { get; init; }
    public double DiskIOThroughputMBps { get; init; }
    public DateTime LastUpdated { get; init; }
}

/// <summary>
/// Comprehensive system health summary.
/// </summary>
public record SystemHealthSummary
{
    public string OverallStatus { get; init; } = string.Empty; // Healthy, Warning, Critical
    public QueueMetrics QueueStatus { get; init; } = new();
    public ClassificationMetrics MLPerformance { get; init; } = new();
    public ErrorMetrics ErrorStatus { get; init; } = new();
    public PerformanceMetrics SystemPerformance { get; init; } = new();
    public List<string> Alerts { get; init; } = new();
    public DateTime GeneratedAt { get; init; }
}