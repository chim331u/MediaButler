using MediaButler.Core.Common;

namespace MediaButler.Services.Background;

/// <summary>
/// Service interface for discovering and monitoring files in watch folders.
/// Provides file system monitoring capabilities following "Simple Made Easy" principles
/// with clear separation between file system operations and business logic.
/// </summary>
public interface IFileDiscoveryService
{
    /// <summary>
    /// Starts monitoring the configured watch folders for new files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the monitoring operation</param>
    /// <returns>Result indicating success or failure of starting monitoring</returns>
    Task<Result> StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring watch folders and releases resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the stop operation</param>
    /// <returns>Result indicating success or failure of stopping monitoring</returns>
    Task<Result> StopMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a one-time scan of watch folders for existing files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the scan operation</param>
    /// <returns>Result containing the number of files discovered</returns>
    Task<Result<int>> ScanFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current monitoring status.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Gets the list of currently monitored folder paths.
    /// </summary>
    IEnumerable<string> MonitoredPaths { get; }

    /// <summary>
    /// Event triggered when a new file is discovered.
    /// </summary>
    event EventHandler<FileDiscoveredEventArgs> FileDiscovered;

    /// <summary>
    /// Event triggered when a file discovery error occurs.
    /// </summary>
    event EventHandler<FileDiscoveryErrorEventArgs> DiscoveryError;
}

/// <summary>
/// Event arguments for file discovered events.
/// </summary>
public class FileDiscoveredEventArgs : EventArgs
{
    public FileDiscoveredEventArgs(string filePath, DateTime discoveredAt)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        DiscoveredAt = discoveredAt;
    }

    /// <summary>
    /// The full path to the discovered file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The timestamp when the file was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; }
}

/// <summary>
/// Event arguments for file discovery error events.
/// </summary>
public class FileDiscoveryErrorEventArgs : EventArgs
{
    public FileDiscoveryErrorEventArgs(string? filePath, string errorMessage, Exception? exception = null)
    {
        FilePath = filePath;
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        Exception = exception;
        OccurredAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The file path that caused the error, if known.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// The error message describing what went wrong.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// The exception that occurred, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// The timestamp when the error occurred.
    /// </summary>
    public DateTime OccurredAt { get; }
}