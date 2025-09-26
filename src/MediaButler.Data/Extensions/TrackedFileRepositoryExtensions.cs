using MediaButler.Core.Entities;
using MediaButler.Data.Repositories;

namespace MediaButler.Data.Extensions;

/// <summary>
/// Extension methods for TrackedFileRepository to support batch operations.
/// Provides optimized methods for working with multiple files simultaneously.
/// </summary>
public static class TrackedFileRepositoryExtensions
{
    /// <summary>
    /// Gets multiple files by their SHA256 hashes in a single database query.
    /// Returns a dictionary for O(1) lookup performance.
    /// </summary>
    /// <param name="repository">The tracked file repository</param>
    /// <param name="hashes">List of SHA256 hashes to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping hash to TrackedFile entity</returns>
    public static async Task<Dictionary<string, TrackedFile>> GetFilesByHashesAsync(
        this ITrackedFileRepository repository,
        List<string> hashes,
        CancellationToken cancellationToken = default)
    {
        if (hashes == null || hashes.Count == 0)
            return new Dictionary<string, TrackedFile>();

        // Remove duplicates and null/empty values
        var uniqueHashes = hashes
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct()
            .ToList();

        if (uniqueHashes.Count == 0)
            return new Dictionary<string, TrackedFile>();

        // Use the existing FindAsync method with a predicate
        var files = await repository.FindAsync(
            f => uniqueHashes.Contains(f.Hash) && f.IsActive,
            cancellationToken);

        return files.ToDictionary(f => f.Hash, f => f);
    }

    /// <summary>
    /// Gets files by their status in batches for efficient processing.
    /// Useful for batch operations on files in specific states.
    /// </summary>
    /// <param name="repository">The tracked file repository</param>
    /// <param name="status">The file status to filter by</param>
    /// <param name="limit">Maximum number of files to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of files with the specified status</returns>
    public static async Task<List<TrackedFile>> GetFilesByStatusAsync(
        this ITrackedFileRepository repository,
        Core.Enums.FileStatus status,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var files = await repository.FindAsync(
            f => f.Status == status && f.IsActive,
            cancellationToken);

        return files.Take(limit).ToList();
    }

    /// <summary>
    /// Gets files that are ready for organization (classified and not yet moved).
    /// Optimized for batch organization workflows.
    /// </summary>
    /// <param name="repository">The tracked file repository</param>
    /// <param name="limit">Maximum number of files to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of files ready for organization</returns>
    public static async Task<List<TrackedFile>> GetFilesReadyForOrganizationAsync(
        this ITrackedFileRepository repository,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var files = await repository.FindAsync(
            f => (f.Status == Core.Enums.FileStatus.Classified ||
                  f.Status == Core.Enums.FileStatus.ReadyToMove) &&
                 f.IsActive &&
                 !string.IsNullOrEmpty(f.SuggestedCategory),
            cancellationToken);

        return files.Take(limit).ToList();
    }

