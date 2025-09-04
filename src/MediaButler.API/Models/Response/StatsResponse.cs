using System.Text.Json.Serialization;

namespace MediaButler.API.Models.Response;

/// <summary>
/// Comprehensive statistics response containing all MediaButler system metrics.
/// Provides aggregated data for monitoring dashboards and system health reporting
/// following "Simple Made Easy" principles by separating different statistical concerns.
/// </summary>
/// <remarks>
/// This response aggregates various statistical data points without complecting
/// different aspects of system performance, processing metrics, and user activity.
/// Each statistical category maintains its own responsibility and data structure.
/// </remarks>
public class StatsResponse
{
    /// <summary>
    /// Gets or sets general processing statistics.
    /// Provides overview of file processing performance and status.
    /// </summary>
    /// <value>Processing-related statistics.</value>
    public ProcessingStats Processing { get; set; } = new();

    /// <summary>
    /// Gets or sets machine learning classification statistics.
    /// Tracks ML model performance and accuracy metrics.
    /// </summary>
    /// <value>ML classification performance statistics.</value>
    public MLStats MachineLearning { get; set; } = new();

    /// <summary>
    /// Gets or sets system health and resource statistics.
    /// Monitors system resource usage and health indicators.
    /// </summary>
    /// <value>System health and resource metrics.</value>
    public SystemHealthStats SystemHealth { get; set; } = new();

    /// <summary>
    /// Gets or sets user activity and interaction statistics.
    /// Tracks user engagement with the system.
    /// </summary>
    /// <value>User activity metrics.</value>
    public ActivityStats Activity { get; set; } = new();

    /// <summary>
    /// Gets or sets storage and file size distribution statistics.
    /// Provides insights into storage usage patterns.
    /// </summary>
    /// <value>Storage utilization statistics.</value>
    public StorageStats Storage { get; set; } = new();

    /// <summary>
    /// Gets or sets performance timing and throughput statistics.
    /// Monitors system performance characteristics.
    /// </summary>
    /// <value>Performance metrics.</value>
    public PerformanceStats Performance { get; set; } = new();

    /// <summary>
    /// Gets or sets when these statistics were generated.
    /// Provides timestamp context for the statistical snapshot.
    /// </summary>
    /// <value>The UTC date and time when statistics were collected.</value>
    /// <example>2024-01-15T15:45:00Z</example>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the time range these statistics cover.
    /// Indicates the period over which statistics were calculated.
    /// </summary>
    /// <value>The time span covered by these statistics.</value>
    /// <example>30 days</example>
    public TimeSpan CoveragePeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets whether the system is operating within normal parameters.
    /// Computed based on various health indicators across all statistical categories.
    /// </summary>
    /// <value>True if all systems are operating normally.</value>
    [JsonInclude]
    public bool IsHealthy => SystemHealth.IsHealthy && 
                            Performance.IsWithinTargets && 
                            Processing.ErrorRate < 5.0m;

    /// <summary>
    /// Gets an overall health score from 0-100.
    /// Provides a single metric for system health assessment.
    /// </summary>
    /// <value>A health score between 0 (critical) and 100 (excellent).</value>
    [JsonInclude]
    public int HealthScore => CalculateHealthScore();

    /// <summary>
    /// Calculates the overall system health score based on various metrics.
    /// </summary>
    /// <returns>A health score between 0 and 100.</returns>
    private int CalculateHealthScore()
    {
        var scores = new[]
        {
            SystemHealth.MemoryHealthScore,
            SystemHealth.StorageHealthScore,
            Performance.ResponseTimeScore,
            (int)(100 - Math.Min(Processing.ErrorRate, 100))
        };

        return (int)scores.Average();
    }
}

/// <summary>
/// File processing statistics and metrics.
/// Tracks the performance and status of file processing operations.
/// </summary>
public class ProcessingStats
{
    /// <summary>
    /// Gets or sets the total number of files processed.
    /// </summary>
    /// <value>The total count of files that have been processed by the system.</value>
    /// <example>1247</example>
    public int TotalFilesProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of files successfully organized.
    /// </summary>
    /// <value>The count of files that completed the full organization workflow.</value>
    /// <example>1198</example>
    public int FilesSuccessfullyOrganized { get; set; }

