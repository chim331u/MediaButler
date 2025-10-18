using System.Collections.Concurrent;
using MediaButler.Core.Common;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaButler.Services.Background;

/// <summary>
/// Service for discovering files in configured watch folders.
/// Uses Hangfire-based polling instead of FileSystemWatcher for reliability.
/// Follows "Simple Made Easy" principles with clear separation of file system concerns.
/// </summary>
public class FileDiscoveryService : IFileDiscoveryService, IDisposable
{
    private readonly FileDiscoveryConfiguration _config;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IFileProcessingQueue _processingQueue;
    private readonly ILogger<FileDiscoveryService> _logger;
    private readonly ARM32MemoryMonitor _memoryMonitor;

    private readonly SemaphoreSlim _scanSemaphore;

    // ARM32 optimization: Batch processing for file discovery
    private readonly List<string> _discoveredFilesBatch = new();
    private readonly object _batchLock = new();

    // File validation cache
    private readonly Dictionary<string, FileValidationResult> _validationCache = new();
    private readonly object _cacheRwLock = new();
    private DateTime _lastCacheCleanup = DateTime.UtcNow;

    private volatile bool _disposed;
    private List<string> _currentWatchFolders = new();

    public FileDiscoveryService(
        IOptions<FileDiscoveryConfiguration> config,
        IServiceScopeFactory serviceScopeFactory,
        IFileProcessingQueue processingQueue,
        ILogger<FileDiscoveryService> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _processingQueue = processingQueue ?? throw new ArgumentNullException(nameof(processingQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize ARM32 memory monitor for QNAP TS-231P
        _memoryMonitor = new ARM32MemoryMonitor(logger);

        // Validate configuration
        var validationErrors = _config.Validate().ToList();
        if (validationErrors.Any())
        {
            var errors = string.Join("; ", validationErrors.Select(e => e.ErrorMessage));
            throw new ArgumentException($"Invalid file discovery configuration: {errors}");
        }

        // ARM32 resource management
        _scanSemaphore = new SemaphoreSlim(_config.MaxConcurrentScans, _config.MaxConcurrentScans);

        // Initialize watch folders from configuration
        _currentWatchFolders = _config.WatchFolders.ToList();

        _logger.LogInformation(
            "File Discovery Service initialized (Hangfire polling). Watching {FolderCount} folders, {ExtensionCount} extensions, Scan interval: {ScanInterval} minutes",
            _config.WatchFolders.Count, _config.FileExtensions.Count, _config.ScanIntervalMinutes);
    }

    /// <inheritdoc />
    public bool IsMonitoring => !_disposed;

    /// <inheritdoc />
    public IEnumerable<string> MonitoredPaths => _currentWatchFolders.AsReadOnly();

    /// <inheritdoc />
    public event EventHandler<FileDiscoveredEventArgs>? FileDiscovered;

    /// <inheritdoc />
    public event EventHandler<FileDiscoveryErrorEventArgs>? DiscoveryError;

    /// <inheritdoc />
    public Task<Result> StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Task.FromResult(Result.Failure("Service has been disposed"));

        _logger.LogInformation("File monitoring uses Hangfire recurring job. No FileSystemWatcher required.");
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result> StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("File monitoring stop requested. Hangfire recurring job will be managed separately.");
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Gets watch folders from static configuration (appsettings.json).
    /// </summary>
    private List<string> GetWatchFoldersFromConfiguration()
    {
        _logger.LogDebug("Using watch folders from static configuration: {Folders}",
            string.Join(", ", _config.WatchFolders));
        return _config.WatchFolders.ToList();
    }

    public async Task<Result<int>> ScanFoldersAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Result<int>.Failure("Service has been disposed");

        var totalDiscovered = 0;
        var scannedFolders = 0;

        // Get watch folders from static configuration
        var watchFolders = GetWatchFoldersFromConfiguration();

        _logger.LogDebug("Starting folder scan for {FolderCount} folders", watchFolders.Count);

        try
        {
            var scanTasks = watchFolders.Select(async folder =>
            {
                await _scanSemaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ScanSingleFolderAsync(folder, cancellationToken);
                }
                finally
                {
                    _scanSemaphore.Release();
                }
            });

            var results = await Task.WhenAll(scanTasks);

            foreach (var result in results)
            {
                scannedFolders++;
                if (result.IsSuccess)
                {
                    totalDiscovered += result.Value;
                }
                else
                {
                    _logger.LogWarning("Folder scan failed: {Error}", result.Error);
                }
            }

            // Process any batched files immediately after scan
            await ProcessBatchedFilesAsync();

            _logger.LogInformation(
                "Folder scan completed. Scanned {ScannedFolders}/{TotalFolders} folders, discovered {FileCount} files",
                scannedFolders, watchFolders.Count, totalDiscovered);

            return Result<int>.Success(totalDiscovered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during folder scan");
            return Result<int>.Failure($"Folder scan failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Scans a single folder for files matching the configured criteria.
    /// </summary>
    public async Task<Result<int>> ScanSingleFolderAsync(string folderPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            return Result<int>.Failure($"Folder does not exist: {folderPath}");
        }

        try
        {
            var discovered = 0;

            foreach (var extension in _config.FileExtensions)
            {
                var pattern = $"*{extension}";
                var files = Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories);

                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await ShouldProcessFileAsync(filePath))
                    {
                        OnFileDiscovered(filePath);
                        discovered++;
                    }
                }
            }

            _logger.LogDebug("Scanned folder {Folder}: {FileCount} files discovered", folderPath, discovered);
            return Result<int>.Success(discovered);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Folder scan cancelled for {Folder}", folderPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning folder {Folder}", folderPath);
            return Result<int>.Failure($"Error scanning {folderPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines if a file should be processed based on configured criteria.
    /// ARM32 optimization: Uses caching and skips redundant checks for known file types.
    /// </summary>
    private async Task<bool> ShouldProcessFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        // Check file extension early (fast operation)
        var extension = Path.GetExtension(filePath);
        if (!_config.IsExtensionMonitored(extension))
            return false;

        // Check exclusion patterns (fast regex operation)
        if (_config.IsFileExcluded(filePath))
        {
            _logger.LogDebug("ARM32: File excluded by pattern: {FilePath}", filePath);
            return false;
        }

        // ARM32 optimization: Check validation cache first
        var cachedResult = GetCachedValidationResult(filePath);
        if (cachedResult != null)
        {
            _logger.LogDebug("ARM32: Using cached validation for {FilePath}: {IsValid}", filePath, cachedResult.IsValid);
            return cachedResult.IsValid;
        }

        // Perform validation and cache result
        var validationResult = await ValidateFileWithCachingAsync(filePath);
        return validationResult;
    }

    /// <summary>
    /// Validates file with caching to reduce I/O operations.
    /// ARM32 optimization: Skip redundant file size checks for known large file types.
    /// </summary>
    private async Task<bool> ValidateFileWithCachingAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        var isKnownLargeType = IsKnownLargeFileType(extension);

        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileLastModified = fileInfo.LastWriteTime;

            // ARM32 optimization: Skip file size check for known large video file types
            bool skipSizeCheck = isKnownLargeType;
            long fileSizeBytes = 0;
            bool sizeValid = true;

            if (!skipSizeCheck)
            {
                fileSizeBytes = fileInfo.Length;
                var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                sizeValid = fileSizeMB >= _config.MinFileSizeMB;

                if (!sizeValid)
                {
                    _logger.LogDebug("ARM32: File too small ({SizeMB:F2}MB < {MinSizeMB}MB): {FilePath}",
                        fileSizeMB, _config.MinFileSizeMB, filePath);
                }
            }
            else
            {
                // For known large types, assume size is valid
                fileSizeBytes = fileInfo.Length;
                _logger.LogDebug("ARM32: Skipping size check for known large file type {Extension}: {FilePath}",
                    extension, filePath);
            }

            bool isValid = sizeValid;
            string? errorReason = sizeValid ? null : "File too small";

            // Check if already tracked (expensive operation, so do it last)
            if (isValid)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();

                    var alreadyTracked = await fileService.IsFileAlreadyTrackedAsync(filePath);
                    if (alreadyTracked.IsSuccess && alreadyTracked.Value)
                    {
                        isValid = false;
                        errorReason = "Already tracked";
                        _logger.LogDebug("ARM32: File already tracked: {FilePath}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ARM32: Could not check if file is already tracked: {FilePath}", filePath);
                    // Continue processing to avoid missing files due to service issues
                }
            }

