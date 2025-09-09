using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Repository interface for ProcessingLog entities providing audit trail operations.
/// Follows "Simple Made Easy" principles with focused responsibility on audit logging.
/// </summary>
public interface IProcessingLogRepository
{
    /// <summary>
    /// Adds a processing log entry to the audit trail.
    /// </summary>
    /// <param name="processingLog">The log entry to add</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> AddAsync(ProcessingLog processingLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple processing log entries efficiently.
    /// </summary>
    /// <param name="processingLogs">The log entries to add</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> AddRangeAsync(IEnumerable<ProcessingLog> processingLogs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing logs for a specific file hash.
    /// </summary>
    /// <param name="fileHash">The file hash to get logs for</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the processing logs for the file</returns>
    Task<Result<IEnumerable<ProcessingLog>>> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing logs filtered by level and time range.
    /// </summary>
    /// <param name="level">Minimum log level to retrieve</param>
    /// <param name="fromDate">Start date for log entries (inclusive)</param>
    /// <param name="toDate">End date for log entries (inclusive)</param>
    /// <param name="skip">Number of entries to skip for pagination</param>
    /// <param name="take">Number of entries to take</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the filtered processing logs</returns>
    Task<Result<IEnumerable<ProcessingLog>>> GetByLevelAsync(
        LogLevel level,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing logs filtered by category and time range.
    /// </summary>
    /// <param name="category">The category to filter by</param>
    /// <param name="fromDate">Start date for log entries (inclusive)</param>
    /// <param name="toDate">End date for log entries (inclusive)</param>
    /// <param name="skip">Number of entries to skip for pagination</param>
    /// <param name="take">Number of entries to take</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the filtered processing logs</returns>
    Task<Result<IEnumerable<ProcessingLog>>> GetByCategoryAsync(
        string category,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent processing logs ordered by creation date.
    /// </summary>
    /// <param name="count">Number of recent entries to retrieve</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing recent processing logs</returns>
    Task<Result<IEnumerable<ProcessingLog>>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing statistics for performance monitoring.
    /// </summary>
    /// <param name="fromDate">Start date for statistics calculation</param>
    /// <param name="toDate">End date for statistics calculation</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing processing statistics</returns>
    Task<Result<ProcessingStatistics>> GetStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old processing log entries based on retention policy.
    /// </summary>
    /// <param name="cutoffDate">Entries older than this date will be deleted</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the number of entries deleted</returns>
    Task<Result<int>> CleanupOldEntriesAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics summary for processing operations.
/// </summary>
public record ProcessingStatistics
{
    public int TotalEntries { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public double AverageDurationMs { get; init; }
    public long MaxDurationMs { get; init; }
    public long MinDurationMs { get; init; }
    public DateTime? OldestEntry { get; init; }
    public DateTime? NewestEntry { get; init; }
    public IReadOnlyDictionary<string, int> CategoriesCount { get; init; } = new Dictionary<string, int>();
}