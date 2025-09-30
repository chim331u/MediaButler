using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MediaButler.Core.Common;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaButler.Services.Background;

/// <summary>
/// Service for discovering and monitoring files in configured watch folders.
/// Implements FileSystemWatcher for real-time detection and periodic scanning as backup.
/// Follows "Simple Made Easy" principles with clear separation of file system concerns.
/// </summary>
public class FileDiscoveryService : IFileDiscoveryService, IDisposable
{
    private readonly FileDiscoveryConfiguration _config;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IFileProcessingQueue _processingQueue;
    private readonly ILogger<FileDiscoveryService> _logger;
    private readonly ARM32MemoryMonitor _memoryMonitor;
    
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _pendingFiles = new();
    private readonly Timer? _scanTimer;
    private readonly Timer? _debounceTimer;
    private readonly SemaphoreSlim _scanSemaphore;

    // ARM32 optimization: Batch processing for file discovery
    private readonly List<string> _discoveredFilesBatch = new();
    private readonly Timer? _batchProcessingTimer;
    private readonly object _batchLock = new();

    // File system optimization: Single watcher and validation cache
    private FileSystemWatcher? _singleWatcher;
    private readonly Dictionary<string, FileValidationResult> _validationCache = new();
    private readonly object _cacheRwLock = new();
    private DateTime _lastCacheCleanup = DateTime.UtcNow;
    
    private volatile bool _isMonitoring;
    private volatile bool _disposed;
    private readonly object _watchersLock = new();
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

        // Setup periodic scanning timer
        if (_config.ScanIntervalMinutes > 0)
        {
            _scanTimer = new Timer(
                PerformPeriodicScan,
                null,
                TimeSpan.FromMinutes(_config.ScanIntervalMinutes),
                TimeSpan.FromMinutes(_config.ScanIntervalMinutes));
        }

