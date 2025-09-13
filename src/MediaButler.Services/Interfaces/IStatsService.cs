using MediaButler.Core.Common;
using MediaButler.Core.Enums;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Service interface for statistics and monitoring data in the MediaButler system.
/// Provides aggregated metrics and analytics following "Simple Made Easy" principles
/// by keeping monitoring concerns separate from operational logic.
/// </summary>
/// <remarks>
/// This service generates statistics and monitoring data for:
/// - File processing workflow metrics (counts by status, throughput)
/// - ML classification performance (accuracy, confidence distributions)
/// - System health monitoring (error rates, processing times)
/// - User activity and usage patterns
/// - Performance analytics and trend analysis
/// 
/// All statistics are computed from existing data without side effects,
/// providing read-only access to aggregated information for dashboards and reports.
/// </remarks>
public interface IStatsService
{
    /// <summary>
    /// Gets processing statistics showing file counts by status.
    /// Provides overview of files in each stage of the processing pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing processing statistics summary.</returns>
    Task<Result<ProcessingStats>> GetProcessingStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ML classification performance metrics including accuracy and confidence distributions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing ML performance statistics.</returns>
    Task<Result<MLPerformanceStats>> GetMLPerformanceStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets system health metrics including error rates and processing performance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing system health statistics.</returns>
    Task<Result<SystemHealthStats>> GetSystemHealthStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file processing activity for a specific date range.
    /// Useful for trend analysis and capacity planning.
    /// </summary>
    /// <param name="startDate">Start date for the range (inclusive).</param>
    /// <param name="endDate">End date for the range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing activity statistics for the date range.</returns>
    Task<Result<ActivityStats>> GetActivityStatsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets category distribution showing how files are organized.
    /// Includes counts and percentages for each category.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing category distribution statistics.</returns>
    Task<Result<CategoryStats>> GetCategoryDistributionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing throughput metrics showing files processed over time.
    /// Includes average processing times and throughput rates.
    /// </summary>
    /// <param name="withinHours">Number of hours to analyze (default: 24).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing throughput statistics.</returns>
    Task<Result<ThroughputStats>> GetThroughputStatsAsync(int withinHours = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets error analysis showing common errors and failure patterns.
    /// </summary>
    /// <param name="withinDays">Number of days to analyze (default: 7).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing error analysis statistics.</returns>
    Task<Result<ErrorStats>> GetErrorAnalysisAsync(int withinDays = 7, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file size distribution statistics to understand storage patterns.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing file size distribution statistics.</returns>
    Task<Result<FileSizeStats>> GetFileSizeDistributionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical trends for processing metrics over time.
    /// Useful for identifying performance patterns and capacity needs.
    /// </summary>
    /// <param name="days">Number of days of history to include (default: 30).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing trend analysis data.</returns>
    Task<Result<TrendStats>> GetHistoricalTrendsAsync(int days = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a comprehensive dashboard summary with key metrics.
    /// Provides high-level overview for main dashboard display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing dashboard summary statistics.</returns>
    Task<Result<DashboardStats>> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Processing statistics showing file counts by status.
/// </summary>
public class ProcessingStats
{
    /// <summary>Gets or sets the count of files by processing status.</summary>
    public Dictionary<FileStatus, int> StatusCounts { get; set; } = new();
    
    /// <summary>Gets or sets the total number of tracked files.</summary>
    public int TotalFiles { get; set; }
    
    /// <summary>Gets or sets the number of files processed today.</summary>
    public int ProcessedToday { get; set; }
    
    /// <summary>Gets or sets the average processing time in minutes.</summary>
    public double AverageProcessingTimeMinutes { get; set; }
}

/// <summary>
/// ML classification performance statistics.
/// </summary>
public class MLPerformanceStats
{
    /// <summary>Gets or sets the overall classification accuracy percentage.</summary>
    public double AccuracyPercentage { get; set; }
    
    /// <summary>Gets or sets the average confidence score.</summary>
    public double AverageConfidence { get; set; }
    
    /// <summary>Gets or sets confidence distribution by ranges.</summary>
    public Dictionary<string, int> ConfidenceDistribution { get; set; } = new();
    
    /// <summary>Gets or sets the number of low-confidence classifications.</summary>
    public int LowConfidenceCount { get; set; }
    
    /// <summary>Gets or sets the number of high-confidence classifications.</summary>
    public int HighConfidenceCount { get; set; }
}

/// <summary>
/// System health monitoring statistics.
/// </summary>
public class SystemHealthStats
{
    /// <summary>Gets or sets the error rate percentage.</summary>
    public double ErrorRatePercentage { get; set; }
    
    /// <summary>Gets or sets the retry rate percentage.</summary>
    public double RetryRatePercentage { get; set; }
    
    /// <summary>Gets or sets the current queue size.</summary>
    public int QueueSize { get; set; }
    
    /// <summary>Gets or sets the average response time in milliseconds.</summary>
    public double AverageResponseTimeMs { get; set; }
    
    /// <summary>Gets or sets the system uptime in hours.</summary>
    public double UptimeHours { get; set; }
    
    /// <summary>Gets or sets the number of successful file operations in the last 24 hours.</summary>
    public int SuccessfulFileOperations { get; set; }
    
    /// <summary>Gets or sets the number of failed file operations in the last 24 hours.</summary>
    public int FailedFileOperations { get; set; }
    
    /// <summary>Gets or sets the average time for file operations in milliseconds.</summary>
    public double AverageFileOperationTimeMs { get; set; }
    
    /// <summary>Gets or sets the number of files currently being processed.</summary>
    public int ActiveFileOperations { get; set; }
    
    /// <summary>Gets or sets the error rate percentage for file operations.</summary>
    public double FileOperationErrorRatePercentage { get; set; }
    
    /// <summary>Gets or sets the retry rate percentage for file operations.</summary>
    public double FileOperationRetryRatePercentage { get; set; }
}

/// <summary>
/// File processing activity statistics for a date range.
/// </summary>
public class ActivityStats
{
    /// <summary>Gets or sets the total files processed in the range.</summary>
    public int TotalProcessed { get; set; }
    
    /// <summary>Gets or sets the files processed per day.</summary>
    public Dictionary<DateTime, int> DailyProcessedCounts { get; set; } = new();
    
    /// <summary>Gets or sets the peak processing day.</summary>
    public DateTime PeakProcessingDate { get; set; }
    
    /// <summary>Gets or sets the peak files processed in a single day.</summary>
    public int PeakDailyCount { get; set; }
    
    /// <summary>Gets or sets the average files processed per day.</summary>
    public double AverageFilesPerDay { get; set; }
}

/// <summary>
/// Category distribution statistics.
/// </summary>
public class CategoryStats
{
    /// <summary>Gets or sets the file count by category.</summary>
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    
    /// <summary>Gets or sets the percentage distribution by category.</summary>
    public Dictionary<string, double> CategoryPercentages { get; set; } = new();
    
    /// <summary>Gets or sets the most popular category.</summary>
    public string MostPopularCategory { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the total number of categories.</summary>
    public int TotalCategories { get; set; }
}

/// <summary>
/// Processing throughput statistics.
/// </summary>
public class ThroughputStats
{
    /// <summary>Gets or sets the files processed per hour.</summary>
    public double FilesPerHour { get; set; }
    
    /// <summary>Gets or sets the average processing time per file in minutes.</summary>
    public double AverageProcessingTimeMinutes { get; set; }
    
    /// <summary>Gets or sets the current processing rate trend.</summary>
    public string ProcessingTrend { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the hourly throughput breakdown.</summary>
    public Dictionary<int, int> HourlyThroughput { get; set; } = new();
}

/// <summary>
/// Error analysis statistics.
/// </summary>
public class ErrorStats
{
    /// <summary>Gets or sets the total error count.</summary>
    public int TotalErrors { get; set; }
    
    /// <summary>Gets or sets error counts by error type.</summary>
    public Dictionary<string, int> ErrorTypeCounts { get; set; } = new();
    
    /// <summary>Gets or sets the most common error type.</summary>
    public string MostCommonError { get; set; } = string.Empty;
    
    /// <summary>Gets or sets files requiring manual intervention.</summary>
    public int FilesNeedingIntervention { get; set; }
    
    /// <summary>Gets or sets the error resolution rate percentage.</summary>
    public double ResolutionRatePercentage { get; set; }
}

/// <summary>
/// File size distribution statistics.
/// </summary>
public class FileSizeStats
{
    /// <summary>Gets or sets the average file size in MB.</summary>
    public double AverageFileSizeMB { get; set; }
    
    /// <summary>Gets or sets the total storage used in GB.</summary>
    public double TotalStorageGB { get; set; }
    
    /// <summary>Gets or sets file count by size ranges.</summary>
    public Dictionary<string, int> SizeDistribution { get; set; } = new();
    
    /// <summary>Gets or sets the largest file size in MB.</summary>
    public double LargestFileSizeMB { get; set; }
    
    /// <summary>Gets or sets the smallest file size in MB.</summary>
    public double SmallestFileSizeMB { get; set; }
}

/// <summary>
/// Historical trend analysis statistics.
/// </summary>
public class TrendStats
{
    /// <summary>Gets or sets daily processing trends over time.</summary>
    public Dictionary<DateTime, int> DailyProcessingTrend { get; set; } = new();
    
    /// <summary>Gets or sets daily error rate trends.</summary>
    public Dictionary<DateTime, double> ErrorRateTrend { get; set; } = new();
    
    /// <summary>Gets or sets the processing trend direction (increasing, decreasing, stable).</summary>
    public string TrendDirection { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the growth rate percentage.</summary>
    public double GrowthRatePercentage { get; set; }
}

/// <summary>
/// Comprehensive dashboard statistics summary.
/// </summary>
public class DashboardStats
{
    /// <summary>Gets or sets the processing statistics.</summary>
    public ProcessingStats Processing { get; set; } = new();
    
    /// <summary>Gets or sets the ML performance statistics.</summary>
    public MLPerformanceStats MLPerformance { get; set; } = new();
    
    /// <summary>Gets or sets the system health statistics.</summary>
    public SystemHealthStats SystemHealth { get; set; } = new();
    
    /// <summary>Gets or sets the recent activity summary.</summary>
    public ActivityStats RecentActivity { get; set; } = new();
    
    /// <summary>Gets or sets the timestamp when stats were generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}