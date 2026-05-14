# M5: Performance Optimization - Implementation Plan

**Date**: 2024-12-24
**Duration**: 4-5 days
**Goal**: Optimize for ARM32 NAS (pagination, caching, batch)
**Status**: Starting Implementation

---

## 🎯 OBJECTIVES

### Performance Targets
- ✅ Initial file load: <2 seconds on ARM32 NAS
- ✅ Memory usage: <100MB typical usage
- ✅ Categories API: <100ms (cached)
- ✅ Smooth scrolling with 100+ files
- ✅ Efficient batch operations

### Features to Implement
1. **Server-side Pagination** - Load 20 files at a time
2. **Response Caching** - Cache categories (5min TTL)
3. **Batch Operation Optimization** - Already done in Phase 2, verify performance
4. **Feature Flag Integration** - Control features via appsettings.json

---

## 📋 IMPLEMENTATION TASKS

### Task 1: Pagination Implementation (Index.razor)
**Estimated Time**: 2-3 hours
**Priority**: P0 (High Impact)

#### Current Behavior
- Loads ALL files at once (skip: 0, take: 100)
- Memory grows with file count
- Slow initial load with many files

#### New Behavior
- Loads 20 files initially
- "Load More" button for next 20
- Controlled by `EnablePagination` feature flag

#### Implementation Steps

**Step 1.1: Add Pagination State Variables**
```csharp
@code {
    private int currentPage = 0;
    private const int PageSize = 20;
    private int totalCount = 0;
    private bool hasMoreFiles = true;
    private bool isLoadingMore = false;
}
```

**Step 1.2: Update RefreshFileList() Method**
```csharp
private async Task RefreshFileList()
{
    busyRefresh = true;
    currentPage = 0;
    hasMoreFiles = true;

    var pageSize = _flags?.Value?.EnablePagination == true ? PageSize : 100;

    var statuses = new FileStatus[] { FileStatus.Classified, FileStatus.ReadyToMove, FileStatus.New };
    var result = await _filesApiService.GetFilesByStatusesAsync(
        skip: 0,
        take: pageSize,
        statuses: statuses,
        cancellationToken: CancellationToken.None
    );

    if (result.IsSuccess && result.Value != null)
    {
        fileDetail = DtoMapper.ToFilesDetailDtoList(result.Value.Items);
        totalCount = result.Value.Total;
        hasMoreFiles = fileDetail.Count() < totalCount;
        log = $"Loaded {fileDetail.Count()} of {totalCount} files";
    }
    else
    {
        log = $"Failed to load files: {result.Error}";
        fileDetail = new List<FilesDetailDto>();
        hasMoreFiles = false;
    }

    await RefreshCategoryList();
    notify(0);
    busyRefresh = false;
}
```

**Step 1.3: Add LoadMoreFiles() Method**
```csharp
private async Task LoadMoreFiles()
{
    if (!hasMoreFiles || isLoadingMore) return;

    isLoadingMore = true;
    currentPage++;

    var statuses = new FileStatus[] { FileStatus.Classified, FileStatus.ReadyToMove, FileStatus.New };
    var result = await _filesApiService.GetFilesByStatusesAsync(
        skip: currentPage * PageSize,
        take: PageSize,
        statuses: statuses,
        cancellationToken: CancellationToken.None
    );

    if (result.IsSuccess && result.Value != null)
    {
        var newFiles = DtoMapper.ToFilesDetailDtoList(result.Value.Items);
        fileDetail = fileDetail.Concat(newFiles).ToList();
        hasMoreFiles = fileDetail.Count() < totalCount;
        log = $"Loaded {fileDetail.Count()} of {totalCount} files";
    }
    else
    {
        log = $"Failed to load more files: {result.Error}";
    }

    isLoadingMore = false;
    StateHasChanged();
}
```

**Step 1.4: Add Load More Button to UI**
```razor
<!-- Add after RadzenDataGrid -->
@if (_flags?.Value?.EnablePagination == true && hasMoreFiles)
{
    <RadzenRow JustifyContent="JustifyContent.Center" class="mt-3">
        <RadzenButton
            Click="LoadMoreFiles"
            Text="@($"Load More ({totalCount - fileDetail.Count()} remaining)")"
            Icon="expand_more"
            ButtonStyle="ButtonStyle.Light"
            Size="ButtonSize.Large"
            IsBusy="@isLoadingMore"
            BusyText="Loading..." />
    </RadzenRow>
}
```

