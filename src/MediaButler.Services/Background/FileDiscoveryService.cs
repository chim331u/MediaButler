using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MediaButler.Core.Common;
using MediaButler.Services.Interfaces;
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
    private readonly IFileService _fileService;
    private readonly IFileProcessingQueue _processingQueue;
    private readonly ILogger<FileDiscoveryService> _logger;
    
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _pendingFiles = new();
    private readonly Timer? _scanTimer;
    private readonly Timer? _debounceTimer;
    private readonly SemaphoreSlim _scanSemaphore;
    
    private volatile bool _isMonitoring;
    private volatile bool _disposed;

    public FileDiscoveryService(
        IOptions<FileDiscoveryConfiguration> config,
        IFileService fileService,
        IFileProcessingQueue processingQueue,
        ILogger<FileDiscoveryService> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _processingQueue = processingQueue ?? throw new ArgumentNullException(nameof(processingQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

        _logger.LogInformation(
            "File Discovery Service initialized. Watching {FolderCount} folders, {ExtensionCount} extensions",
            _config.WatchFolders.Count, _config.FileExtensions.Count);
    }

    /// <inheritdoc />
    public bool IsMonitoring => _isMonitoring;

    /// <inheritdoc />
    public IEnumerable<string> MonitoredPaths => _config.WatchFolders.AsReadOnly();

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
            _logger.LogInformation("Starting file monitoring for {FolderCount} folders", _config.WatchFolders.Count);

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

            // Stop and dispose all file system watchers (make a copy to avoid collection modification)
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
                    _logger.LogWarning(ex, "Error disposing FileSystemWatcher");
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

    /// <inheritdoc />
    public async Task<Result<int>> ScanFoldersAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Result<int>.Failure("Service has been disposed");

        var totalDiscovered = 0;
        var scannedFolders = 0;

        _logger.LogDebug("Starting folder scan for {FolderCount} folders", _config.WatchFolders.Count);

        try
        {
            var scanTasks = _config.WatchFolders.Select(async folder =>
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
    private async Task<Result<int>> ScanSingleFolderAsync(string folderPath, CancellationToken cancellationToken)
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
    /// Sets up FileSystemWatcher instances for all configured folders.
    /// </summary>
    private async Task SetupFileSystemWatchersAsync(CancellationToken cancellationToken)
    {
        foreach (var folderPath in _config.WatchFolders)
        {
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("Watch folder does not exist, attempting to create: {Folder}", folderPath);
                try
                {
                    Directory.CreateDirectory(folderPath);
                    _logger.LogInformation("Created watch folder: {Folder}", folderPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create watch folder: {Folder}", folderPath);
                    OnDiscoveryError(null, $"Failed to create watch folder: {folderPath}", ex);
                    continue;
                }
            }

            try
            {
                var watcher = new FileSystemWatcher(folderPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnFileSystemEvent;
                watcher.Renamed += OnFileSystemEvent;
                watcher.Error += OnFileSystemError;

                _watchers.Add(watcher);
                _logger.LogDebug("FileSystemWatcher setup for folder: {Folder}", folderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup FileSystemWatcher for folder: {Folder}", folderPath);
                OnDiscoveryError(null, $"Failed to setup watcher for folder: {folderPath}", ex);
            }
        }

        _logger.LogInformation("FileSystemWatcher setup completed for {WatcherCount} folders", _watchers.Count);
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
    /// </summary>
    private async Task<bool> ShouldProcessFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        // Check file extension
        var extension = Path.GetExtension(filePath);
        if (!_config.IsExtensionMonitored(extension))
            return false;

        // Check exclusion patterns
        if (_config.IsFileExcluded(filePath))
        {
            _logger.LogDebug("File excluded by pattern: {FilePath}", filePath);
            return false;
        }

        // Check file size
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            
            if (fileSizeMB < _config.MinFileSizeMB)
            {
                _logger.LogDebug("File too small ({SizeMB:F2}MB < {MinSizeMB}MB): {FilePath}", 
                    fileSizeMB, _config.MinFileSizeMB, filePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check file size: {FilePath}", filePath);
            return false;
        }

        // Check if already tracked
        try
        {
            var alreadyTracked = await _fileService.IsFileAlreadyTrackedAsync(filePath);
            if (alreadyTracked.IsSuccess && alreadyTracked.Value)
            {
                _logger.LogDebug("File already tracked: {FilePath}", filePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check if file is already tracked: {FilePath}", filePath);
            // Continue processing to avoid missing files due to service issues
        }

        return true;
    }

    /// <summary>
    /// Raises the FileDiscovered event and queues the file for processing.
    /// </summary>
    private async void OnFileDiscovered(string filePath)
    {
        try
        {
            // Register the file with FileService
            var registrationResult = await _fileService.RegisterFileAsync(filePath);
            
            if (registrationResult.IsSuccess)
            {
                // Queue the file for processing
                await _processingQueue.EnqueueAsync(registrationResult.Value);
                
                // Raise the event
                FileDiscovered?.Invoke(this, new FileDiscoveredEventArgs(filePath, DateTime.UtcNow));
                
                _logger.LogInformation("File discovered and queued for processing: {FilePath}", filePath);
            }
            else
            {
                _logger.LogWarning("Failed to register discovered file {FilePath}: {Error}", 
                    filePath, registrationResult.Error);
                OnDiscoveryError(filePath, $"Failed to register file: {registrationResult.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing discovered file: {FilePath}", filePath);
            OnDiscoveryError(filePath, "Error processing discovered file", ex);
        }
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
        _scanSemaphore?.Dispose();

        var watchersToDispose = _watchers.ToArray();
        foreach (var watcher in watchersToDispose)
        {
            watcher?.Dispose();
        }
        _watchers.Clear();

        _logger.LogInformation("File Discovery Service disposed");
    }
}