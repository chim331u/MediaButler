using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Repository interface specific to TrackedFile entity with domain-specific query methods.
/// Extends the generic repository with MediaButler workflow-specific operations,
/// following "Simple Made Easy" principles by keeping file-specific concerns separate.
/// </summary>
/// <remarks>
/// This interface provides TrackedFile-specific queries that support:
/// - File processing workflow states (New -> Classified -> Moved)
/// - ML classification result management
/// - Error handling and retry logic
/// - Performance monitoring and statistics
/// - Duplicate detection and file organization
/// 
/// Each method has a single, clear responsibility without complecting concerns.
/// </remarks>
public interface ITrackedFileRepository : IRepository<TrackedFile>
{
    /// <summary>
    /// Retrieves a tracked file by its SHA256 hash.
    /// Hash serves as the primary key and unique identifier for files.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The tracked file if found and active, null otherwise.</returns>
    Task<TrackedFile?> GetByHashAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tracked files by their current processing status.
    /// Essential for workflow management and background processing queues.
    /// </summary>
    /// <param name="status">The file processing status to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files with the specified status.</returns>
    Task<IEnumerable<TrackedFile>> GetByStatusAsync(FileStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files that are ready for ML classification (Status = New).
    /// Used by background services to queue files for ML processing.
    /// </summary>
    /// <param name="limit">Maximum number of files to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files ready for classification, ordered by creation date.</returns>
    Task<IEnumerable<TrackedFile>> GetFilesReadyForClassificationAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files that have been classified and are awaiting user confirmation.
    /// Used for UI display of pending file decisions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of classified files awaiting confirmation.</returns>
    Task<IEnumerable<TrackedFile>> GetFilesAwaitingConfirmationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files that are ready to be moved to their target locations.
    /// Used by file organization services to process confirmed files.
    /// </summary>
    /// <param name="limit">Maximum number of files to return (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files ready for organization, ordered by confirmation date.</returns>
    Task<IEnumerable<TrackedFile>> GetFilesReadyForMovingAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files that have encountered errors and may need retry or manual intervention.
    /// Includes files in Error or Retry status with their error information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files with processing errors.</returns>
    Task<IEnumerable<TrackedFile>> GetFilesWithErrorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files by their assigned category for organization statistics.
    /// Useful for generating reports and monitoring categorization accuracy.
    /// </summary>
    /// <param name="category">The category name to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files in the specified category.</returns>
    Task<IEnumerable<TrackedFile>> GetFilesByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files classified with confidence below the specified threshold.
    /// Used for identifying files that may need manual review or model improvement.
    /// </summary>
    /// <param name="confidenceThreshold">The confidence threshold (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files with low classification confidence.</returns>
    Task<IEnumerable<TrackedFile>> GetLowConfidenceFilesAsync(decimal confidenceThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file with the specified original path already exists in the system.
    /// Used for duplicate detection during file discovery.
    /// </summary>
    /// <param name="originalPath">The original file path to check.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if a file with this path exists, false otherwise.</returns>
    Task<bool> ExistsByOriginalPathAsync(string originalPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves processing statistics for monitoring and reporting.
    /// Returns counts by status for dashboard display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Dictionary with status as key and count as value.</returns>
    Task<Dictionary<FileStatus, int>> GetProcessingStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files processed within the specified date range.
    /// Useful for performance monitoring and batch processing reports.
    /// </summary>
    /// <param name="startDate">Start date for the range (inclusive).</param>
    /// <param name="endDate">End date for the range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files processed within the date range.</returns>
    Task<IEnumerable<TrackedFile>> GetFilesProcessedInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files that have exceeded the maximum retry count and need manual intervention.
    /// Used for error handling and support processes.
    /// </summary>
    /// <param name="maxRetryCount">Maximum allowed retry count (default: 3).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files that have exceeded retry limits.</returns>
    Task<IEnumerable<TrackedFile>> GetFilesExceedingRetryLimitAsync(int maxRetryCount = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recently moved files for verification and rollback scenarios.
    /// Includes files moved within the last specified number of hours.
    /// </summary>
    /// <param name="withinHours">Number of hours to look back (default: 24).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of recently moved files.</returns>
    Task<IEnumerable<TrackedFile>> GetRecentlyMovedFilesAsync(int withinHours = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for files by filename pattern using SQL LIKE functionality.
    /// Useful for debugging and manual file lookup in the UI.
    /// </summary>
    /// <param name="filenamePattern">Filename pattern with SQL wildcards (% and _).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files matching the filename pattern.</returns>
    Task<IEnumerable<TrackedFile>> SearchByFilenameAsync(string filenamePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets distinct category values from tracked files.
    /// Retrieves all unique, non-null category values that have been assigned to files.
    /// Useful for populating category dropdowns and understanding data distribution.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of distinct category names.</returns>
    Task<IEnumerable<string>> GetDistinctCategoriesAsync(CancellationToken cancellationToken = default);
}