    /// <summary>
    /// Gets or sets the number of files currently awaiting user review.
    /// </summary>
    /// <value>The count of files in classified state waiting for confirmation.</value>
    /// <example>23</example>
    public int FilesPendingReview { get; set; }

    /// <summary>
    /// Gets or sets the number of files that encountered errors.
    /// </summary>
    /// <value>The count of files in error state requiring attention.</value>
    /// <example>26</example>
    public int FilesWithErrors { get; set; }

    /// <summary>
    /// Gets or sets the current error rate as a percentage.
    /// </summary>
    /// <value>The percentage of processed files that resulted in errors.</value>
    /// <example>2.1</example>
    public decimal ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the average processing time per file in milliseconds.
    /// </summary>
    /// <value>The mean time taken to process a file from discovery to classification.</value>
    /// <example>850</example>
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of files processed in the last 24 hours.
    /// </summary>
    /// <value>Recent processing activity count.</value>
    /// <example>47</example>
    public int FilesProcessedLast24Hours { get; set; }

    /// <summary>
    /// Gets or sets the current processing throughput (files per hour).
    /// </summary>
    /// <value>The rate of file processing over recent time period.</value>
    /// <example>12.3</example>
    public double ProcessingThroughput { get; set; }

    /// <summary>
    /// Gets or sets breakdown of files by status.
    /// </summary>
    /// <value>A dictionary mapping file status to count.</value>
    public Dictionary<string, int> FilesByStatus { get; set; } = new();

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    /// <value>Percentage of files successfully processed without errors.</value>
    [JsonInclude]
    public decimal SuccessRate => TotalFilesProcessed > 0 
        ? (decimal)(FilesSuccessfullyOrganized * 100) / TotalFilesProcessed 
        : 0;
}

/// <summary>
/// Machine learning classification performance statistics.
/// Tracks the accuracy and effectiveness of the ML classification system.
/// </summary>
public class MLStats
{
    /// <summary>
    /// Gets or sets the overall ML classification accuracy percentage.
    /// </summary>
    /// <value>The percentage of correct classifications based on user feedback.</value>
    /// <example>87.4</example>
    public decimal ClassificationAccuracy { get; set; }

    /// <summary>
    /// Gets or sets the average confidence score of ML classifications.
    /// </summary>
    /// <value>The mean confidence score across all classifications.</value>
    /// <example>0.79</example>
    public decimal AverageConfidence { get; set; }

    /// <summary>
    /// Gets or sets the number of unique categories identified.
    /// </summary>
    /// <value>The count of distinct categories discovered by the ML system.</value>
    /// <example>156</example>
    public int UniqueCategories { get; set; }

    /// <summary>
    /// Gets or sets the most frequently classified category.
    /// </summary>
    /// <value>The category name with the highest number of classifications.</value>
    /// <example>THE OFFICE</example>
    public string? MostCommonCategory { get; set; }

    /// <summary>
    /// Gets or sets category-specific accuracy breakdown.
    /// </summary>
    /// <value>A dictionary mapping category names to their accuracy percentages.</value>
    public Dictionary<string, decimal> AccuracyByCategory { get; set; } = new();

