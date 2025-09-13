using MediaButler.Core.Common;
using MediaButler.Core.Enums;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services;

/// <summary>
/// Service implementation for statistics and monitoring data in the MediaButler system.
/// Provides aggregated metrics and analytics following "Simple Made Easy" principles.
/// </summary>
public class StatsService : IStatsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StatsService> _logger;

    public StatsService(IUnitOfWork unitOfWork, ILogger<StatsService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ProcessingStats>> GetProcessingStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allFiles = await _unitOfWork.TrackedFiles.GetAllAsync(cancellationToken);

            var today = DateTime.UtcNow.Date;
            var processedToday = allFiles.Where(f => f.LastUpdateDate.Date == today);

            var stats = new ProcessingStats
            {
                StatusCounts = allFiles
                    .GroupBy(f => f.Status)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TotalFiles = allFiles.Count(),
                ProcessedToday = processedToday.Count(),
                AverageProcessingTimeMinutes = CalculateAverageProcessingTime(allFiles)
            };

            return Result<ProcessingStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing statistics");
            return Result<ProcessingStats>.Failure($"Failed to retrieve processing statistics: {ex.Message}");
        }
    }

    public async Task<Result<MLPerformanceStats>> GetMLPerformanceStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var classifiedFiles = await _unitOfWork.TrackedFiles.FindAsync(
                f => f.Status != FileStatus.New && f.Confidence > 0, cancellationToken);

            if (!classifiedFiles.Any())
            {
                return Result<MLPerformanceStats>.Success(new MLPerformanceStats());
            }

            var stats = new MLPerformanceStats
            {
                AverageConfidence = (double)classifiedFiles.Average(f => f.Confidence),
                ConfidenceDistribution = BuildConfidenceDistribution(classifiedFiles),
                LowConfidenceCount = classifiedFiles.Count(f => f.Confidence < 0.6m),
                HighConfidenceCount = classifiedFiles.Count(f => f.Confidence >= 0.8m)
            };

            // Calculate accuracy based on confirmed vs suggested categories
            var confirmedFiles = classifiedFiles.Where(f => !string.IsNullOrEmpty(f.Category));
            if (confirmedFiles.Any())
            {
                var accurateClassifications = confirmedFiles.Count(f => 
                    !string.IsNullOrEmpty(f.SuggestedCategory) && 
                    string.Equals(f.Category, f.SuggestedCategory, StringComparison.OrdinalIgnoreCase));
                
                stats.AccuracyPercentage = (double)accurateClassifications / confirmedFiles.Count() * 100;
            }

            return Result<MLPerformanceStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ML performance statistics");
            return Result<MLPerformanceStats>.Failure($"Failed to retrieve ML performance statistics: {ex.Message}");
        }
    }

    public async Task<Result<SystemHealthStats>> GetSystemHealthStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allFiles = await _unitOfWork.TrackedFiles.GetAllAsync(cancellationToken);

            var filesWithErrors = allFiles.Where(f => f.Status == FileStatus.Error || f.RetryCount > 0);
            var filesInProgress = allFiles.Where(f => f.Status == FileStatus.Processing || f.Status == FileStatus.Moving);
            
            // Calculate file operation metrics for the last 24 hours
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var recentFiles = allFiles.Where(f => f.LastUpdateDate >= yesterday);
            var successfulOperations = recentFiles.Where(f => f.Status == FileStatus.Moved || f.Status == FileStatus.ReadyToMove);
            var failedOperations = recentFiles.Where(f => f.Status == FileStatus.Error);
            var totalRecentOperations = recentFiles.Count();

            var stats = new SystemHealthStats
            {
                ErrorRatePercentage = allFiles.Any() ? (double)filesWithErrors.Count() / allFiles.Count() * 100 : 0,
                RetryRatePercentage = allFiles.Any() ? (double)allFiles.Count(f => f.RetryCount > 0) / allFiles.Count() * 100 : 0,
                QueueSize = filesInProgress.Count(),
                AverageResponseTimeMs = CalculateAverageResponseTime(allFiles),
                UptimeHours = GetSystemUptimeHours(),
                
                // New file operation metrics
                SuccessfulFileOperations = successfulOperations.Count(),
                FailedFileOperations = failedOperations.Count(),
                ActiveFileOperations = filesInProgress.Count(),
                AverageFileOperationTimeMs = CalculateAverageFileOperationTime(recentFiles),
                FileOperationErrorRatePercentage = totalRecentOperations > 0 
                    ? (double)failedOperations.Count() / totalRecentOperations * 100 
                    : 0,
                FileOperationRetryRatePercentage = totalRecentOperations > 0 
                    ? (double)recentFiles.Count(f => f.RetryCount > 0) / totalRecentOperations * 100 
                    : 0
            };

            return Result<SystemHealthStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system health statistics");
            return Result<SystemHealthStats>.Failure($"Failed to retrieve system health statistics: {ex.Message}");
        }
    }

    public async Task<Result<ActivityStats>> GetActivityStatsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return Result<ActivityStats>.Failure("Start date must be before or equal to end date");

        try
        {
            var processedFiles = await _unitOfWork.TrackedFiles.FindAsync(
                f => f.LastUpdateDate >= startDate && f.LastUpdateDate <= endDate.AddDays(1), cancellationToken);

            var dailyCounts = processedFiles
                .GroupBy(f => f.LastUpdateDate.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var peakDay = dailyCounts.Any() ? dailyCounts.OrderByDescending(kvp => kvp.Value).First() : new KeyValuePair<DateTime, int>();

            var totalDays = (endDate - startDate).TotalDays + 1;
            
            var stats = new ActivityStats
            {
                TotalProcessed = processedFiles.Count(),
                DailyProcessedCounts = dailyCounts,
                PeakProcessingDate = peakDay.Key,
                PeakDailyCount = peakDay.Value,
                AverageFilesPerDay = totalDays > 0 ? processedFiles.Count() / totalDays : 0
            };

            return Result<ActivityStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get activity statistics for date range {StartDate} to {EndDate}", startDate, endDate);
            return Result<ActivityStats>.Failure($"Failed to retrieve activity statistics: {ex.Message}");
        }
    }

    public async Task<Result<CategoryStats>> GetCategoryDistributionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var categorizedFiles = await _unitOfWork.TrackedFiles.FindAsync(
                f => !string.IsNullOrEmpty(f.Category), cancellationToken);

            if (!categorizedFiles.Any())
            {
                return Result<CategoryStats>.Success(new CategoryStats());
            }

            var categoryCounts = categorizedFiles
                .GroupBy(f => f.Category)
                .ToDictionary(g => g.Key!, g => g.Count());

            var totalFiles = categorizedFiles.Count();
            var categoryPercentages = categoryCounts
                .ToDictionary(kvp => kvp.Key, kvp => (double)kvp.Value / totalFiles * 100);

            var mostPopular = categoryCounts.OrderByDescending(kvp => kvp.Value).First();

            var stats = new CategoryStats
            {
                CategoryCounts = categoryCounts,
                CategoryPercentages = categoryPercentages,
                MostPopularCategory = mostPopular.Key,
                TotalCategories = categoryCounts.Count
            };

            return Result<CategoryStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get category distribution statistics");
            return Result<CategoryStats>.Failure($"Failed to retrieve category distribution: {ex.Message}");
        }
    }

    public async Task<Result<ThroughputStats>> GetThroughputStatsAsync(int withinHours = 24, CancellationToken cancellationToken = default)
    {
        if (withinHours <= 0)
            return Result<ThroughputStats>.Failure("Hours must be greater than 0");

        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-withinHours);
            
            var recentFiles = await _unitOfWork.TrackedFiles.FindAsync(
                f => f.LastUpdateDate >= cutoffTime, cancellationToken);

            var hourlyBreakdown = recentFiles
                .GroupBy(f => f.LastUpdateDate.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            var filesPerHour = (double)recentFiles.Count() / withinHours;
            var averageProcessingTime = CalculateAverageProcessingTime(recentFiles);

            var trend = CalculateProcessingTrend(recentFiles, withinHours);

            var stats = new ThroughputStats
            {
                FilesPerHour = filesPerHour,
                AverageProcessingTimeMinutes = averageProcessingTime,
                ProcessingTrend = trend,
                HourlyThroughput = hourlyBreakdown
            };

            return Result<ThroughputStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get throughput statistics for {Hours} hours", withinHours);
            return Result<ThroughputStats>.Failure($"Failed to retrieve throughput statistics: {ex.Message}");
        }
    }

    public async Task<Result<ErrorStats>> GetErrorAnalysisAsync(int withinDays = 7, CancellationToken cancellationToken = default)
    {
        if (withinDays <= 0)
            return Result<ErrorStats>.Failure("Days must be greater than 0");

        try
        {
            var cutoffTime = DateTime.UtcNow.AddDays(-withinDays);
            
            var filesWithErrors = await _unitOfWork.TrackedFiles.FindAsync(
                f => (f.Status == FileStatus.Error || f.RetryCount > 0) && f.LastUpdateDate >= cutoffTime, cancellationToken);

            var errorTypeCounts = new Dictionary<string, int>();
            var totalErrors = 0;
            var filesNeedingIntervention = 0;

            foreach (var file in filesWithErrors)
            {
                if (!string.IsNullOrEmpty(file.LastError))
                {
                    var errorType = ExtractErrorType(file.LastError);
                    errorTypeCounts.TryGetValue(errorType, out var currentCount);
                    errorTypeCounts[errorType] = currentCount + 1;
                    totalErrors++;
                }

                if (file.RetryCount >= 3 || file.Status == FileStatus.Error)
                {
                    filesNeedingIntervention++;
                }
            }

            var mostCommonError = errorTypeCounts.Any() 
                ? errorTypeCounts.OrderByDescending(kvp => kvp.Value).First().Key 
                : "None";

            var totalFilesWithHistory = await _unitOfWork.TrackedFiles.FindAsync(
                f => f.LastUpdateDate >= cutoffTime, cancellationToken);
            
            var resolvedFiles = totalFilesWithHistory.Count(f => f.RetryCount > 0 && f.Status == FileStatus.Moved);
            var resolutionRate = filesWithErrors.Any() ? (double)resolvedFiles / filesWithErrors.Count() * 100 : 100;

            var stats = new ErrorStats
            {
                TotalErrors = totalErrors,
                ErrorTypeCounts = errorTypeCounts,
                MostCommonError = mostCommonError,
                FilesNeedingIntervention = filesNeedingIntervention,
                ResolutionRatePercentage = resolutionRate
            };

            return Result<ErrorStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error analysis for {Days} days", withinDays);
            return Result<ErrorStats>.Failure($"Failed to retrieve error analysis: {ex.Message}");
        }
    }

    public async Task<Result<FileSizeStats>> GetFileSizeDistributionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allFiles = await _unitOfWork.TrackedFiles.FindAsync(
                f => f.FileSize > 0, cancellationToken);

            if (!allFiles.Any())
            {
                return Result<FileSizeStats>.Success(new FileSizeStats());
            }

            var fileSizesMB = allFiles.Select(f => f.FileSize / (1024.0 * 1024.0)).ToList();
            
            var sizeDistribution = new Dictionary<string, int>
            {
                ["< 100 MB"] = fileSizesMB.Count(s => s < 100),
                ["100 MB - 500 MB"] = fileSizesMB.Count(s => s >= 100 && s < 500),
                ["500 MB - 1 GB"] = fileSizesMB.Count(s => s >= 500 && s < 1024),
                ["1 GB - 2 GB"] = fileSizesMB.Count(s => s >= 1024 && s < 2048),
                ["2 GB - 5 GB"] = fileSizesMB.Count(s => s >= 2048 && s < 5120),
                ["> 5 GB"] = fileSizesMB.Count(s => s >= 5120)
            };

            var stats = new FileSizeStats
            {
                AverageFileSizeMB = fileSizesMB.Average(),
                TotalStorageGB = allFiles.Sum(f => f.FileSize) / (1024.0 * 1024.0 * 1024.0),
                SizeDistribution = sizeDistribution,
                LargestFileSizeMB = fileSizesMB.Max(),
                SmallestFileSizeMB = fileSizesMB.Min()
            };

            return Result<FileSizeStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file size distribution statistics");
            return Result<FileSizeStats>.Failure($"Failed to retrieve file size distribution: {ex.Message}");
        }
    }

    public async Task<Result<TrendStats>> GetHistoricalTrendsAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        if (days <= 0)
            return Result<TrendStats>.Failure("Days must be greater than 0");

        try
        {
            var startDate = DateTime.UtcNow.AddDays(-days).Date;
            var endDate = DateTime.UtcNow.Date;

            var historicalFiles = await _unitOfWork.TrackedFiles.FindAsync(
                f => f.LastUpdateDate >= startDate, cancellationToken);

            var dailyProcessingTrend = new Dictionary<DateTime, int>();
            var errorRateTrend = new Dictionary<DateTime, double>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayFiles = historicalFiles.Where(f => f.LastUpdateDate.Date == date);
                var dayErrors = dayFiles.Where(f => f.Status == FileStatus.Error || f.RetryCount > 0);

                dailyProcessingTrend[date] = dayFiles.Count();
                errorRateTrend[date] = dayFiles.Any() ? (double)dayErrors.Count() / dayFiles.Count() * 100 : 0;
            }

            var trendDirection = CalculateTrendDirection(dailyProcessingTrend.Values.ToList());
            var growthRate = CalculateGrowthRate(dailyProcessingTrend.Values.ToList());

            var stats = new TrendStats
            {
                DailyProcessingTrend = dailyProcessingTrend,
                ErrorRateTrend = errorRateTrend,
                TrendDirection = trendDirection,
                GrowthRatePercentage = growthRate
            };

            return Result<TrendStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get historical trends for {Days} days", days);
            return Result<TrendStats>.Failure($"Failed to retrieve historical trends: {ex.Message}");
        }
    }

    public async Task<Result<DashboardStats>> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Execute all statistics queries concurrently for better performance
            var processingTask = GetProcessingStatsAsync(cancellationToken);
            var mlPerformanceTask = GetMLPerformanceStatsAsync(cancellationToken);
            var systemHealthTask = GetSystemHealthStatsAsync(cancellationToken);
            var activityTask = GetActivityStatsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, cancellationToken);

            await Task.WhenAll(processingTask, mlPerformanceTask, systemHealthTask, activityTask);

            if (!processingTask.Result.IsSuccess)
                return Result<DashboardStats>.Failure($"Processing stats failed: {processingTask.Result.Error}");

            if (!mlPerformanceTask.Result.IsSuccess)
                return Result<DashboardStats>.Failure($"ML performance stats failed: {mlPerformanceTask.Result.Error}");

            if (!systemHealthTask.Result.IsSuccess)
                return Result<DashboardStats>.Failure($"System health stats failed: {systemHealthTask.Result.Error}");

            if (!activityTask.Result.IsSuccess)
                return Result<DashboardStats>.Failure($"Activity stats failed: {activityTask.Result.Error}");

            var dashboardStats = new DashboardStats
            {
                Processing = processingTask.Result.Value,
                MLPerformance = mlPerformanceTask.Result.Value,
                SystemHealth = systemHealthTask.Result.Value,
                RecentActivity = activityTask.Result.Value,
                GeneratedAt = DateTime.UtcNow
            };

            return Result<DashboardStats>.Success(dashboardStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dashboard summary");
            return Result<DashboardStats>.Failure($"Failed to generate dashboard summary: {ex.Message}");
        }
    }

    #region Private Helper Methods

    private static double CalculateAverageProcessingTime(IEnumerable<Core.Entities.TrackedFile> files)
    {
        var processedFiles = files.Where(f => 
            f.Status == FileStatus.Moved && 
            f.ClassifiedAt.HasValue);

        if (!processedFiles.Any())
            return 0;

        var processingTimes = processedFiles.Select(f => 
            (f.ClassifiedAt!.Value - f.CreatedDate).TotalMinutes);

        return processingTimes.Average();
    }

    private static double CalculateAverageResponseTime(IEnumerable<Core.Entities.TrackedFile> files)
    {
        var recentFiles = files.Where(f => 
            f.LastUpdateDate >= DateTime.UtcNow.AddHours(-1) &&
            f.Status != FileStatus.New);

        if (!recentFiles.Any())
            return 0;

        var responseTimes = recentFiles.Select(f => 
            (f.LastUpdateDate - f.CreatedDate).TotalMilliseconds);

        return responseTimes.Average();
    }

    private static double GetSystemUptimeHours()
    {
        return Environment.TickCount64 / (1000.0 * 60.0 * 60.0);
    }

    private static Dictionary<string, int> BuildConfidenceDistribution(IEnumerable<Core.Entities.TrackedFile> classifiedFiles)
    {
        return new Dictionary<string, int>
        {
            ["Very Low (0-30%)"] = classifiedFiles.Count(f => f.Confidence < 0.3m),
            ["Low (30-60%)"] = classifiedFiles.Count(f => f.Confidence >= 0.3m && f.Confidence < 0.6m),
            ["Medium (60-80%)"] = classifiedFiles.Count(f => f.Confidence >= 0.6m && f.Confidence < 0.8m),
            ["High (80-95%)"] = classifiedFiles.Count(f => f.Confidence >= 0.8m && f.Confidence < 0.95m),
            ["Very High (95%+)"] = classifiedFiles.Count(f => f.Confidence >= 0.95m)
        };
    }

    private static string CalculateProcessingTrend(IEnumerable<Core.Entities.TrackedFile> files, int withinHours)
    {
        var sortedFiles = files.OrderBy(f => f.LastUpdateDate).ToList();
        
        if (sortedFiles.Count < 2)
            return "Stable";

        var firstHalf = sortedFiles.Take(sortedFiles.Count / 2);
        var secondHalf = sortedFiles.Skip(sortedFiles.Count / 2);

        var firstHalfAvg = firstHalf.Count() / (double)(withinHours / 2);
        var secondHalfAvg = secondHalf.Count() / (double)(withinHours / 2);

        var changePercent = (secondHalfAvg - firstHalfAvg) / firstHalfAvg * 100;

        return changePercent switch
        {
            > 10 => "Increasing",
            < -10 => "Decreasing",
            _ => "Stable"
        };
    }

    private static string ExtractErrorType(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "Unknown";

        var lowerError = errorMessage.ToLowerInvariant();

        return lowerError switch
        {
            _ when lowerError.Contains("file not found") || lowerError.Contains("path") => "File Not Found",
            _ when lowerError.Contains("access") || lowerError.Contains("permission") => "Access Denied",
            _ when lowerError.Contains("disk") || lowerError.Contains("space") => "Disk Space",
            _ when lowerError.Contains("network") || lowerError.Contains("connection") => "Network Error",
            _ when lowerError.Contains("timeout") => "Timeout",
            _ when lowerError.Contains("classification") || lowerError.Contains("ml") => "ML Classification",
            _ => "Other"
        };
    }

    private static string CalculateTrendDirection(List<int> dailyValues)
    {
        if (dailyValues.Count < 3)
            return "Stable";

        var firstThird = dailyValues.Take(dailyValues.Count / 3).Average();
        var lastThird = dailyValues.Skip(dailyValues.Count * 2 / 3).Average();

        var changePercent = (lastThird - firstThird) / firstThird * 100;

        return changePercent switch
        {
            > 15 => "Increasing",
            < -15 => "Decreasing",
            _ => "Stable"
        };
    }

    private static double CalculateGrowthRate(List<int> dailyValues)
    {
        if (dailyValues.Count < 2)
            return 0;

        var firstValue = dailyValues.First();
        var lastValue = dailyValues.Last();

        if (firstValue == 0)
            return lastValue > 0 ? 100 : 0;

        return (double)(lastValue - firstValue) / firstValue * 100;
    }

    /// <summary>
    /// Calculates the average file operation time based on file processing duration.
    /// Uses the time between file creation and completion for moved files.
    /// </summary>
    /// <param name="files">Collection of tracked files to analyze</param>
    /// <returns>Average file operation time in milliseconds</returns>
    private static double CalculateAverageFileOperationTime(IEnumerable<Core.Entities.TrackedFile> files)
    {
        var completedFiles = files.Where(f => 
            f.Status == FileStatus.Moved && 
            f.MovedAt.HasValue);
            
        if (!completedFiles.Any())
            return 0;
            
        var operationTimes = completedFiles.Select(f => 
            (f.MovedAt!.Value - f.CreatedDate).TotalMilliseconds);
            
        return operationTimes.Average();
    }

    #endregion
}