    /// <summary>
    /// Bulk updates file status for batch operations.
    /// More efficient than updating files individually.
    /// </summary>
    /// <param name="repository">The tracked file repository</param>
    /// <param name="fileHashes">List of file hashes to update</param>
    /// <param name="newStatus">The new status to set</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of files updated</returns>
    public static async Task<int> BulkUpdateStatusAsync(
        this ITrackedFileRepository repository,
        List<string> fileHashes,
        Core.Enums.FileStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        if (fileHashes == null || fileHashes.Count == 0)
            return 0;

        var files = await repository.FindAsync(
            f => fileHashes.Contains(f.Hash) && f.IsActive,
            cancellationToken);

        var updatedCount = 0;
        foreach (var file in files)
        {
            if (file.Status != newStatus)
            {
                file.Status = newStatus;
                file.MarkAsModified();
                repository.Update(file);
                updatedCount++;
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// Gets summary statistics for files by status.
    /// Useful for dashboard and monitoring views.
    /// </summary>
    /// <param name="repository">The tracked file repository</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping status to count</returns>
    public static async Task<Dictionary<Core.Enums.FileStatus, int>> GetFileStatusSummaryAsync(
        this ITrackedFileRepository repository,
        CancellationToken cancellationToken = default)
    {
        var files = await repository.FindAsync(
            f => f.IsActive,
            cancellationToken);

        return files
            .GroupBy(f => f.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Gets files with errors that might be suitable for retry operations.
    /// Filters by retry count to avoid infinite retry loops.
    /// </summary>
    /// <param name="repository">The tracked file repository</param>
    /// <param name="maxRetryCount">Maximum retry count to consider</param>
    /// <param name="limit">Maximum number of files to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of files that can be retried</returns>
    public static async Task<List<TrackedFile>> GetFilesForRetryAsync(
        this ITrackedFileRepository repository,
        int maxRetryCount = 3,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var files = await repository.FindAsync(
            f => (f.Status == Core.Enums.FileStatus.Error ||
                  f.Status == Core.Enums.FileStatus.Retry) &&
                 f.RetryCount < maxRetryCount &&
                 f.IsActive,
            cancellationToken);

        return files
            .OrderBy(f => f.LastErrorAt) // Oldest errors first
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Validates that all provided file hashes exist and are in a valid state for processing.
    /// Returns validation results for batch operations.
    /// </summary>
    /// <param name="repository">The tracked file repository</param>
    /// <param name="hashes">List of file hashes to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with details about found/missing files</returns>
    public static async Task<BatchValidationResult> ValidateFilesForBatchAsync(
        this ITrackedFileRepository repository,
        List<string> hashes,
        CancellationToken cancellationToken = default)
    {
        var existingFiles = await repository.GetFilesByHashesAsync(hashes, cancellationToken);

        var result = new BatchValidationResult
        {
            RequestedHashes = hashes,
            FoundFiles = existingFiles,
            MissingHashes = hashes.Except(existingFiles.Keys).ToList(),
            InvalidFiles = new List<string>()
        };

        // Check for files in invalid states
        foreach (var file in existingFiles.Values)
        {
            if (!file.IsActive)
            {
                result.InvalidFiles.Add($"{file.Hash}: File is not active");
            }
            else if (file.Status == Core.Enums.FileStatus.Moved && string.IsNullOrEmpty(file.MovedToPath))
            {
                result.InvalidFiles.Add($"{file.Hash}: File marked as moved but no target path");
            }
            else if (!File.Exists(file.OriginalPath))
            {
                result.InvalidFiles.Add($"{file.Hash}: Source file does not exist at {file.OriginalPath}");
            }
        }

        result.IsValid = result.MissingHashes.Count == 0 && result.InvalidFiles.Count == 0;

        return result;
    }
}

/// <summary>
/// Result of batch file validation operations.
/// Provides detailed information about found, missing, and invalid files.
/// </summary>
public class BatchValidationResult
{
    /// <summary>
    /// Gets or sets the list of requested file hashes.
    /// </summary>
    public required List<string> RequestedHashes { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of found files (hash -> TrackedFile).
    /// </summary>
    public required Dictionary<string, TrackedFile> FoundFiles { get; set; }

    /// <summary>
    /// Gets or sets the list of file hashes that were not found in the database.
    /// </summary>
    public required List<string> MissingHashes { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors for found files.
    /// </summary>
    public required List<string> InvalidFiles { get; set; }

    /// <summary>
    /// Gets or sets whether all requested files are valid for batch processing.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the count of valid files that can be processed.
    /// </summary>
    public int ValidFileCount => FoundFiles.Count - InvalidFiles.Count;

    /// <summary>
    /// Gets the percentage of requested files that are valid.
    /// </summary>
    public double ValidPercentage => RequestedHashes.Count > 0
        ? (double)ValidFileCount / RequestedHashes.Count * 100
        : 0;
}