using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CoreLogLevel = MediaButler.Core.Enums.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Repository implementation for ProcessingLog entities providing audit trail operations.
/// Implements comprehensive logging with BaseEntity support following "Simple Made Easy" principles.
/// </summary>
public class ProcessingLogRepository : IProcessingLogRepository
{
    private readonly MediaButlerDbContext _context;
    private readonly ILogger<ProcessingLogRepository> _logger;

    public ProcessingLogRepository(
        MediaButlerDbContext context,
        ILogger<ProcessingLogRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds a processing log entry to the audit trail.
    /// </summary>
    public async Task<Result> AddAsync(ProcessingLog processingLog, CancellationToken cancellationToken = default)
    {
        try
        {
            if (processingLog == null)
            {
                return Result.Failure("Processing log cannot be null");
            }

            // Ensure audit properties are set (BaseEntity behavior)
            if (processingLog.CreatedDate == default)
            {
                processingLog.CreatedDate = DateTime.UtcNow;
            }

            processingLog.LastUpdateDate = DateTime.UtcNow;
            processingLog.IsActive = true;

            await _context.ProcessingLogs.AddAsync(processingLog, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Added processing log entry: {FileHash} - {Level} - {Category}", 
                processingLog.FileHash, processingLog.Level, processingLog.Category);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add processing log entry for file {FileHash}", 
                processingLog?.FileHash ?? "unknown");
            return Result.Failure($"Failed to add processing log: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds multiple processing log entries efficiently.
    /// </summary>
    public async Task<Result> AddRangeAsync(IEnumerable<ProcessingLog> processingLogs, CancellationToken cancellationToken = default)
    {
        try
        {
            if (processingLogs == null)
            {
                return Result.Failure("Processing logs collection cannot be null");
            }

            var logsArray = processingLogs.ToArray();
            if (logsArray.Length == 0)
            {
                return Result.Success();
            }

            var utcNow = DateTime.UtcNow;

            // Ensure audit properties are set for all entries
            foreach (var log in logsArray)
            {
                if (log.CreatedDate == default)
                {
                    log.CreatedDate = utcNow;
                }
                log.LastUpdateDate = utcNow;
                log.IsActive = true;
            }

            await _context.ProcessingLogs.AddRangeAsync(logsArray, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Added {Count} processing log entries", logsArray.Length);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add processing log entries");
            return Result.Failure($"Failed to add processing logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets processing logs for a specific file hash.
    /// </summary>
    public async Task<Result<IEnumerable<ProcessingLog>>> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileHash))
            {
                return Result<IEnumerable<ProcessingLog>>.Failure("File hash cannot be null or empty");
            }

            var logs = await _context.ProcessingLogs
                .Where(log => log.FileHash == fileHash && log.IsActive)
                .OrderByDescending(log => log.CreatedDate)
                .ToListAsync(cancellationToken);

            return Result<IEnumerable<ProcessingLog>>.Success(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing logs for file {FileHash}", fileHash);
            return Result<IEnumerable<ProcessingLog>>.Failure($"Failed to get processing logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets processing logs filtered by level and time range.
    /// </summary>
    public async Task<Result<IEnumerable<ProcessingLog>>> GetByLevelAsync(
        CoreLogLevel level,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.ProcessingLogs
                .Where(log => log.Level >= level && log.IsActive);

            if (fromDate.HasValue)
            {
                query = query.Where(log => log.CreatedDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(log => log.CreatedDate <= toDate.Value);
            }

            var logs = await query
                .OrderByDescending(log => log.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return Result<IEnumerable<ProcessingLog>>.Success(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing logs by level {Level}", level);
            return Result<IEnumerable<ProcessingLog>>.Failure($"Failed to get processing logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets processing logs filtered by category and time range.
    /// </summary>
    public async Task<Result<IEnumerable<ProcessingLog>>> GetByCategoryAsync(
        string category,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Result<IEnumerable<ProcessingLog>>.Failure("Category cannot be null or empty");
            }

            var query = _context.ProcessingLogs
                .Where(log => log.Category == category && log.IsActive);

            if (fromDate.HasValue)
            {
                query = query.Where(log => log.CreatedDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(log => log.CreatedDate <= toDate.Value);
            }

            var logs = await query
                .OrderByDescending(log => log.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return Result<IEnumerable<ProcessingLog>>.Success(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing logs by category {Category}", category);
            return Result<IEnumerable<ProcessingLog>>.Failure($"Failed to get processing logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets recent processing logs ordered by creation date.
    /// </summary>
    public async Task<Result<IEnumerable<ProcessingLog>>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            if (count <= 0 || count > 1000)
            {
                return Result<IEnumerable<ProcessingLog>>.Failure("Count must be between 1 and 1000");
            }

            var logs = await _context.ProcessingLogs
                .Where(log => log.IsActive)
                .OrderByDescending(log => log.CreatedDate)
                .Take(count)
                .ToListAsync(cancellationToken);

            return Result<IEnumerable<ProcessingLog>>.Success(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent processing logs");
            return Result<IEnumerable<ProcessingLog>>.Failure($"Failed to get recent processing logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets processing statistics for performance monitoring.
    /// </summary>
    public async Task<Result<ProcessingStatistics>> GetStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.ProcessingLogs.Where(log => log.IsActive);

            if (fromDate.HasValue)
            {
                query = query.Where(log => log.CreatedDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(log => log.CreatedDate <= toDate.Value);
            }

            var logs = await query.ToListAsync(cancellationToken);

            if (!logs.Any())
            {
                var emptyStats = new ProcessingStatistics
                {
                    TotalEntries = 0,
                    ErrorCount = 0,
                    WarningCount = 0,
                    InfoCount = 0,
                    AverageDurationMs = 0,
                    MaxDurationMs = 0,
                    MinDurationMs = 0,
                    OldestEntry = null,
                    NewestEntry = null,
                    CategoriesCount = new Dictionary<string, int>()
                };

                return Result<ProcessingStatistics>.Success(emptyStats);
            }

            var durations = logs.Where(l => l.DurationMs.HasValue).Select(l => l.DurationMs!.Value).ToList();

            var statistics = new ProcessingStatistics
            {
                TotalEntries = logs.Count,
                ErrorCount = logs.Count(l => l.Level == CoreLogLevel.Error),
                WarningCount = logs.Count(l => l.Level == CoreLogLevel.Warning),
                InfoCount = logs.Count(l => l.Level == CoreLogLevel.Information),
                AverageDurationMs = durations.Any() ? durations.Average() : 0,
                MaxDurationMs = durations.Any() ? durations.Max() : 0,
                MinDurationMs = durations.Any() ? durations.Min() : 0,
                OldestEntry = logs.Min(l => l.CreatedDate),
                NewestEntry = logs.Max(l => l.CreatedDate),
                CategoriesCount = logs
                    .GroupBy(l => l.Category)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return Result<ProcessingStatistics>.Success(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing statistics");
            return Result<ProcessingStatistics>.Failure($"Failed to get processing statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleans up old processing log entries based on retention policy.
    /// </summary>
    public async Task<Result<int>> CleanupOldEntriesAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var oldEntries = await _context.ProcessingLogs
                .Where(log => log.CreatedDate < cutoffDate && log.IsActive)
                .ToListAsync(cancellationToken);

            if (!oldEntries.Any())
            {
                return Result<int>.Success(0);
            }

            // Soft delete - mark as inactive rather than physical deletion
            foreach (var entry in oldEntries)
            {
                entry.SoftDelete();
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cleaned up {Count} old processing log entries before {CutoffDate}", 
                oldEntries.Count, cutoffDate);

            return Result<int>.Success(oldEntries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old processing log entries");
            return Result<int>.Failure($"Failed to cleanup old entries: {ex.Message}");
        }
    }
}