    /// <summary>
    /// Gets or sets confidence score distribution.
    /// </summary>
    /// <value>A dictionary mapping confidence ranges to file counts.</value>
    /// <example>{"High (80-100%)": 425, "Medium (60-79%)": 123, ...}</example>
    public Dictionary<string, int> ConfidenceDistribution { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of user corrections to ML classifications.
    /// </summary>
    /// <value>Count of times users overrode ML suggestions.</value>
    /// <example>89</example>
    public int UserCorrections { get; set; }

    /// <summary>
    /// Gets or sets recent model performance trend.
    /// </summary>
    /// <value>Indicates whether model performance is improving, declining, or stable.</value>
    /// <example>Improving</example>
    public string PerformanceTrend { get; set; } = "Stable";

    /// <summary>
    /// Gets the user correction rate as a percentage.
    /// </summary>
    /// <value>Percentage of classifications that users modified.</value>
    [JsonInclude]
    public decimal CorrectionRate => UniqueCategories > 0 
        ? (decimal)(UserCorrections * 100) / UniqueCategories 
        : 0;
}

/// <summary>
/// System health and resource utilization statistics.
/// Monitors the operational health of the MediaButler system.
/// </summary>
public class SystemHealthStats
{
    /// <summary>
    /// Gets or sets current memory usage in MB.
    /// </summary>
    /// <value>The amount of memory currently being used by the application.</value>
    /// <example>245.7</example>
    public double MemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets maximum memory usage in MB over the monitoring period.
    /// </summary>
    /// <value>The peak memory usage observed.</value>
    /// <example>289.3</example>
    public double PeakMemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets available storage space in GB.
    /// </summary>
    /// <value>The amount of free storage space available.</value>
    /// <example>1250.4</example>
    public double AvailableStorageGB { get; set; }

    /// <summary>
    /// Gets or sets total storage space in GB.
    /// </summary>
    /// <value>The total storage capacity.</value>
    /// <example>2000.0</example>
    public double TotalStorageGB { get; set; }

    /// <summary>
    /// Gets or sets CPU usage percentage over the monitoring period.
    /// </summary>
    /// <value>The average CPU utilization percentage.</value>
    /// <example>23.7</example>
    public double CPUUsagePercentage { get; set; }

    /// <summary>
    /// Gets or sets system uptime in hours.
    /// </summary>
    /// <value>The number of hours the system has been running.</value>
    /// <example>168.5</example>
    public double UptimeHours { get; set; }

    /// <summary>
    /// Gets or sets the number of application restarts in the monitoring period.
    /// </summary>
    /// <value>Count of application restart events.</value>
    /// <example>2</example>
    public int RestartCount { get; set; }

    /// <summary>
    /// Gets or sets active background jobs count.
    /// </summary>
    /// <value>The number of background processing jobs currently running.</value>
    /// <example>3</example>
    public int ActiveBackgroundJobs { get; set; }

    /// <summary>
    /// Gets or sets database health status.
    /// </summary>
    /// <value>Indicates the operational status of the database connection.</value>
    /// <example>Healthy</example>
    public string DatabaseStatus { get; set; } = "Unknown";

    /// <summary>
    /// Gets whether the system is operating within healthy parameters.
    /// </summary>
    /// <value>True if all health indicators are within normal ranges.</value>
    [JsonInclude]
    public bool IsHealthy => MemoryUsageMB < 300 && 
                            StorageUsagePercentage < 85 && 
                            CPUUsagePercentage < 80;

    /// <summary>
    /// Gets the storage usage percentage.
    /// </summary>
    /// <value>Percentage of total storage currently used.</value>
    [JsonInclude]
    public double StorageUsagePercentage => TotalStorageGB > 0 
        ? ((TotalStorageGB - AvailableStorageGB) / TotalStorageGB) * 100 
        : 0;

    /// <summary>
    /// Gets the memory health score (0-100).
    /// </summary>
    /// <value>A score indicating memory usage health.</value>
    [JsonInclude]
    public int MemoryHealthScore => MemoryUsageMB switch
    {
        < 200 => 100,
        < 250 => 85,
        < 300 => 70,
        < 400 => 50,
        _ => 25
    };

    /// <summary>
    /// Gets the storage health score (0-100).
    /// </summary>
    /// <value>A score indicating storage usage health.</value>
    [JsonInclude]
    public int StorageHealthScore => StorageUsagePercentage switch
    {
        < 50 => 100,
        < 70 => 85,
        < 85 => 70,
        < 95 => 50,
        _ => 25
    };
}

/// <summary>
/// User activity and engagement statistics.
/// Tracks how users interact with the MediaButler system.
/// </summary>
public class ActivityStats
{
    /// <summary>
    /// Gets or sets the number of user confirmations in the monitoring period.
    /// </summary>
    /// <value>Count of files confirmed by users.</value>
    /// <example>234</example>
    public int UserConfirmations { get; set; }

