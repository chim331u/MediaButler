using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaButler.API.Jobs.Recurring;

/// <summary>
/// Hangfire recurring job for cleaning up old log files.
/// Removes log files older than configured retention period (default: 30 days).
/// Optimized for ARM32 NAS deployment with minimal resource usage.
/// </summary>
[Queue("low-priority")]
[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
public class LogCleanupJob
{
    private readonly ILogger<LogCleanupJob> _logger;
    private readonly IConfiguration _configuration;
    private const int DefaultRetentionDays = 30;

    public LogCleanupJob(
        ILogger<LogCleanupJob> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Scans log directory and deletes files older than retention period.
    /// Runs daily at 2:00 AM by default.
    /// </summary>
    [JobDisplayName("Log Cleanup - Delete Old Logs")]
    public async Task CleanupOldLogsAsync()
    {
        _logger.LogInformation("Log cleanup job started");

        try
        {
            // Get log path from Serilog configuration
            var logPath = _configuration.GetValue<string>("Serilog:WriteTo:1:Args:path", "/data/logs/mediabutler-.log");
            var logDirectory = Path.GetDirectoryName(logPath) ?? "/data/logs";

            // Get retention period from configuration
            var retentionDays = _configuration.GetValue<int>("Serilog:ARM32Optimization:LogRetentionDays", DefaultRetentionDays);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            _logger.LogInformation("Cleaning logs from directory: {LogDirectory}, retention: {RetentionDays} days, cutoff: {CutoffDate}",
                logDirectory, retentionDays, cutoffDate);

            if (!Directory.Exists(logDirectory))
            {
                _logger.LogWarning("Log directory not found: {LogDirectory}", logDirectory);
                return;
            }

            // Find all .log files older than retention period
            var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories);
            var oldLogs = logFiles
                .Where(f =>
                {
                    try
                    {
                        return File.GetCreationTimeUtc(f) < cutoffDate;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            _logger.LogInformation("Found {Count} log files older than {Days} days", oldLogs.Count, retentionDays);

            if (oldLogs.Count == 0)
            {
                _logger.LogInformation("No old log files to delete");
                return;
            }

            var deletedCount = 0;
            var deletedSize = 0L;
            var failedCount = 0;

            foreach (var logFile in oldLogs)
            {
                try
                {
                    var fileInfo = new FileInfo(logFile);
                    var fileSize = fileInfo.Length;

                    File.Delete(logFile);
                    deletedCount++;
                    deletedSize += fileSize;

                    _logger.LogDebug("Deleted old log file: {LogFile} (size: {Size} bytes, created: {Created})",
                        logFile, fileSize, fileInfo.CreationTimeUtc);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogWarning(ex, "Failed to delete log file: {LogFile}", logFile);
                }
            }

            _logger.LogInformation(
                "Log cleanup completed. Deleted {DeletedCount}/{TotalCount} files (failed: {FailedCount}), freed {FreedMB:F2} MB",
                deletedCount, oldLogs.Count, failedCount, deletedSize / (1024.0 * 1024.0));

            // Await to satisfy async signature (actual work is synchronous)
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log cleanup job failed with exception");
            throw;
        }
    }
}