        // Setup debounce processing timer
        _debounceTimer = new Timer(
            ProcessPendingFiles,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));

        // ARM32 optimization: Setup batch processing timer for discovered files
        _batchProcessingTimer = new Timer(
            ProcessBatchedFiles,
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));

        _logger.LogInformation(
            "File Discovery Service initialized. Watching {FolderCount} folders, {ExtensionCount} extensions",
            _config.WatchFolders.Count, _config.FileExtensions.Count);
    }

    /// <inheritdoc />
    public bool IsMonitoring => _isMonitoring;

    /// <inheritdoc />
    public IEnumerable<string> MonitoredPaths
    {
        get
        {
            lock (_watchersLock)
            {
                return _currentWatchFolders.AsReadOnly();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<FileDiscoveredEventArgs>? FileDiscovered;

    /// <inheritdoc />
    public event EventHandler<FileDiscoveryErrorEventArgs>? DiscoveryError;

    /// <inheritdoc />
    public async Task<Result> StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Result.Failure("Service has been disposed");

        if (_isMonitoring)
            return Result.Failure("Monitoring is already active");

        try
        {
            // Initialize current watch folders from config, then check database
            lock (_watchersLock)
            {
                _currentWatchFolders = _config.WatchFolders.ToList();
            }

            // Get watch folders from static configuration
            var configWatchFolders = GetWatchFoldersFromConfiguration();
            lock (_watchersLock)
            {
                _currentWatchFolders = configWatchFolders.ToList();
            }

            _logger.LogInformation("Starting file monitoring for {FolderCount} folders", _currentWatchFolders.Count);

            // Setup FileSystemWatcher for each configured folder
            if (_config.EnableFileSystemWatcher)
            {
                await SetupFileSystemWatchersAsync(cancellationToken);
            }

            // Perform initial scan
            var scanResult = await ScanFoldersAsync(cancellationToken);
            if (!scanResult.IsSuccess)
            {
                _logger.LogWarning("Initial folder scan failed: {Error}", scanResult.Error);
            }

            _isMonitoring = true;
            _logger.LogInformation(
                "File monitoring started successfully. FileSystemWatcher: {WatcherEnabled}, Initial scan: {ScanResult}",
                _config.EnableFileSystemWatcher, scanResult.IsSuccess ? $"{scanResult.Value} files" : "failed");

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start file monitoring");
            return Result.Failure($"Failed to start monitoring: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (!_isMonitoring)
            return Result.Success();

        try
        {
            _logger.LogInformation("Stopping file monitoring");

            // Stop and dispose single file system watcher
            if (_singleWatcher != null)
            {
                try
                {
                    _singleWatcher.EnableRaisingEvents = false;
                    _singleWatcher.Dispose();
                    _singleWatcher = null;
                    _logger.LogDebug("ARM32: Single FileSystemWatcher disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing single FileSystemWatcher");
                }
            }

            // Legacy cleanup for any remaining watchers
            var watchersToDispose = _watchers.ToArray();
            foreach (var watcher in watchersToDispose)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing legacy FileSystemWatcher");
                }
            }
            _watchers.Clear();

            // Process any remaining pending files
            ProcessPendingFiles(null);

            _isMonitoring = false;
            _logger.LogInformation("File monitoring stopped successfully");

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop file monitoring");
            return Result.Failure($"Failed to stop monitoring: {ex.Message}");
        }
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

            _logger.LogInformation(
                "Folder scan completed. Scanned {ScannedFolders}/{TotalFolders} folders, discovered {FileCount} files",
                scannedFolders, _config.WatchFolders.Count, totalDiscovered);

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
            var searchPattern = string.Join("|", _config.FileExtensions);
            
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
    /// Sets up a single FileSystemWatcher for all configured folders.
    /// ARM32 optimization: Use single watcher to reduce resource usage.
    /// </summary>
    private async Task SetupFileSystemWatchersAsync(CancellationToken cancellationToken)
    {
        List<string> foldersToWatch;
        lock (_watchersLock)
        {
            foldersToWatch = _currentWatchFolders.ToList();
        }

        if (foldersToWatch.Count == 0)
        {
            _logger.LogWarning("No watch folders configured");
            return;
        }

        // ARM32 optimization: Use single watcher for the primary folder
        var primaryFolder = foldersToWatch[0];

        if (!Directory.Exists(primaryFolder))
        {
            _logger.LogWarning("Primary watch folder does not exist, attempting to create: {Folder}", primaryFolder);
            try
            {
                Directory.CreateDirectory(primaryFolder);
                _logger.LogInformation("Created primary watch folder: {Folder}", primaryFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create primary watch folder: {Folder}", primaryFolder);
                OnDiscoveryError(null, $"Failed to create primary watch folder: {primaryFolder}", ex);
                return;
            }
        }

        try
        {
            // Dispose existing watcher if any
            _singleWatcher?.Dispose();

            _singleWatcher = new FileSystemWatcher(primaryFolder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _singleWatcher.Created += OnFileSystemEvent;
            _singleWatcher.Renamed += OnFileSystemEvent;
            _singleWatcher.Error += OnFileSystemError;

            _logger.LogInformation("ARM32: Single FileSystemWatcher setup for primary folder: {Folder}", primaryFolder);

            // Log additional folders that will be handled by periodic scanning only
            if (foldersToWatch.Count > 1)
            {
                var additionalFolders = foldersToWatch.Skip(1).ToList();
                _logger.LogInformation("ARM32: Additional folders {Count} will be handled by periodic scanning: {Folders}",
                    additionalFolders.Count, string.Join(", ", additionalFolders));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup single FileSystemWatcher for folder: {Folder}", primaryFolder);
            OnDiscoveryError(null, $"Failed to setup watcher for folder: {primaryFolder}", ex);
        }
    }

    /// <summary>
    /// Handles FileSystemWatcher events for file creation and renaming.
    /// </summary>
    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (_disposed || !_isMonitoring)
            return;

        try
        {
            // Add to debounce queue for processing
            _pendingFiles[e.FullPath] = DateTime.UtcNow;
            
            _logger.LogDebug("File system event: {EventType} - {FilePath}", e.ChangeType, e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file system event for: {FilePath}", e.FullPath);
            OnDiscoveryError(e.FullPath, "Error handling file system event", ex);
        }
    }

    /// <summary>
    /// Handles FileSystemWatcher error events.
    /// </summary>
    private void OnFileSystemError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error occurred");
        OnDiscoveryError(null, "FileSystemWatcher error", e.GetException());
    }

    /// <summary>
    /// Processes pending files that have passed the debounce delay.
    /// </summary>
    private async void ProcessPendingFiles(object? state)
    {
        if (_disposed || _pendingFiles.IsEmpty)
            return;

        var cutoffTime = DateTime.UtcNow.AddSeconds(-_config.DebounceDelaySeconds);
        var filesToProcess = new List<string>();

        // Identify files that have passed debounce delay
        foreach (var kvp in _pendingFiles)
        {
            if (kvp.Value <= cutoffTime)
            {
                filesToProcess.Add(kvp.Key);
            }
        }

        // Process identified files
        foreach (var filePath in filesToProcess)
        {
            _pendingFiles.TryRemove(filePath, out _);

            try
            {
                if (await ShouldProcessFileAsync(filePath))
                {
                    OnFileDiscovered(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending file: {FilePath}", filePath);
                OnDiscoveryError(filePath, "Error processing pending file", ex);
            }
        }
    }

    /// <summary>
    /// Timer callback for periodic folder scanning.
    /// </summary>
    private async void PerformPeriodicScan(object? state)
    {
        if (_disposed || !_isMonitoring)
            return;

        _logger.LogDebug("Performing periodic folder scan");
        
        try
        {
            var result = await ScanFoldersAsync();
            if (result.IsSuccess)
            {
                _logger.LogDebug("Periodic scan completed: {FileCount} files discovered", result.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic folder scan");
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

            // If batch is full, process immediately
            if (_discoveredFilesBatch.Count >= 5)
            {
                ProcessBatchedFiles(null);
            }
        }
    }

    /// <summary>
    /// ARM32 optimization: Processes discovered files in batches of 5 to reduce memory pressure.
    /// </summary>
    private async void ProcessBatchedFiles(object? state)
    {
        if (_disposed) return;

        List<string> batchToProcess;
        lock (_batchLock)
        {
            if (_discoveredFilesBatch.Count == 0) return;

            // ARM32: Take up to 5 files for batch processing
            batchToProcess = _discoveredFilesBatch.Take(5).ToList();
            _discoveredFilesBatch.RemoveRange(0, batchToProcess.Count);
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

        _ = StopMonitoringAsync();

        _scanTimer?.Dispose();
        _debounceTimer?.Dispose();
        _batchProcessingTimer?.Dispose();
        _scanSemaphore?.Dispose();

        // Dispose single watcher
        _singleWatcher?.Dispose();

        // Legacy cleanup for any remaining watchers
        var watchersToDispose = _watchers.ToArray();
        foreach (var watcher in watchersToDispose)
        {
            watcher?.Dispose();
        }
        _watchers.Clear();

        // Clear validation cache for ARM32 memory management
        lock (_cacheRwLock)
        {
            _validationCache.Clear();
        }

        _logger.LogInformation("File Discovery Service disposed");
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