    /// <summary>
    /// Gets or sets the number of category overrides by users.
    /// </summary>
    /// <value>Count of times users changed ML-suggested categories.</value>
    /// <example>45</example>
    public int CategoryOverrides { get; set; }

    /// <summary>
    /// Gets or sets the number of files manually ignored by users.
    /// </summary>
    /// <value>Count of files marked as ignored.</value>
    /// <example>12</example>
    public int FilesIgnored { get; set; }

    /// <summary>
    /// Gets or sets average time to user confirmation in hours.
    /// </summary>
    /// <value>Mean time between file classification and user review.</value>
    /// <example>4.7</example>
    public double AverageConfirmationTimeHours { get; set; }

    /// <summary>
    /// Gets or sets the most active day of the week for user activity.
    /// </summary>
    /// <value>The day when users are most active.</value>
    /// <example>Sunday</example>
    public string? MostActiveDay { get; set; }

    /// <summary>
    /// Gets or sets peak activity hour of the day (24-hour format).
    /// </summary>
    /// <value>The hour when most user activity occurs.</value>
    /// <example>19</example>
    public int PeakActivityHour { get; set; }

    /// <summary>
    /// Gets or sets user engagement level.
    /// </summary>
    /// <value>Overall assessment of user engagement with the system.</value>
    /// <example>High</example>
    public string EngagementLevel { get; set; } = "Medium";

    /// <summary>
    /// Gets the user acceptance rate for ML suggestions.
    /// </summary>
    /// <value>Percentage of ML suggestions accepted without modification.</value>
    [JsonInclude]
    public decimal MLAcceptanceRate => (UserConfirmations + CategoryOverrides) > 0 
        ? (decimal)(UserConfirmations * 100) / (UserConfirmations + CategoryOverrides) 
        : 0;
}

/// <summary>
/// Storage utilization and file size distribution statistics.
/// Provides insights into how storage space is being utilized.
/// </summary>
public class StorageStats
{
    /// <summary>
    /// Gets or sets total size of organized files in GB.
    /// </summary>
    /// <value>The cumulative size of all successfully organized files.</value>
    /// <example>1847.3</example>
    public double TotalOrganizedSizeGB { get; set; }

    /// <summary>
    /// Gets or sets average file size in MB.
    /// </summary>
    /// <value>The mean size of processed files.</value>
    /// <example>523.7</example>
    public double AverageFileSizeMB { get; set; }

    /// <summary>
    /// Gets or sets the largest file size encountered in GB.
    /// </summary>
    /// <value>The size of the largest file processed.</value>
    /// <example>15.6</example>
    public double LargestFileSizeGB { get; set; }

    /// <summary>
    /// Gets or sets file size distribution across different ranges.
    /// </summary>
    /// <value>A dictionary mapping size ranges to file counts.</value>
    /// <example>{"< 1GB": 450, "1-5GB": 320, "> 5GB": 45}</example>
    public Dictionary<string, int> FileSizeDistribution { get; set; } = new();

    /// <summary>
    /// Gets or sets storage usage by category.
    /// </summary>
    /// <value>A dictionary mapping categories to their total storage usage in GB.</value>
    public Dictionary<string, double> StorageByCategory { get; set; } = new();

    /// <summary>
    /// Gets or sets the category using the most storage space.
    /// </summary>
    /// <value>The category name with the highest storage utilization.</value>
    /// <example>GAME OF THRONES</example>
    public string? LargestCategory { get; set; }

    /// <summary>
    /// Gets or sets monthly storage growth rate in GB.
    /// </summary>
    /// <value>The average monthly increase in organized file storage.</value>
    /// <example>127.4</example>
    public double MonthlyGrowthRateGB { get; set; }