**Step 1.5: Inject IOptions<FeatureFlags>**
```csharp
[Inject]
public IOptions<FeatureFlags>? _flags { get; set; }
```

#### Acceptance Criteria
- [ ] Initial load shows 20 files (if `EnablePagination=true`)
- [ ] "Load More" button visible if more files exist
- [ ] Clicking "Load More" appends next 20 files
- [ ] Button shows remaining file count
- [ ] Loading indicator during pagination
- [ ] Falls back to 100 files if flag disabled

---

### Task 2: Response Caching (CachedFilesApiService)
**Estimated Time**: 2-3 hours
**Priority**: P1 (Medium Impact)

#### Current Behavior
- Every category call hits API
- Unnecessary network traffic
- Slower UX

#### New Behavior
- Categories cached for 5 minutes
- Cache invalidated on file operations
- Controlled by `EnableCaching` feature flag

#### Implementation Steps

**Step 2.1: Create CachedFilesApiService.cs**

**File**: `src/MediaButler.Mobile/Components/Service/CachedFilesApiService.cs`

```csharp
using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Models;
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
```

**Step 2.2: Register Decorator in MauiProgram.cs**
```csharp
// M5: Caching decorator (applied if EnableCaching=true)
builder.Services.AddMemoryCache();
builder.Services.AddScoped<FilesApiService>(); // Concrete implementation
builder.Services.AddScoped<IFilesApiService>(sp =>
{
    var concrete = sp.GetRequiredService<FilesApiService>();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<CachedFilesApiService>>();
    var flags = sp.GetRequiredService<IOptions<FeatureFlags>>();

    return new CachedFilesApiService(concrete, cache, logger, flags);
});
```

#### Acceptance Criteria
- [ ] First category call → Cache MISS (API called)
- [ ] Second category call within 5min → Cache HIT (no API call)
- [ ] After 5min → Cache expired, API called again
- [ ] Confirm file → Category cache invalidated
- [ ] Flag disabled → No caching (pass-through)

---

### Task 3: Feature Flag Integration
**Estimated Time**: 1 hour
**Priority**: P2 (Low Impact)

#### Implementation Steps

**Step 3.1: Create FeatureFlags Model** (Already exists)
```csharp
// src/MediaButler.Mobile/Models/FeatureFlags.cs
public class FeatureFlags
{
    public bool UseNewApi { get; set; } = true;  // Always true now
    public bool EnableBatchOperations { get; set; } = true;  // Already implemented
    public bool EnablePagination { get; set; } = false;  // NEW: Control pagination
    public bool EnableSignalRService { get; set; } = false;  // Future use
    public bool EnableCaching { get; set; } = false;  // NEW: Control caching
}
```

**Step 3.2: Update appsettings.json** (Already configured)
```json
{
  "FeatureFlags": {
    "UseNewApi": true,
    "EnableBatchOperations": true,
    "EnablePagination": true,    // Enable for testing
    "EnableSignalRService": false,
    "EnableCaching": true         // Enable for testing
  }
}
```

---

## 🎯 SUCCESS CRITERIA

### Performance Metrics
- [ ] Initial load time: <2 seconds (with 20 files)
- [ ] Memory usage: <100MB (tested with 100+ files)
- [ ] Category API: <100ms (cached, second call)
- [ ] "Load More" response: <1 second
- [ ] Smooth scrolling (no frame drops)

### Feature Completeness
- [ ] Pagination works (20 files → Load More → 40 files)
- [ ] Caching works (5-minute TTL)
- [ ] Cache invalidation works (confirm file, batch operation)
- [ ] Feature flags control behavior
- [ ] Fallback to non-optimized mode if flags disabled

### Code Quality
- [ ] Build successful (0 errors)
- [ ] Logging appropriate (cache hits/misses)
- [ ] "Simple Made Easy" - decorator pattern, no braiding
- [ ] Backward compatible (flags can be disabled)

---

## 📊 EXPECTED PERFORMANCE GAINS

### Before M5 (Current)
- Initial load: ~4-5 seconds (100 files)
- Memory: ~150MB (100 files loaded)
- Category calls: ~200ms each (no cache)
- Total API calls: High (every category dropdown)

### After M5 (Optimized)
- Initial load: **~1-2 seconds** (20 files) → **60% faster**
- Memory: **~50MB** (20 files loaded) → **67% reduction**
- Category calls: **~10ms** (cached) → **95% faster**
- Total API calls: **Reduced 80%** (caching)

---

**Document Version**: 1.0
**Last Updated**: 2024-12-24
**Next Review**: After M5 implementation
