using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Models;
using MediaButler.Shared.UI.Models;
using MediaButler.Shared.UI.Services;
using MediaButler.Core.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MediaButler.Mobile.Components.Service;

/// <summary>
/// Caching decorator for IFilesApiService.
/// Implements "Simple Made Easy" - composes caching WITHOUT braiding it into core logic.
/// </summary>
public class CachedFilesApiService : IFilesApiService
{
    private readonly IFilesApiService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedFilesApiService> _logger;
    private readonly bool _cachingEnabled;

    private const string CategoriesCacheKey = "files:categories";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public CachedFilesApiService(
        IFilesApiService inner,
        IMemoryCache cache,
        ILogger<CachedFilesApiService> logger,
        Microsoft.Extensions.Options.IOptions<FeatureFlags> flags)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _cachingEnabled = flags.Value?.EnableCaching ?? false;
    }

    // CACHED: Categories (5 minutes)
    public async Task<Result<IReadOnlyList<string>>> GetDistinctCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_cachingEnabled)
        {
            return await _inner.GetDistinctCategoriesAsync(cancellationToken);
        }

        return await _cache.GetOrCreateAsync(CategoriesCacheKey, async entry =>
        {
            _logger.LogDebug("Cache MISS: Categories");

            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.Priority = CacheItemPriority.Normal;

            var result = await _inner.GetDistinctCategoriesAsync(cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("✅ Cached {Count} categories for {Duration} minutes",
                    result.Value?.Count ?? 0, CacheDuration.TotalMinutes);
            }

            return result;
        })!;
    }

    // CACHE INVALIDATION: Operations that modify files
    public async Task<Result<FileManagementDto>> ConfirmFileCategoryAsync(
        string hash, string category, CancellationToken cancellationToken = default)
    {
        var result = await _inner.ConfirmFileCategoryAsync(hash, category, cancellationToken);

        if (result.IsSuccess)
        {
            InvalidateCache();
        }

        return result;
    }

    public async Task<Result<BatchJobResponseDto>> OrganizeBatchAsync(
        BatchOrganizeRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await _inner.OrganizeBatchAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            InvalidateCache();
        }

        return result;
    }

    public async Task<Result<object>> IgnoreFileAsync(
        string hash, CancellationToken cancellationToken = default)
    {
        var result = await _inner.IgnoreFileAsync(hash, cancellationToken);

        if (result.IsSuccess)
        {
            InvalidateCache();
        }

        return result;
    }

    private void InvalidateCache()
    {
        _cache.Remove(CategoriesCacheKey);
        _logger.LogDebug("🔄 Cache invalidated (categories)");
    }

    // PASS-THROUGH: All other methods (no caching)
    public Task<Result<PaginatedFilesDto>> GetFilesByStatusesAsync(
        int skip, int take, FileStatus[] statuses, string? category = null,
        string? searchTerm = null, string? orderBy = null, bool descending = true,
        CancellationToken cancellationToken = default) =>
        _inner.GetFilesByStatusesAsync(skip, take, statuses, category, searchTerm, orderBy, descending, cancellationToken);

    public Task<Result<FileManagementDto>> GetFileAsync(
        string hash, CancellationToken cancellationToken = default) =>
        _inner.GetFileAsync(hash, cancellationToken);

    public Task<Result<IReadOnlyList<FileManagementDto>>> GetPendingFilesAsync(
        CancellationToken cancellationToken = default) =>
        _inner.GetPendingFilesAsync(cancellationToken);

    public Task<Result<IReadOnlyList<FileManagementDto>>> GetFilesReadyForClassificationAsync(
        int limit = 50, CancellationToken cancellationToken = default) =>
        _inner.GetFilesReadyForClassificationAsync(limit, cancellationToken);

    public Task<Result<FileManagementDto>> MarkFileAsMovedAsync(
        string hash, string targetPath, CancellationToken cancellationToken = default) =>
        _inner.MarkFileAsMovedAsync(hash, targetPath, cancellationToken);

    public Task<Result> DeleteFileAsync(
        string hash, string? reason = null, CancellationToken cancellationToken = default) =>
        _inner.DeleteFileAsync(hash, reason, cancellationToken);

    public Task<Result<ScanResultDto>> ScanFoldersAsync(
        CancellationToken cancellationToken = default) =>
        _inner.ScanFoldersAsync(cancellationToken);

    public Task<Result<ScanResultDto>> ScanSpecificFolderAsync(
        string folderPath, CancellationToken cancellationToken = default) =>
        _inner.ScanSpecificFolderAsync(folderPath, cancellationToken);

    public Task<Result<BatchJobResponseDto>> GetBatchStatusAsync(
        string jobId, bool includeDetails = false, CancellationToken cancellationToken = default) =>
        _inner.GetBatchStatusAsync(jobId, includeDetails, cancellationToken);

    public Task<Result<MlEvaluationResponse>> QueueMlEvaluationAsync(
        string? filterByCategory = null, bool forceReEvaluation = true,
        CancellationToken cancellationToken = default) =>
        _inner.QueueMlEvaluationAsync(filterByCategory, forceReEvaluation, cancellationToken);

    public Task<Result<IReadOnlyList<FileManagementDto>>> GetFilesAsync(
        int skip = 0, int take = 20, string? status = null, string? category = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetFilesAsync(skip, take, status, category, cancellationToken);
}