    /// <summary>
    /// Gets or sets estimated storage needed for pending files in GB.
    /// </summary>
    /// <value>Projected storage requirement for unprocessed files.</value>
    /// <example>45.8</example>
    public double PendingFilesStorageGB { get; set; }
}

/// <summary>
/// System performance timing and throughput statistics.
/// Monitors the responsiveness and efficiency of system operations.
/// </summary>
public class PerformanceStats
{
    /// <summary>
    /// Gets or sets average API response time in milliseconds.
    /// </summary>
    /// <value>Mean response time for API requests.</value>
    /// <example>45.7</example>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets 95th percentile API response time in milliseconds.
    /// </summary>
    /// <value>Response time below which 95% of requests fall.</value>
    /// <example>89.2</example>
    public double P95ResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets average database query time in milliseconds.
    /// </summary>
    /// <value>Mean execution time for database queries.</value>
    /// <example>12.3</example>
    public double AverageQueryTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of slow queries (>100ms) detected.
    /// </summary>
    /// <value>Count of database queries exceeding performance thresholds.</value>
    /// <example>8</example>
    public int SlowQueriesCount { get; set; }

    /// <summary>
    /// Gets or sets file processing throughput (files per minute).
    /// </summary>
    /// <value>The rate of file processing operations.</value>
    /// <example>2.3</example>
    public double ProcessingThroughputPerMinute { get; set; }

    /// <summary>
    /// Gets or sets ML classification speed (classifications per second).
    /// </summary>
    /// <value>The rate of ML classification operations.</value>
    /// <example>1.8</example>
    public double ClassificationSpeed { get; set; }

    /// <summary>
    /// Gets or sets peak memory usage during operations in MB.
    /// </summary>
    /// <value>Maximum memory usage observed during processing.</value>
    /// <example>267.4</example>
    public double PeakMemoryUsageMB { get; set; }

    /// <summary>
    /// Gets whether performance is within target parameters.
    /// </summary>
    /// <value>True if all performance metrics meet target thresholds.</value>
    [JsonInclude]
    public bool IsWithinTargets => AverageResponseTimeMs < 100 && 
                                  AverageQueryTimeMs < 50 && 
                                  PeakMemoryUsageMB < 300;

    /// <summary>
    /// Gets the response time performance score (0-100).
    /// </summary>
    /// <value>A score indicating API response time performance.</value>
    [JsonInclude]
    public int ResponseTimeScore => AverageResponseTimeMs switch
    {
        < 50 => 100,
        < 75 => 85,
        < 100 => 70,
        < 150 => 50,
        _ => 25
    };
}

/// <summary>
/// Historical trending data for key metrics.
/// Provides time-series data for monitoring trends and patterns.
/// </summary>
public class TrendData
{
    /// <summary>
    /// Gets or sets the metric name being tracked.
    /// </summary>
    /// <value>The name of the metric (e.g., "ProcessingThroughput").</value>
    /// <example>ProcessingThroughput</example>
    public required string MetricName { get; set; }

    /// <summary>
    /// Gets or sets time-series data points.
    /// </summary>
    /// <value>A collection of timestamped data points.</value>
    public IReadOnlyCollection<DataPoint> DataPoints { get; set; } = Array.Empty<DataPoint>();

    /// <summary>
    /// Gets or sets the trend direction.
    /// </summary>
    /// <value>Indicates whether the trend is improving, declining, or stable.</value>
    /// <example>Improving</example>
    public string TrendDirection { get; set; } = "Stable";

    /// <summary>
    /// Gets or sets the percentage change over the monitoring period.
    /// </summary>
    /// <value>The percentage change from start to end of period.</value>
    /// <example>12.5</example>
    public decimal PercentageChange { get; set; }
}

/// <summary>
/// Individual data point in a time series.
/// Represents a single measurement at a specific time.
/// </summary>
public class DataPoint
{
    /// <summary>
    /// Gets or sets the timestamp for this data point.
    /// </summary>
    /// <value>The UTC date and time when this measurement was taken.</value>
    /// <example>2024-01-15T10:00:00Z</example>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the measured value at this timestamp.
    /// </summary>
    /// <value>The numeric value of the measurement.</value>
    /// <example>23.7</example>
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets optional label for this data point.
    /// </summary>
    /// <value>A descriptive label for this measurement.</value>
    /// <example>Peak Hour</example>
    public string? Label { get; set; }
}