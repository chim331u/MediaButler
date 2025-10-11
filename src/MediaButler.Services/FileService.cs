using MediaButler.Core.Common;
using MediaButler.Core.Configuration;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Security.Cryptography;

namespace MediaButler.Services;

/// <summary>
/// Service implementation for file management operations in the MediaButler system.
/// Provides high-level business operations for tracked files following "Simple Made Easy" principles
/// by maintaining single responsibility and avoiding complecting of concerns.
/// </summary>
public class FileService(
    ITrackedFileRepository trackedFileRepository,
    IUnitOfWork unitOfWork,
    IPathGenerationService pathGenerationService,
    IMediaButlerConfiguration configuration,
    ILogger<FileService> logger) : IFileService
{
    // Validation constants
    private const decimal MinConfidence = 0.0m;
    private const decimal MaxConfidence = 1.0m;

    // Default limits for batch operations
    private const int DefaultClassificationLimit = 100;
    private const int DefaultMovingLimit = 50;
    private const int DefaultRecentlyMovedHours = 24;
    private const int MaxPageSize = 1000;

    private readonly ITrackedFileRepository _trackedFileRepository = trackedFileRepository ?? throw new ArgumentNullException(nameof(trackedFileRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IPathGenerationService _pathGenerationService = pathGenerationService ?? throw new ArgumentNullException(nameof(pathGenerationService));
    private readonly IMediaButlerConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger<FileService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Registers a new file for tracking based on its file path.
    /// Implements idempotent registration: if file hash already exists, returns existing record instead of failing.
    /// This prevents UNIQUE constraint violations from concurrent file discovery processes.
    /// </summary>
    public async Task<Result<TrackedFile>> RegisterFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<TrackedFile>.Failure("File path cannot be empty");

        if (!File.Exists(filePath))
            return Result<TrackedFile>.Failure($"File not found: {filePath}");

        try
        {
            // Calculate file hash first to check for existing file by hash (more reliable than path)
            var hash = await CalculateFileHashAsync(filePath, cancellationToken);

            // Check if file with same hash already exists (idempotent registration)
            var existingFile = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (existingFile != null)
            {
                _logger.LogDebug(
                    "File with hash {Hash} already registered as {ExistingFileName}. Returning existing record instead of creating duplicate.",
                    hash, existingFile.FileName);

                // Update the OriginalPath if it's different (file might have been copied/moved)
                if (existingFile.OriginalPath != filePath)
                {
                    _logger.LogInformation(
                        "Updating OriginalPath for file {Hash} from {OldPath} to {NewPath}",
                        hash, existingFile.OriginalPath, filePath);
                    existingFile.OriginalPath = filePath;
                    existingFile.FileName = Path.GetFileName(filePath);
                    _trackedFileRepository.Update(existingFile);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return Result<TrackedFile>.Success(existingFile);
            }

            // File is new, create TrackedFile entity
            var fileInfo = new FileInfo(filePath);

            var trackedFile = new TrackedFile
            {
                Hash = hash,
                FileName = Path.GetFileName(filePath),
                OriginalPath = filePath,
                FileSize = fileInfo.Length,
                Status = FileStatus.New
            };

            _trackedFileRepository.Add(trackedFile);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Registered file for tracking: {FileName} (Hash: {Hash})", trackedFile.FileName, hash);
                return Result<TrackedFile>.Success(trackedFile);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                when (dbEx.InnerException?.Message?.Contains("UNIQUE constraint failed: TrackedFiles.Hash") == true)
            {
                // Race condition: Another process registered this hash between our check and insert
                // Retrieve and return the existing record
                _logger.LogWarning(
                    "UNIQUE constraint violation for hash {Hash}. Another process registered this file concurrently. Retrieving existing record.",
                    hash);

                var concurrentlyAddedFile = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
                if (concurrentlyAddedFile != null)
                {
                    return Result<TrackedFile>.Success(concurrentlyAddedFile);
                }

                // Fallback: if we still can't find it, something is very wrong
                _logger.LogError("Failed to retrieve file after UNIQUE constraint violation for hash {Hash}", hash);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register file: {FilePath}", filePath);
            return Result<TrackedFile>.Failure($"Failed to register file: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves a tracked file by its SHA256 hash.
    /// </summary>
    public async Task<Result<TrackedFile>> GetFileByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result<TrackedFile>.Failure("Hash cannot be empty");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            
            if (file == null)
                return Result<TrackedFile>.Failure($"File with hash {hash} not found");

            return Result<TrackedFile>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file by hash: {Hash}", hash);
            return Result<TrackedFile>.Failure($"Failed to retrieve file: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves tracked files filtered by their processing status.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> GetFilesByStatusAsync(FileStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await _trackedFileRepository.GetByStatusAsync(status, cancellationToken);
            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files by status: {Status}", status);
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to retrieve files by status: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets files that are ready for ML classification processing.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> GetFilesReadyForClassificationAsync(int limit = DefaultClassificationLimit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            return Result<IEnumerable<TrackedFile>>.Failure("Limit must be greater than 0");

        try
        {
            var files = await _trackedFileRepository.GetFilesReadyForClassificationAsync(limit, cancellationToken);
            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files ready for classification");
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to retrieve files ready for classification: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets files that have been classified and are awaiting user confirmation.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> GetFilesAwaitingConfirmationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await _trackedFileRepository.GetFilesAwaitingConfirmationAsync(cancellationToken);
            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files awaiting confirmation");
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to retrieve files awaiting confirmation: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets files that are ready to be moved to their target locations.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> GetFilesReadyForMovingAsync(int limit = DefaultMovingLimit, CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await _trackedFileRepository.GetFilesReadyForMovingAsync(limit, cancellationToken);
            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files ready for moving");
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to retrieve files ready for moving: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates a tracked file with ML classification results.
    /// </summary>
    public async Task<Result<TrackedFile>> UpdateClassificationAsync(string hash, string suggestedCategory, decimal confidence, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result<TrackedFile>.Failure("Hash cannot be empty");

        if (string.IsNullOrWhiteSpace(suggestedCategory))
            return Result<TrackedFile>.Failure("Suggested category cannot be empty");

        if (confidence < MinConfidence || confidence > MaxConfidence)
            return Result<TrackedFile>.Failure($"Confidence must be between {MinConfidence} and {MaxConfidence}");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (file == null)
                return Result<TrackedFile>.Failure($"File with hash {hash} not found");

            if (file.Status != FileStatus.New && file.Status !=  FileStatus.Classified)
                return Result<TrackedFile>.Failure($"File is not in New status. Current status: {file.Status}");

            file.SuggestedCategory = suggestedCategory;
            file.Confidence = confidence;
            file.Status = FileStatus.Classified;
            file.ClassifiedAt = DateTime.UtcNow;

            _trackedFileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated classification for file {Hash}: {Category} ({Confidence:P2})", hash, suggestedCategory, confidence);
            return Result<TrackedFile>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update classification for file: {Hash}", hash);
            return Result<TrackedFile>.Failure($"Failed to update classification: {ex.Message}");
        }
    }

    /// <summary>
    /// Confirms a file's category, transitioning it to ReadyToMove status.
    /// </summary>
    public async Task<Result<TrackedFile>> ConfirmCategoryAsync(string hash, string confirmedCategory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result<TrackedFile>.Failure("Hash cannot be empty");

        if (string.IsNullOrWhiteSpace(confirmedCategory))
            return Result<TrackedFile>.Failure("Confirmed category cannot be empty");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (file == null)
                return Result<TrackedFile>.Failure($"File with hash {hash} not found");

            if (file.Status != FileStatus.Classified)
                return Result<TrackedFile>.Failure($"File is not in Classified status. Current status: {file.Status}");

            // Generate target path using PathGenerationService
            var pathResult = await _pathGenerationService.GenerateTargetPathAsync(file, confirmedCategory);
            if (pathResult.IsFailure)
            {
                _logger.LogError("Failed to generate target path for file {Hash}: {Error}", hash, pathResult.Error);
                return Result<TrackedFile>.Failure($"Failed to generate target path: {pathResult.Error}");
            }

            file.Category = confirmedCategory;
            file.Status = FileStatus.ReadyToMove;
            file.TargetPath = pathResult.Value;

            _trackedFileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Confirmed category for file {Hash}: {Category}, Target path: {TargetPath}",
                hash, confirmedCategory, file.TargetPath);
            return Result<TrackedFile>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm category for file: {Hash}", hash);
            return Result<TrackedFile>.Failure($"Failed to confirm category: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks a file as moved to its target location.
    /// </summary>
    public async Task<Result<TrackedFile>> MarkFileAsMovedAsync(string hash, string targetPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result<TrackedFile>.Failure("Hash cannot be empty");

        if (string.IsNullOrWhiteSpace(targetPath))
            return Result<TrackedFile>.Failure("Target path cannot be empty");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (file == null)
                return Result<TrackedFile>.Failure($"File with hash {hash} not found");

            if (file.Status != FileStatus.ReadyToMove)
                return Result<TrackedFile>.Failure($"File is not in Ready to move status. Current status: {file.Status}");

            file.Status = FileStatus.Moved;
            file.MovedAt = DateTime.UtcNow;
            file.TargetPath = targetPath;

            _trackedFileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Marked file as moved {Hash}: {TargetPath}", hash, targetPath);
            return Result<TrackedFile>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark file as moved: {Hash}", hash);
            return Result<TrackedFile>.Failure($"Failed to mark file as moved: {ex.Message}");
        }
    }

    /// <summary>
    /// Records an error for a tracked file and increments the retry count.
    /// </summary>
    public async Task<Result<TrackedFile>> RecordErrorAsync(string hash, string errorMessage, string? exception = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result<TrackedFile>.Failure("Hash cannot be empty");

        if (string.IsNullOrWhiteSpace(errorMessage))
            return Result<TrackedFile>.Failure("Error message cannot be empty");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (file == null)
                return Result<TrackedFile>.Failure($"File with hash {hash} not found");

            file.LastError = errorMessage;
            file.LastErrorAt = DateTime.UtcNow;
            file.RetryCount++;

            // Determine status based on retry count
            if (file.RetryCount >= _configuration.MaxRetryCount)
            {
                file.Status = FileStatus.Error;
                _logger.LogError("File {Hash} exceeded maximum retry count ({MaxRetryCount}): {ErrorMessage}", hash, _configuration.MaxRetryCount, errorMessage);
            }
            else
            {
                file.Status = FileStatus.Retry;
                _logger.LogWarning("File {Hash} error recorded (retry {RetryCount}/{MaxRetryCount}): {ErrorMessage}", hash, file.RetryCount, _configuration.MaxRetryCount, errorMessage);
            }

            _trackedFileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<TrackedFile>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record error for file: {Hash}", hash);
            return Result<TrackedFile>.Failure($"Failed to record error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets a file's error state and retry count, allowing it to be processed again.
    /// </summary>
    public async Task<Result<TrackedFile>> ResetFileErrorAsync(string hash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result<TrackedFile>.Failure("Hash cannot be empty");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (file == null)
                return Result<TrackedFile>.Failure($"File with hash {hash} not found");

            file.LastError = null;
            file.LastErrorAt = null;
            file.RetryCount = 0;

            // Reset to appropriate status based on file state
            if (!string.IsNullOrEmpty(file.Category))
                file.Status = FileStatus.ReadyToMove;
            else if (!string.IsNullOrEmpty(file.SuggestedCategory))
                file.Status = FileStatus.Classified;
            else
                file.Status = FileStatus.New;

            _trackedFileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Reset error state for file {Hash}", hash);
            return Result<TrackedFile>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset file error: {Hash}", hash);
            return Result<TrackedFile>.Failure($"Failed to reset file error: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft deletes a tracked file, marking it as inactive while preserving audit trail.
    /// </summary>
    public async Task<Result> DeleteFileAsync(string hash, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result.Failure("Hash cannot be empty");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (file == null)
                return Result.Failure($"File with hash {hash} not found");

            _trackedFileRepository.SoftDelete(file, reason);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted file {Hash} with reason: {Reason}", hash, reason ?? "No reason provided");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {Hash}", hash);
            return Result.Failure($"Failed to delete file: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a file with the specified path is already being tracked.
    /// </summary>
    public async Task<Result<bool>> IsFileAlreadyTrackedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<bool>.Failure("File path cannot be empty");

        try
        {
            var exists = await _trackedFileRepository.ExistsByOriginalPathAsync(filePath, cancellationToken);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if file is already tracked: {FilePath}", filePath);
            return Result<bool>.Failure($"Failed to check file existence: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for tracked files by filename pattern.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> SearchFilesByNameAsync(string filenamePattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filenamePattern))
            return Result<IEnumerable<TrackedFile>>.Failure("Filename pattern cannot be empty");

        try
        {
            var files = await _trackedFileRepository.SearchByFilenameAsync(filenamePattern, cancellationToken);
            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search files by filename pattern: {Pattern}", filenamePattern);
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to search files: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves files that have exceeded the maximum retry count and need manual intervention.
    /// Uses the default max retry count from configuration.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> GetFilesNeedingInterventionAsync(int? maxRetryCount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveMaxRetryCount = maxRetryCount ?? _configuration.MaxRetryCount;
            var files = await _trackedFileRepository.GetFilesExceedingRetryLimitAsync(effectiveMaxRetryCount, cancellationToken);
            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files needing intervention");
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to retrieve files needing intervention: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves recently moved files for verification and potential rollback scenarios.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> GetRecentlyMovedFilesAsync(int withinHours = DefaultRecentlyMovedHours, CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await _trackedFileRepository.GetRecentlyMovedFilesAsync(withinHours, cancellationToken);
            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recently moved files");
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to retrieve recently moved files: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets paginated list of tracked files with optional filtering.
    /// </summary>
    public async Task<Result<IEnumerable<TrackedFile>>> GetFilesPagedAsync(
        int skip, 
        int take, 
        FileStatus? status = null, 
        string? category = null, 
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
            return Result<IEnumerable<TrackedFile>>.Failure("Skip must be non-negative");

        if (take <= 0 || take > MaxPageSize)
            return Result<IEnumerable<TrackedFile>>.Failure($"Take must be between 1 and {MaxPageSize}");

        try
        {
            var files = await _trackedFileRepository.GetPagedAsync(
                skip, 
                take,
                predicate: f => (status == null || f.Status == status) && 
                              (category == null || f.Category == category),
                orderBy: f => f.CreatedDate,
                cancellationToken: cancellationToken);

            return Result<IEnumerable<TrackedFile>>.Success(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get paged files");
            return Result<IEnumerable<TrackedFile>>.Failure($"Failed to retrieve paged files: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets paginated list of tracked files filtered by multiple statuses.
    /// </summary>
    public async Task<Result<PagedResult<TrackedFile>>> GetFilesPagedByStatusesAsync(
        int skip,
        int take,
        IEnumerable<FileStatus> statuses,
        string? category = null,
        string? searchTerm = null,
        string? orderBy = null,
        bool descending = true,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
            return Result<PagedResult<TrackedFile>>.Failure("Skip must be non-negative");

        if (take <= 0 || take > MaxPageSize)
            return Result<PagedResult<TrackedFile>>.Failure($"Take must be between 1 and {MaxPageSize}");

        if (statuses == null)
            return Result<PagedResult<TrackedFile>>.Failure("Statuses collection cannot be null");

        var statusList = statuses.ToList();
        if (!statusList.Any())
            return Result<PagedResult<TrackedFile>>.Failure("At least one status must be provided");

        try
        {
            // Build predicate with status, category, and search filters
            // Use EF.Functions.Like for case-insensitive search in SQLite
            Expression<Func<TrackedFile, bool>> predicate = f =>
                statusList.Contains(f.Status) &&
                (category == null || f.Category == category) &&
                (searchTerm == null ||
                 EF.Functions.Like(f.FileName, $"%{searchTerm}%") ||
                 (f.Category != null && EF.Functions.Like(f.Category, $"%{searchTerm}%")));

            // Build order expression
            Expression<Func<TrackedFile, object>>? orderExpression = orderBy?.ToLowerInvariant() switch
            {
                "filename" => f => f.FileName,
                "category" => f => f.Category ?? string.Empty,
                "status" => f => f.Status,
                "createddate" => f => f.CreatedDate,
                "lastupdatedate" => f => f.LastUpdateDate,
                _ => f => f.LastUpdateDate // Default: LastUpdateDate DESC (BaseEntity initializes this)
            };

            // Get total count first
            var total = await _trackedFileRepository.CountAsync(predicate, cancellationToken);

            // Get paged data
            var files = await _trackedFileRepository.GetPagedAsync(
                skip,
                take,
                predicate: predicate,
                orderBy: orderExpression,
                descending: descending,
                cancellationToken: cancellationToken);

            var result = new PagedResult<TrackedFile>
            {
                Items = files,
                Total = total
            };

            return Result<PagedResult<TrackedFile>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get paged files by statuses");
            return Result<PagedResult<TrackedFile>>.Failure($"Failed to retrieve paged files by statuses: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks a file as ignored, transitioning it to the Ignored status.
    /// </summary>
    public async Task<Result<TrackedFile>> IgnoreFileAsync(string hash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result<TrackedFile>.Failure("Hash cannot be empty");

        try
        {
            var file = await _trackedFileRepository.GetByHashAsync(hash, cancellationToken);
            if (file == null)
            {
                _logger.LogWarning("Attempted to ignore non-existent file with hash: {Hash}", hash);
                return Result<TrackedFile>.Failure($"File with hash '{hash}' not found");
            }

            // Check if file is already ignored
            if (file.Status == FileStatus.Ignored)
            {
                _logger.LogDebug("File {Hash} is already ignored", hash);
                return Result<TrackedFile>.Success(file);
            }

            // Check if file is already moved - might want to prevent ignoring moved files
            if (file.Status == FileStatus.Moved)
            {
                _logger.LogWarning("Attempted to ignore file {Hash} that has already been moved", hash);
                return Result<TrackedFile>.Failure("Cannot ignore a file that has already been moved");
            }

            _logger.LogInformation("Marking file {Hash} ({FileName}) as ignored. Previous status: {PreviousStatus}",
                hash, file.FileName, file.Status);

            // Update file status to Ignored
            file.Status = FileStatus.Ignored;
            file.LastUpdateDate = DateTime.UtcNow;

            _trackedFileRepository.Update(file);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully marked file {Hash} as ignored", hash);
            return Result<TrackedFile>.Success(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark file {Hash} as ignored", hash);
            return Result<TrackedFile>.Failure($"Failed to ignore file: {ex.Message}");
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Calculates the SHA256 hash of a file.
    /// </summary>
    private static async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var sha256 = SHA256.Create();

        var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gets distinct categories from tracked files.
    /// </summary>
    public async Task<Result<IEnumerable<string>>> GetDistinctCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await _trackedFileRepository.GetDistinctCategoriesAsync(cancellationToken);

            // Filter out null/empty categories and sort alphabetically
            var validCategories = categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .OrderBy(c => c)
                .ToList();

            _logger.LogDebug("Retrieved {Count} distinct categories from tracked files", validCategories.Count);

            return Result<IEnumerable<string>>.Success(validCategories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get distinct categories from tracked files");
            return Result<IEnumerable<string>>.Failure($"Failed to retrieve categories: {ex.Message}");
        }
    }

    #endregion
}