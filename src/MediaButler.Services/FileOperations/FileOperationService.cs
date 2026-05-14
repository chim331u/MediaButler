using System.Diagnostics;
using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Common;
using MediaButler.Core.Enums;
using MediaButler.Data.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.FileOperations;

/// <summary>
/// Implementation of atomic file operations with safety guarantees and comprehensive audit trail.
/// Follows "Simple Made Easy" principles with clear separation of concerns and explicit error handling.
/// Designed for ARM32 environments with memory and performance constraints.
/// </summary>
/// <remarks>
/// Key Design Decisions:
/// - Use OS-level atomic operations instead of custom transaction management
/// - Validate exhaustively before executing any operations (fail fast)
/// - Record all operations for audit trail and potential rollback
/// - Handle cross-drive scenarios with copy-then-delete pattern
/// - Provide detailed progress reporting for user feedback
/// 
/// ARM32 Optimizations:
/// - Minimize memory allocations during operations
/// - Use streaming for large file operations
/// - Respect system resource constraints
/// - Provide accurate progress reporting for UI responsiveness
/// </remarks>
public class FileOperationService : IFileOperationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FileOperationService> _logger;
    private readonly SemaphoreSlim _operationSemaphore;
    private readonly Dictionary<string, FileOperationRecord> _activeOperations;
    private readonly object _statsLock = new();

    // ARM32 performance constraints
    private const int MaxConcurrentOperations = 2;
    private const int BufferSize = 65536; // 64KB buffer for file operations
    private const long MinAvailableSpaceBytes = 100 * 1024 * 1024; // 100MB minimum free space

    // Statistics tracking
    private int _completedOperations;
    private int _failedOperations;
    private long _totalBytesMoved;
    private readonly List<TimeSpan> _recentOperationDurations = new();

    public FileOperationService(
        IUnitOfWork unitOfWork,
        ILogger<FileOperationService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operationSemaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
        _activeOperations = new Dictionary<string, FileOperationRecord>();
    }

    /// <inheritdoc />
    public async Task<Result<FileOperationResult>> MoveFileAsync(
        string fileHash, 
        string targetPath, 
        bool createDirectories = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return Result<FileOperationResult>.Failure("File hash cannot be null or empty");

        if (string.IsNullOrWhiteSpace(targetPath))
            return Result<FileOperationResult>.Failure("Target path cannot be null or empty");

        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString();

        _logger.LogInformation("Starting file move operation {OperationId} for file {FileHash} to {TargetPath}",
            operationId, fileHash, targetPath);

        // Acquire semaphore for ARM32 resource management
        await _operationSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Get file from database
            var trackedFile = await _unitOfWork.TrackedFiles.GetByHashAsync(fileHash);
            if (trackedFile == null)
            {
                return Result<FileOperationResult>.Failure($"File with hash {fileHash} not found in database");
            }

            var sourcePath = trackedFile.OriginalPath;
            if (!File.Exists(sourcePath))
            {
                return Result<FileOperationResult>.Failure($"Source file does not exist: {sourcePath}");
            }

            // Create operation record for tracking
            var operation = new FileOperationRecord
            {
                OperationId = operationId,
                FileHash = fileHash,
                OperationType = FileOperationType.Move,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                StartedAt = DateTime.UtcNow
            };

            lock (_activeOperations)
            {
                _activeOperations[operationId] = operation;
            }

            try
            {
                // Validate operation before executing
                var validationResult = await ValidateOperationAsync(fileHash, targetPath, cancellationToken);
                if (!validationResult.IsSuccess || !validationResult.Value.IsValid)
                {
                    var errors = validationResult.IsSuccess ? validationResult.Value.Errors : new List<string> { validationResult.Error };
                    return Result<FileOperationResult>.Failure($"Validation failed: {string.Join(", ", errors)}");
                }

                // Create target directory if requested and needed
                var targetDirectory = Path.GetDirectoryName(targetPath);
                bool directoriesCreated = false;
                
                if (createDirectories && !string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                {
                    var dirResult = await CreateDirectoryStructureAsync(targetDirectory, cancellationToken);
                    if (!dirResult.IsSuccess)
                    {
                        return Result<FileOperationResult>.Failure($"Failed to create target directory: {dirResult.Error}");
                    }
                    directoriesCreated = true;
                }

                // Perform the actual file move operation
                var fileInfo = new FileInfo(sourcePath);
                var fileSizeBytes = fileInfo.Length;
                bool wasCrossDriveOperation = false;

                try
                {
                    // Check if source and target are on the same drive
                    // Convert to full paths first to handle relative paths properly
                    var fullSourcePath = Path.GetFullPath(sourcePath);
                    var fullTargetPath = Path.GetFullPath(targetPath);
                    var sourceRoot = Path.GetPathRoot(fullSourcePath);
                    var targetRoot = Path.GetPathRoot(fullTargetPath);
                    wasCrossDriveOperation = !string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase);

                    _logger.LogDebug("Drive detection: Source={SourcePath} ({SourceRoot}) -> Target={TargetPath} ({TargetRoot}), CrossDrive={CrossDrive}",
                        fullSourcePath, sourceRoot, fullTargetPath, targetRoot, wasCrossDriveOperation);

                    // Check if target file already exists and handle appropriately
                    string finalTargetPath = fullTargetPath;
                    if (File.Exists(fullTargetPath))
                    {
                        _logger.LogWarning("Target file already exists: {TargetPath}. Checking if files are identical.",
                            fullTargetPath);

                        // Compare file hashes to determine if they're the same file
                        var sourceHash = await CalculateFileHashAsync(fullSourcePath, cancellationToken);
                        var targetHash = await CalculateFileHashAsync(fullTargetPath, cancellationToken);

                        if (sourceHash == targetHash)
                        {
                            // Files are identical - skip move, delete source, update database
                            _logger.LogInformation(
                                "Target file {TargetPath} is identical to source (same hash: {Hash}). Skipping move and deleting source.",
                                fullTargetPath, sourceHash);

                            File.Delete(fullSourcePath);
                            _logger.LogDebug("Deleted duplicate source file: {SourcePath}", fullSourcePath);
                            
                            // Use existing target
                            finalTargetPath = fullTargetPath;
                        }
                        else
                        {
                            // Files are different - find a unique suffix
                            var targetDir = Path.GetDirectoryName(fullTargetPath)!;
                            var targetFileName = Path.GetFileNameWithoutExtension(fullTargetPath);
                            var targetExtension = Path.GetExtension(fullTargetPath);

                            var suffix = 1;
                            bool moved = false;
                            while (suffix < 100)
                            {
                                finalTargetPath = Path.Combine(targetDir, $"{targetFileName}_{suffix}{targetExtension}");
                                if (!File.Exists(finalTargetPath))
                                {
                                    try 
                                    {
                                        // Attempt move - even if File.Exists was false, someone else might have grabbed it
                                        // Use overwrite: false to ensure we don't accidentally overwrite concurrent move
                                        File.Move(fullSourcePath, finalTargetPath, overwrite: false);
                                        moved = true;
                                        _logger.LogInformation("Moved file with suffix to {TargetPath}", finalTargetPath);
                                        break;
                                    }
                                    catch (IOException) // Likely file already exists now
                                    {
                                        _logger.LogDebug("Collision detected for suffix {Suffix}, retrying...", suffix);
                                    }
                                }
                                suffix++;
                            }

                            if (!moved)
                            {
                                throw new InvalidOperationException(
                                    $"Cannot generate unique filename with suffix. Too many duplicates exist for: {fullTargetPath}");
                            }
                        }
                    }
                    else
                    {
                        // Target doesn't exist - try to move normally
                        try
                        {
                            if (wasCrossDriveOperation)
                            {
                                await CopyFileAsync(fullSourcePath, finalTargetPath, cancellationToken);
                                File.Delete(fullSourcePath);
                            }
                            else
                            {
                                File.Move(fullSourcePath, finalTargetPath, overwrite: false);
                            }
                        }
                        catch (IOException) when (File.Exists(finalTargetPath))
                        {
                            // Someone else created it just now - recurse or handle once more?
                            // For simplicity, we just throw and let the higher level retry or fail
                            throw new InvalidOperationException($"Target file was created concurrently: {finalTargetPath}");
                        }
                    }

                    // Move related files if any (subtitles, metadata)
                    await MoveRelatedFilesAsync(fullSourcePath, finalTargetPath, wasCrossDriveOperation, cancellationToken);
                    
                    targetPath = finalTargetPath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "File operation failed during move from {SourcePath} to {TargetPath}",
                        sourcePath, targetPath);
                    throw;
                }

                // Update TrackedFile in database
                trackedFile.OriginalPath = targetPath;
                trackedFile.MarkAsModified();
                
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                stopwatch.Stop();
                
                // Create successful result
                var result = new FileOperationResult
                {
                    FileHash = fileHash,
                    SourcePath = sourcePath,
                    TargetPath = targetPath,
                    FileSizeBytes = fileSizeBytes,
                    OperationDuration = stopwatch.Elapsed,
                    WasCrossDriveOperation = wasCrossDriveOperation,
                    DirectoriesCreated = directoriesCreated,
                    CompletedAt = DateTime.UtcNow
                };

                // Update statistics
                UpdateOperationStats(true, stopwatch.Elapsed, fileSizeBytes);

                // Record successful operation
                operation.Success = true;
                operation.CompletedAt = DateTime.UtcNow;
                operation.Duration = stopwatch.Elapsed;
                await RecordOperationAsync(operation, cancellationToken);

                _logger.LogInformation("Successfully completed file move operation {OperationId} in {Duration}ms", 
                    operationId, stopwatch.ElapsedMilliseconds);

                return Result<FileOperationResult>.Success(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Record failed operation
                operation.Success = false;
                operation.ErrorMessage = ex.Message;
                operation.CompletedAt = DateTime.UtcNow;
                operation.Duration = stopwatch.Elapsed;
                
                try
                {
                    await RecordOperationAsync(operation, CancellationToken.None);
                }
                catch (Exception recordEx)
                {
                    _logger.LogError(recordEx, "Failed to record failed operation {OperationId}", operationId);
                }

                // Update statistics
                UpdateOperationStats(false, stopwatch.Elapsed, 0);

                _logger.LogError(ex, "File move operation {OperationId} failed after {Duration}ms", 
                    operationId, stopwatch.ElapsedMilliseconds);

                return Result<FileOperationResult>.Failure($"File move operation failed: {ex.Message}");
            }
            finally
            {
                lock (_activeOperations)
                {
                    _activeOperations.Remove(operationId);
                }
            }
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<DirectoryOperationResult>> CreateDirectoryStructureAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return Result<DirectoryOperationResult>.Failure("Directory path cannot be null or empty");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Creating directory structure: {DirectoryPath}", directoryPath);

        try
        {
            var normalizedPath = Path.GetFullPath(directoryPath);
            var alreadyExisted = Directory.Exists(normalizedPath);
            var createdDirectories = new List<string>();

            if (!alreadyExisted)
            {
                // Find which parent directories need to be created
                var pathsToCreate = new List<string>();
                var currentPath = normalizedPath;
                
                while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
                {
                    pathsToCreate.Add(currentPath);
                    currentPath = Path.GetDirectoryName(currentPath);
                }

                pathsToCreate.Reverse(); // Create from root to leaf

                // Create directories
                foreach (var pathToCreate in pathsToCreate)
                {
                    Directory.CreateDirectory(pathToCreate);
                    createdDirectories.Add(pathToCreate);
                    
                    _logger.LogTrace("Created directory: {Directory}", pathToCreate);
                }
            }

            stopwatch.Stop();

            var result = new DirectoryOperationResult
            {
                DirectoryPath = normalizedPath,
                AlreadyExisted = alreadyExisted,
                CreatedDirectories = createdDirectories,
                OperationDuration = stopwatch.Elapsed,
                CompletedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Directory creation completed for {DirectoryPath} in {Duration}ms. Created {Count} directories",
                directoryPath, stopwatch.ElapsedMilliseconds, createdDirectories.Count);

            return Result<DirectoryOperationResult>.Success(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to create directory structure {DirectoryPath} after {Duration}ms", 
                directoryPath, stopwatch.ElapsedMilliseconds);

            return Result<DirectoryOperationResult>.Failure($"Directory creation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<FileOperationValidationResult>> ValidateOperationAsync(
        string fileHash,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        long requiredSpaceBytes = 0;
        long availableSpaceBytes = 0;
        bool targetExists = false;
        bool requiresDirectoryCreation = false;
        bool isCrossDriveOperation = false;
        var estimatedDuration = TimeSpan.FromMilliseconds(100);

        try
        {
            // Validate file exists in database and on filesystem
            var trackedFile = await _unitOfWork.TrackedFiles.GetByHashAsync(fileHash);
            if (trackedFile == null)
            {
                errors.Add($"File with hash {fileHash} not found in database");
            }
            else
            {
                var sourceFile = new FileInfo(trackedFile.OriginalPath);
                if (!sourceFile.Exists)
                {
                    errors.Add($"Source file does not exist: {trackedFile.OriginalPath}");
                }
                else
                {
                    requiredSpaceBytes = sourceFile.Length;
                }
            }

            // Validate target path
            try
            {
                var fullTargetPath = Path.GetFullPath(targetPath);
                var targetDirectory = Path.GetDirectoryName(fullTargetPath);

                if (string.IsNullOrEmpty(targetDirectory))
                {
                    errors.Add("Invalid target path - cannot determine target directory");
                }
                else
                {
                    // Check if target file already exists
                    if (File.Exists(fullTargetPath))
                    {
                        targetExists = true;
                        warnings.Add($"Target file already exists: {fullTargetPath}");
                    }

                    // Check if target directory exists or needs creation
                    if (!Directory.Exists(targetDirectory))
                    {
                        requiresDirectoryCreation = true;
                    }

                    // Check available disk space
                    try
                    {
                        var driveInfo = new DriveInfo(Path.GetPathRoot(fullTargetPath)!);
                        if (driveInfo.IsReady)
                        {
                            availableSpaceBytes = driveInfo.AvailableFreeSpace;

                            if (driveInfo.AvailableFreeSpace < requiredSpaceBytes + MinAvailableSpaceBytes)
                            {
                                errors.Add($"Insufficient disk space. Available: {driveInfo.AvailableFreeSpace / 1024 / 1024} MB, " +
                                          $"Required: {(requiredSpaceBytes + MinAvailableSpaceBytes) / 1024 / 1024} MB");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Could not check disk space: {ex.Message}");
                    }

                    // Check if it's a cross-drive operation
                    if (trackedFile != null)
                    {
                        var sourceRoot = Path.GetPathRoot(trackedFile.OriginalPath);
                        var targetRoot = Path.GetPathRoot(fullTargetPath);
                        
                        if (!string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            isCrossDriveOperation = true;
                            warnings.Add("Cross-drive operation detected - will use copy+delete method");
                            
                            // Estimate longer duration for cross-drive operations
                            var estimatedSeconds = Math.Max(1, requiredSpaceBytes / (10 * 1024 * 1024)); // ~10MB/s estimate
                            estimatedDuration = TimeSpan.FromSeconds(estimatedSeconds);
                        }
                        else
                        {
                            // Same-drive move is much faster
                            estimatedDuration = TimeSpan.FromMilliseconds(100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid target path: {ex.Message}");
            }

            // Validate path length (Windows limitation)
            if (targetPath.Length > 260)
            {
                warnings.Add("Target path is very long and may cause issues on some systems");
            }

            var finalResult = new FileOperationValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                AvailableSpaceBytes = availableSpaceBytes,
                RequiredSpaceBytes = requiredSpaceBytes,
                TargetExists = targetExists,
                RequiresDirectoryCreation = requiresDirectoryCreation,
                IsCrossDriveOperation = isCrossDriveOperation,
                EstimatedDuration = estimatedDuration
            };

            return Result<FileOperationValidationResult>.Success(finalResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for file {FileHash} and target {TargetPath}", 
                fileHash, targetPath);

            var errorResult = new FileOperationValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Validation error: {ex.Message}" }
            };

            return Result<FileOperationValidationResult>.Success(errorResult);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RecordOperationAsync(
        FileOperationRecord operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create ProcessingLog entry for audit trail
            var logEntry = new ProcessingLog
            {
                FileHash = operation.FileHash,
                Category = "FileOperation",
                Message = $"File {operation.OperationType}: {operation.SourcePath} -> {operation.TargetPath}",
                Details = operation.Success ? "Operation completed successfully" : operation.ErrorMessage,
                Exception = operation.Success ? null : operation.ErrorMessage,
                DurationMs = operation.Duration?.TotalMilliseconds is not null ? (long)operation.Duration.Value.TotalMilliseconds : null,
                Level = operation.Success ? MediaButler.Core.Enums.LogLevel.Information : MediaButler.Core.Enums.LogLevel.Error
            };

            _unitOfWork.ProcessingLogs.Add(logEntry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Recorded operation {OperationId} in audit trail", operation.OperationId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record operation {OperationId} in audit trail", operation.OperationId);
            return Result.Failure($"Failed to record operation: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<FileOperationStats>> GetOperationStatsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_statsLock)
            {
                var activeOperationsList = new List<FileOperationRecord>();
                lock (_activeOperations)
                {
                    activeOperationsList.AddRange(_activeOperations.Values);
                }

                var totalOperations = _completedOperations + _failedOperations;
                var successRate = totalOperations > 0 ? (_completedOperations * 100.0) / totalOperations : 0.0;

                var averageDuration = _recentOperationDurations.Count > 0 
                    ? TimeSpan.FromTicks(_recentOperationDurations.Sum(d => d.Ticks) / _recentOperationDurations.Count)
                    : TimeSpan.Zero;

                // Get available space from most common target location (approximation)
                long availableSpace = 0;
                try
                {
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToArray();
                    availableSpace = drives.Length > 0 ? drives.Max(d => d.AvailableFreeSpace) : 0;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not determine available disk space");
                }

                var stats = new FileOperationStats
                {
                    ActiveOperations = activeOperationsList.Count,
                    CompletedOperations = _completedOperations,
                    FailedOperations = _failedOperations,
                    SuccessRatePercent = successRate,
                    AverageOperationDuration = averageDuration,
                    TotalBytesMoved = _totalBytesMoved,
                    AvailableSpaceBytes = availableSpace,
                    RecentOperations = activeOperationsList,
                    CollectedAt = DateTime.UtcNow
                };

                return Result<FileOperationStats>.Success(stats);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get operation statistics");
            return Result<FileOperationStats>.Failure($"Failed to get statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Copies a file asynchronously with progress reporting, optimized for ARM32.
    /// </summary>
    private static async Task CopyFileAsync(string sourcePath, string targetPath, CancellationToken cancellationToken)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

        var buffer = new byte[BufferSize];
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer, 0, bytesRead, cancellationToken);
        }

        await target.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Updates operation statistics in a thread-safe manner.
    /// </summary>
    private void UpdateOperationStats(bool success, TimeSpan duration, long bytesProcessed)
    {
        lock (_statsLock)
        {
            if (success)
            {
                _completedOperations++;
                _totalBytesMoved += bytesProcessed;
            }
            else
            {
                _failedOperations++;
            }

            _recentOperationDurations.Add(duration);
            
            // Keep only recent durations for performance (last 100 operations)
            if (_recentOperationDurations.Count > 100)
            {
                _recentOperationDurations.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Discovers and moves related files (subtitles, metadata) for a given media file.
    /// </summary>
    private async Task MoveRelatedFilesAsync(string sourcePath, string targetPath, bool wasCrossDriveOperation, CancellationToken cancellationToken)
    {
        try
        {
            var sourceDir = Path.GetDirectoryName(sourcePath);
            var targetDir = Path.GetDirectoryName(targetPath);
            var sourceBaseName = Path.GetFileNameWithoutExtension(sourcePath);
            var targetBaseName = Path.GetFileNameWithoutExtension(targetPath);

            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(sourceBaseName))
                return;

            // Common media-related extensions
            var relatedExtensions = new[] { ".srt", ".sub", ".ass", ".nfo", ".jpg", ".png", ".xml" };

            foreach (var ext in relatedExtensions)
            {
                var sourceRelated = Path.Combine(sourceDir, sourceBaseName + ext);
                if (File.Exists(sourceRelated))
                {
                    var targetRelated = Path.Combine(targetDir, targetBaseName + ext);
                    _logger.LogDebug("Moving related file: {Source} -> {Target}", sourceRelated, targetRelated);

                    try
                    {
                        if (wasCrossDriveOperation)
                        {
                            await CopyFileAsync(sourceRelated, targetRelated, cancellationToken);
                            File.Delete(sourceRelated);
                        }
                        else
                        {
                            File.Move(sourceRelated, targetRelated, overwrite: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to move related file {SourceRelated}. Skipping.", sourceRelated);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during related files discovery/move for {SourcePath}", sourcePath);
        }
    }

    /// <summary>
    /// Calculates SHA256 hash of a file for duplicate detection.
    /// </summary>
    private static async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = System.Security.Cryptography.SHA256.Create();

        var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Dispose of resources properly.
    /// </summary>
    public void Dispose()
    {
        _operationSemaphore?.Dispose();
    }
}