            // Cache the validation result
            CacheValidationResult(filePath, new FileValidationResult
            {
                IsValid = isValid,
                ErrorReason = errorReason,
                FileSizeBytes = fileSizeBytes,
                FileLastModified = fileLastModified
            });

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ARM32: Could not validate file: {FilePath}", filePath);

            // Cache negative result to avoid repeated failures
            CacheValidationResult(filePath, new FileValidationResult
            {
                IsValid = false,
                ErrorReason = ex.Message,
                FileSizeBytes = 0,
                FileLastModified = DateTime.MinValue
            });

            return false;
        }
    }

    /// <summary>
    /// Checks if a file extension represents a known large file type that can skip size validation.
    /// ARM32 optimization: Avoid expensive I/O for file types that are typically large.
    /// </summary>
    private static bool IsKnownLargeFileType(string extension)
    {
        var knownLargeTypes = new[] { ".mkv", ".mp4", ".avi", ".m4v", ".mov", ".wmv", ".flv", ".webm" };
        return knownLargeTypes.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets cached validation result if available and still valid.
    /// </summary>
    private FileValidationResult? GetCachedValidationResult(string filePath)
    {
        lock (_cacheRwLock)
        {
            if (_validationCache.TryGetValue(filePath, out var cachedResult))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (cachedResult.IsCacheValid(fileInfo.LastWriteTime))
                    {
                        return cachedResult;
                    }
                    else
                    {
                        // Cache expired or file modified, remove it
                        _validationCache.Remove(filePath);
                    }
                }
                catch
                {
                    // File may not exist anymore, remove from cache
                    _validationCache.Remove(filePath);
                }
            }

            // Periodic cache cleanup (every 10 minutes)
            if (DateTime.UtcNow.Subtract(_lastCacheCleanup).TotalMinutes > 10)
            {
                CleanupValidationCache();
                _lastCacheCleanup = DateTime.UtcNow;
            }
        }

        return null;
    }

    /// <summary>
    /// Caches validation result for future use.
    /// </summary>
    private void CacheValidationResult(string filePath, FileValidationResult result)
    {
        lock (_cacheRwLock)
        {
            _validationCache[filePath] = result;

            // Limit cache size for ARM32 memory management
            if (_validationCache.Count > 1000)
            {
                CleanupValidationCache();
            }
        }
    }

    /// <summary>
    /// Cleans up expired validation cache entries.
    /// ARM32 optimization: Keep cache size manageable.
    /// </summary>
    private void CleanupValidationCache()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-15);
        var keysToRemove = _validationCache
            .Where(kvp => kvp.Value.CachedAt < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _validationCache.Remove(key);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("ARM32: Cleaned up {Count} expired validation cache entries", keysToRemove.Count);
        }
    }

    /// <summary>
    /// Raises the FileDiscovered event and queues the file for processing.
    /// ARM32 optimization: Adds files to batch for processing instead of immediate processing.
    /// </summary>
    private void OnFileDiscovered(string filePath)
    {
        // ARM32 optimization: Add to batch instead of immediate processing
        lock (_batchLock)
        {
            _discoveredFilesBatch.Add(filePath);
            _logger.LogDebug("ARM32: File added to discovery batch: {FilePath} (batch size: {BatchSize})",
                filePath, _discoveredFilesBatch.Count);
        }
    }

    /// <summary>
    /// ARM32 optimization: Processes discovered files in batches of 5 to reduce memory pressure.
    /// </summary>
    private async Task ProcessBatchedFilesAsync()
    {
        if (_disposed) return;

        List<string> batchToProcess;
        lock (_batchLock)
        {
            if (_discoveredFilesBatch.Count == 0) return;

            batchToProcess = _discoveredFilesBatch.ToList();
            _discoveredFilesBatch.Clear();
        }

        // ARM32: Check memory before processing batch
        if (_memoryMonitor.ShouldThrottleProcessing())
        {
            _logger.LogWarning("ARM32: Memory pressure detected, waiting before processing batch of {Count} files", batchToProcess.Count);

            if (!await _memoryMonitor.WaitForMemoryAvailableAsync())
            {
                _logger.LogError("ARM32: Could not resolve memory pressure, re-queuing {Count} files", batchToProcess.Count);
                lock (_batchLock)
                {
                    _discoveredFilesBatch.InsertRange(0, batchToProcess);
                }
                return;
            }
        }

        _logger.LogInformation("ARM32: Processing batch of {Count} discovered files", batchToProcess.Count);

        foreach (var filePath in batchToProcess)
        {
            try
            {
                // Track operation for periodic GC
                _memoryMonitor.TrackOperation();

                // Register the file with FileService using scoped service
                using var scope = _serviceScopeFactory.CreateScope();
                var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();

                var registrationResult = await fileService.RegisterFileAsync(filePath);

                if (registrationResult.IsSuccess)
                {
                    // Queue the file for processing
                    await _processingQueue.EnqueueAsync(registrationResult.Value);

                    // Raise the event
                    FileDiscovered?.Invoke(this, new FileDiscoveredEventArgs(filePath, DateTime.UtcNow));

                    _logger.LogDebug("ARM32: File registered and queued: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("ARM32: Failed to register file {FilePath}: {Error}",
                        filePath, registrationResult.Error);
                    OnDiscoveryError(filePath, $"Failed to register file: {registrationResult.Error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ARM32: Error processing file {FilePath}", filePath);
                OnDiscoveryError(filePath, "Error processing discovered file", ex);
            }
        }

        _logger.LogInformation("ARM32: Completed batch processing of {Count} files", batchToProcess.Count);
    }

    /// <summary>
    /// Raises the DiscoveryError event.
    /// </summary>
    private void OnDiscoveryError(string? filePath, string errorMessage, Exception? exception = null)
    {
        try
        {
            DiscoveryError?.Invoke(this, new FileDiscoveryErrorEventArgs(filePath, errorMessage, exception));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising DiscoveryError event");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _scanSemaphore?.Dispose();

        // Clear validation cache for ARM32 memory management
        lock (_cacheRwLock)
        {
            _validationCache.Clear();
        }

        _logger.LogInformation("File Discovery Service disposed (Hangfire polling mode)");
    }
}

/// <summary>
/// Cached validation result for file system optimization.
/// Reduces repeated I/O operations for file validation.
/// </summary>
internal class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorReason { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public DateTime FileLastModified { get; set; }

    /// <summary>
    /// Check if cache entry is still valid (file not modified, cache not expired).
    /// </summary>
    public bool IsCacheValid(DateTime fileLastModified, int cacheValidityMinutes = 5)
    {
        var cacheExpiry = CachedAt.AddMinutes(cacheValidityMinutes);
        return DateTime.UtcNow <= cacheExpiry && FileLastModified == fileLastModified;
    }
}
