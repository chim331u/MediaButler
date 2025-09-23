using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Service interface for file management operations in the MediaButler system.
/// Provides high-level business operations for tracked files following "Simple Made Easy" principles
/// by keeping file operations un-braided from other concerns.
/// </summary>
/// <remarks>
/// This service coordinates file lifecycle operations including:
/// - File discovery and registration
/// - Status management and workflow transitions
/// - File organization and movement operations
/// - Error handling and retry coordination
/// - Integration with ML classification results
/// 
/// All operations return Result&lt;T&gt; for explicit error handling without exceptions.
/// Each method has a single, clear responsibility without complecting concerns.
/// </remarks>
public interface IFileService
{
    /// <summary>
    /// Registers a new file for tracking based on its file path.
    /// Calculates hash, extracts metadata, and initializes the file record.
    /// </summary>
    /// <param name="filePath">The full path to the file to track.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the tracked file if successful, error message if failed.</returns>
    Task<Result<TrackedFile>> RegisterFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a tracked file by its SHA256 hash.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the tracked file if found, error message if not found or failed.</returns>
    Task<Result<TrackedFile>> GetFileByHashAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tracked files filtered by their processing status.
    /// </summary>
    /// <param name="status">The file processing status to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing collection of files with the specified status.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetFilesByStatusAsync(FileStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files that are ready for ML classification processing.
    /// Returns files in New status, ordered by creation date for FIFO processing.
    /// </summary>
    /// <param name="limit">Maximum number of files to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing files ready for classification.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetFilesReadyForClassificationAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files that have been classified and are awaiting user confirmation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing files awaiting confirmation.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetFilesAwaitingConfirmationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files that are ready to be moved to their target locations.
    /// </summary>
    /// <param name="limit">Maximum number of files to return (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing files ready for organization.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetFilesReadyForMovingAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a tracked file with ML classification results.
    /// Transitions file from New to Classified status with confidence score.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file to update.</param>
    /// <param name="suggestedCategory">The category suggested by ML classification.</param>
    /// <param name="confidence">The confidence score (0.0 to 1.0) of the classification.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing updated tracked file if successful.</returns>
    Task<Result<TrackedFile>> UpdateClassificationAsync(string hash, string suggestedCategory, decimal confidence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a file's category, transitioning it to ReadyToMove status.
    /// This accepts or overrides the ML classification result.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file to confirm.</param>
    /// <param name="confirmedCategory">The user-confirmed category for the file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing updated tracked file if successful.</returns>
    Task<Result<TrackedFile>> ConfirmCategoryAsync(string hash, string confirmedCategory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a file as moved to its target location and updates the moved timestamp.
    /// Transitions file to Moved status indicating successful organization.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file that was moved.</param>
    /// <param name="targetPath">The target path where the file was moved.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing updated tracked file if successful.</returns>
    Task<Result<TrackedFile>> MarkFileAsMovedAsync(string hash, string targetPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an error for a tracked file and increments the retry count.
    /// Transitions file to Error or Retry status based on retry limits.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file that encountered an error.</param>
    /// <param name="errorMessage">The error message to record.</param>
    /// <param name="exception">Optional exception details for debugging.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing updated tracked file if successful.</returns>
    Task<Result<TrackedFile>> RecordErrorAsync(string hash, string errorMessage, string? exception = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a file's error state and retry count, allowing it to be processed again.
    /// Transitions file back to appropriate status for retry.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file to reset.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing updated tracked file if successful.</returns>
    Task<Result<TrackedFile>> ResetFileErrorAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a tracked file, marking it as inactive while preserving audit trail.
    /// </summary>
    /// <param name="hash">The SHA256 hash of the file to delete.</param>
    /// <param name="reason">Optional reason for the deletion.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result indicating success or failure of the operation.</returns>
    Task<Result> DeleteFileAsync(string hash, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file with the specified path is already being tracked.
    /// Used for duplicate detection during file discovery.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing true if file exists, false otherwise.</returns>
    Task<Result<bool>> IsFileAlreadyTrackedAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for tracked files by filename pattern.
    /// Supports SQL-like wildcards for flexible file lookup.
    /// </summary>
    /// <param name="filenamePattern">Filename pattern with wildcards (% and _).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing collection of matching files.</returns>
    Task<Result<IEnumerable<TrackedFile>>> SearchFilesByNameAsync(string filenamePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves files that have exceeded the maximum retry count and need manual intervention.
    /// </summary>
    /// <param name="maxRetryCount">Maximum allowed retry count (default: 3).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing files that need manual intervention.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetFilesNeedingInterventionAsync(int maxRetryCount = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recently moved files for verification and potential rollback scenarios.
    /// </summary>
    /// <param name="withinHours">Number of hours to look back (default: 24).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing recently moved files.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetRecentlyMovedFilesAsync(int withinHours = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated list of tracked files with optional filtering.
    /// </summary>
    /// <param name="skip">Number of files to skip for pagination.</param>
    /// <param name="take">Number of files to take (page size).</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing paginated file collection.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetFilesPagedAsync(
        int skip,
        int take,
        FileStatus? status = null,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated list of tracked files filtered by multiple statuses.
    /// Enables efficient querying of files across multiple processing states.
    /// </summary>
    /// <param name="skip">Number of files to skip for pagination.</param>
    /// <param name="take">Number of files to take (page size).</param>
    /// <param name="statuses">Collection of file statuses to filter by.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing paginated file collection matching any of the specified statuses.</returns>
    Task<Result<IEnumerable<TrackedFile>>> GetFilesPagedByStatusesAsync(
        int skip,
        int take,
        IEnumerable<FileStatus> statuses,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets distinct categories from tracked files.
    /// Retrieves all unique category values that have been assigned to files in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing alphabetically sorted list of distinct categories.</returns>
    Task<Result<IEnumerable<string>>> GetDistinctCategoriesAsync(
        CancellationToken cancellationToken = default);
}