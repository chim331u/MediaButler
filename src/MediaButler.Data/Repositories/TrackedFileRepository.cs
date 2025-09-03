using Microsoft.EntityFrameworkCore;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Repository implementation for TrackedFile entity with MediaButler-specific query operations.
/// Extends the generic repository with domain-specific queries that support the file processing workflow,
/// following "Simple Made Easy" principles with explicit, single-purpose methods.
/// </summary>
/// <remarks>
/// This implementation leverages the optimized database indexes created in our entity configurations
/// to provide efficient queries for:
/// - File processing workflow management
/// - ML classification result handling
/// - Error monitoring and retry logic
/// - Performance analytics and reporting
/// - File organization and duplicate detection
/// 
/// Each method is designed to use specific database indexes for optimal performance.
/// </remarks>
public class TrackedFileRepository : Repository<TrackedFile>, ITrackedFileRepository
{
    /// <summary>
    /// Initializes a new instance of the TrackedFileRepository.
    /// </summary>
    /// <param name="context">The database context.</param>
    public TrackedFileRepository(DbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<TrackedFile?> GetByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        
        // Uses primary key lookup - most efficient query possible
        return await DbSet.FindAsync([hash], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetByStatusAsync(FileStatus status, CancellationToken cancellationToken = default)
    {
        // Uses IX_TrackedFiles_Status_IsActive index for optimal performance
        return await DbSet
            .Where(f => f.Status == status)
            .OrderBy(f => f.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetFilesReadyForClassificationAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) throw new ArgumentException("Limit must be positive", nameof(limit));

        // Uses IX_TrackedFiles_Status_IsActive index, filtered for New status
        return await DbSet
            .Where(f => f.Status == FileStatus.New)
            .OrderBy(f => f.CreatedDate) // FIFO processing
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetFilesAwaitingConfirmationAsync(CancellationToken cancellationToken = default)
    {
        // Uses IX_TrackedFiles_Classification_Workflow index for Classified status
        return await DbSet
            .Where(f => f.Status == FileStatus.Classified)
            .OrderBy(f => f.ClassifiedAt) // Order by classification time
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetFilesReadyForMovingAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) throw new ArgumentException("Limit must be positive", nameof(limit));

        // Uses IX_TrackedFiles_Organization_Workflow index for ReadyToMove status
        return await DbSet
            .Where(f => f.Status == FileStatus.ReadyToMove)
            .OrderBy(f => f.LastUpdateDate) // Order by confirmation time
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetFilesWithErrorsAsync(CancellationToken cancellationToken = default)
    {
        // Uses IX_TrackedFiles_Error_Monitoring index for error statuses
        return await DbSet
            .Where(f => f.Status == FileStatus.Error || f.Status == FileStatus.Retry)
            .OrderByDescending(f => f.LastErrorAt) // Most recent errors first
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetFilesByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        // Uses IX_TrackedFiles_Category_Stats index
        return await DbSet
            .Where(f => f.Category == category)
            .OrderByDescending(f => f.MovedAt)
            .ThenBy(f => f.FileName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetLowConfidenceFilesAsync(decimal confidenceThreshold, CancellationToken cancellationToken = default)
    {
        if (confidenceThreshold < 0 || confidenceThreshold > 1)
            throw new ArgumentException("Confidence threshold must be between 0.0 and 1.0", nameof(confidenceThreshold));

        // Uses IX_TrackedFiles_Classification_Workflow index
        return await DbSet
            .Where(f => f.Status == FileStatus.Classified && f.Confidence < confidenceThreshold)
            .OrderBy(f => f.Confidence) // Lowest confidence first
            .ThenBy(f => f.ClassifiedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByOriginalPathAsync(string originalPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalPath);

        // Uses IX_TrackedFiles_OriginalPath index
        return await DbSet
            .AnyAsync(f => f.OriginalPath == originalPath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<FileStatus, int>> GetProcessingStatsAsync(CancellationToken cancellationToken = default)
    {
        // Uses IX_TrackedFiles_Status_IsActive index for efficient grouping
        var stats = await DbSet
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return stats.ToDictionary(s => s.Status, s => s.Count);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetFilesProcessedInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date must be before or equal to end date");

        // Uses BaseEntity CreatedDate index for efficient date range queries
        return await DbSet
            .Where(f => f.CreatedDate >= startDate && f.CreatedDate <= endDate)
            .OrderBy(f => f.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetFilesExceedingRetryLimitAsync(int maxRetryCount = 3, CancellationToken cancellationToken = default)
    {
        if (maxRetryCount < 0)
            throw new ArgumentException("Max retry count cannot be negative", nameof(maxRetryCount));

        // Uses IX_TrackedFiles_Error_Monitoring index for retry count queries
        return await DbSet
            .Where(f => f.RetryCount > maxRetryCount)
            .OrderByDescending(f => f.LastErrorAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> GetRecentlyMovedFilesAsync(int withinHours = 24, CancellationToken cancellationToken = default)
    {
        if (withinHours <= 0)
            throw new ArgumentException("Within hours must be positive", nameof(withinHours));

        var cutoffDate = DateTime.UtcNow.AddHours(-withinHours);

        // Uses IX_TrackedFiles_Organization_Workflow index
        return await DbSet
            .Where(f => f.Status == FileStatus.Moved && f.MovedAt >= cutoffDate)
            .OrderByDescending(f => f.MovedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedFile>> SearchByFilenameAsync(string filenamePattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filenamePattern);

        // Uses IX_TrackedFiles_Filename_Analysis index for filename searches
        return await DbSet
            .Where(f => EF.Functions.Like(f.FileName, filenamePattern))
            .OrderBy(f => f.FileName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets files that need ML classification retry due to temporary failures.
    /// Used by background services for resilient processing.
    /// </summary>
    /// <param name="maxRetryCount">Maximum allowed retry attempts.</param>
    /// <param name="retryDelayMinutes">Minimum minutes since last attempt.</param>
    /// <param name="limit">Maximum number of files to return.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of files ready for classification retry.</returns>
    public async Task<IEnumerable<TrackedFile>> GetFilesReadyForRetryAsync(
        int maxRetryCount = 3, 
        int retryDelayMinutes = 15, 
        int limit = 20, 
        CancellationToken cancellationToken = default)
    {
        if (maxRetryCount < 0) throw new ArgumentException("Max retry count cannot be negative", nameof(maxRetryCount));
        if (retryDelayMinutes <= 0) throw new ArgumentException("Retry delay must be positive", nameof(retryDelayMinutes));
        if (limit <= 0) throw new ArgumentException("Limit must be positive", nameof(limit));

        var retryAfter = DateTime.UtcNow.AddMinutes(-retryDelayMinutes);

        // Uses IX_TrackedFiles_Error_Monitoring index
        return await DbSet
            .Where(f => f.Status == FileStatus.Retry && 
                       f.RetryCount < maxRetryCount && 
                       (f.LastErrorAt == null || f.LastErrorAt <= retryAfter))
            .OrderBy(f => f.LastErrorAt ?? f.CreatedDate) // Oldest errors first
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets files with high confidence classifications for ML model validation.
    /// Used for quality assurance and model performance monitoring.
    /// </summary>
    /// <param name="confidenceThreshold">Minimum confidence threshold (default: 0.95).</param>
    /// <param name="limit">Maximum number of files to return.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of high-confidence classified files.</returns>
    public async Task<IEnumerable<TrackedFile>> GetHighConfidenceFilesAsync(
        decimal confidenceThreshold = 0.95m, 
        int limit = 100, 
        CancellationToken cancellationToken = default)
    {
        if (confidenceThreshold < 0 || confidenceThreshold > 1)
            throw new ArgumentException("Confidence threshold must be between 0.0 and 1.0", nameof(confidenceThreshold));
        if (limit <= 0) throw new ArgumentException("Limit must be positive", nameof(limit));

        // Uses IX_TrackedFiles_Classification_Workflow index
        return await DbSet
            .Where(f => f.Status == FileStatus.Classified && f.Confidence >= confidenceThreshold)
            .OrderByDescending(f => f.Confidence)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}