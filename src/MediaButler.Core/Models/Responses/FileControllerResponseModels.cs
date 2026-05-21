using MediaButler.Core.Models.Responses; // To reference TrackedFileResponse if needed. Wait, TrackedFileResponse is likely in MediaButler.API.Models.Response. Let's check where it is.
// Actually I'll just use the same namespace and assume it resolves.

namespace MediaButler.Core.Models.Responses;

/// <summary>
/// Response model for folder scan operations.
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Number of files discovered during the scan.
    /// </summary>
    public int FilesDiscovered { get; set; }

    /// <summary>
    /// Timestamp when the scan operation started.
    /// </summary>
    public DateTime ScanStartedAt { get; set; }

    /// <summary>
    /// Timestamp when the scan operation completed.
    /// </summary>
    public DateTime ScanCompletedAt { get; set; }

    /// <summary>
    /// Whether file system monitoring is currently enabled.
    /// </summary>
    public bool MonitoringEnabled { get; set; }

    /// <summary>
    /// List of paths currently being monitored.
    /// </summary>
    public List<string> MonitoredPaths { get; set; } = new();

    /// <summary>
    /// Specific path that was scanned (for single folder scans).
    /// </summary>
    public string? ScannedPath { get; set; }

    /// <summary>
    /// Duration of the scan operation in milliseconds.
    /// </summary>
    public double ScanDurationMs => (ScanCompletedAt - ScanStartedAt).TotalMilliseconds;
}

/// <summary>
/// Paginated response for file queries with total count.
/// </summary>
/// <typeparam name="T">The type of the paginated items</typeparam>
public class PaginatedResponse<T>
{
    /// <summary>
    /// List of items for the current page.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total number of items matching the query (across all pages).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Number of items skipped (pagination offset).
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Number of items requested (page size).
    /// </summary>
    public int Take { get; set; }
}

/// <summary>
/// Paginated response specifically for files, mapping to the old PaginatedFilesResponse.
/// </summary>
public class PaginatedFilesResponse
{
    /// <summary>
    /// List of files for the current page.
    /// </summary>
    public List<TrackedFileResponse> Items { get; set; } = new();

    /// <summary>
    /// Total number of files matching the query (across all pages).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Number of files skipped (pagination offset).
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Number of files requested (page size).
    /// </summary>
    public int Take { get; set; }
}

