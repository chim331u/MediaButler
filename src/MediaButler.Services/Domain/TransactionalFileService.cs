using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Domain;

/// <summary>
/// Service that wraps file operations with transaction management and concurrency control.
/// Provides thread-safe file operations with automatic retry and conflict resolution.
/// </summary>
public class TransactionalFileService
{
    private readonly ITrackedFileRepository _trackedFileRepository;
    private readonly IProcessingLogRepository _processingLogRepository;
    private readonly ConcurrencyHandler _concurrencyHandler;
    private readonly ILogger<TransactionalFileService> _logger;

    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    public TransactionalFileService(
        ITrackedFileRepository trackedFileRepository,
        IProcessingLogRepository processingLogRepository,
        ConcurrencyHandler concurrencyHandler,
        ILogger<TransactionalFileService> logger)
    {
        _trackedFileRepository = trackedFileRepository ?? throw new ArgumentNullException(nameof(trackedFileRepository));
        _processingLogRepository = processingLogRepository ?? throw new ArgumentNullException(nameof(processingLogRepository));
        _concurrencyHandler = concurrencyHandler ?? throw new ArgumentNullException(nameof(concurrencyHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Updates a TrackedFile with automatic concurrency control and retry logic.
    /// Handles DbUpdateConcurrencyException and resolves conflicts automatically.
    /// </summary>
    /// <param name="fileHash">The hash of the file to update</param>
    /// <param name="updateAction">Action to perform on the file</param>
    /// <param name="operation">Description of the operation for logging</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result<TrackedFile>> UpdateFileWithConcurrencyControlAsync(
        string fileHash,
        Action<TrackedFile> updateAction,
        string operation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return Result<TrackedFile>.Failure("File hash cannot be null or empty");

        if (updateAction == null)
            return Result<TrackedFile>.Failure("Update action cannot be null");

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                // Get the current file state
                var currentFile = await _trackedFileRepository.GetByHashAsync(fileHash, cancellationToken);
                if (currentFile == null)
                {
                    return Result<TrackedFile>.Failure($"File with hash {fileHash} not found");
                }

                // Create a copy for updates to avoid modifying the tracked entity directly
                var updatedFile = CloneFile(currentFile);
                
                // Apply the update action
                updateAction(updatedFile);

                // Handle concurrent access
                var concurrencyResult = await _concurrencyHandler.HandleConcurrentUpdateAsync(
                    currentFile, updatedFile, operation, cancellationToken);

                if (!concurrencyResult.IsSuccess)
                {
                    if (attempt < MaxRetryAttempts)
                    {
                        _logger.LogWarning("Concurrency conflict on attempt {Attempt}/{MaxAttempts} for file {FileHash}: {Error}. Retrying...", 
                            attempt, MaxRetryAttempts, fileHash, concurrencyResult.Error);
                        
                        await Task.Delay(RetryDelay * attempt, cancellationToken);
                        continue;
                    }

                    return Result<TrackedFile>.Failure(concurrencyResult.Error);
                }

                var resolvedFile = concurrencyResult.Value;

                // Update the file in repository
                _trackedFileRepository.Update(resolvedFile);
                await _trackedFileRepository.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Successfully updated file {FileHash} after {Attempts} attempt(s) for operation: {Operation}", 
                    fileHash, attempt, operation);

                return Result<TrackedFile>.Success(resolvedFile);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Database concurrency exception on attempt {Attempt}/{MaxAttempts} for file {FileHash}", 
                    attempt, MaxRetryAttempts, fileHash);

                if (attempt < MaxRetryAttempts)
                {
                    await Task.Delay(RetryDelay * attempt, cancellationToken);
                    continue;
                }

                return Result<TrackedFile>.Failure($"Database concurrency conflict after {MaxRetryAttempts} attempts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file {FileHash} on attempt {Attempt}/{MaxAttempts}", 
                    fileHash, attempt, MaxRetryAttempts);

                if (attempt < MaxRetryAttempts)
                {
                    await Task.Delay(RetryDelay * attempt, cancellationToken);
                    continue;
                }

                return Result<TrackedFile>.Failure($"Update failed after {MaxRetryAttempts} attempts: {ex.Message}");
            }
        }

        return Result<TrackedFile>.Failure($"Update failed after {MaxRetryAttempts} attempts");
    }

    /// <summary>
    /// Performs a bulk update operation with transaction isolation.
    /// Processes files in batches to handle memory constraints and provide isolation.
    /// </summary>
    /// <param name="fileHashes">Collection of file hashes to update</param>
    /// <param name="updateAction">Action to perform on each file</param>
    /// <param name="operation">Description of the bulk operation</param>
    /// <param name="batchSize">Number of files to process per batch</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result with summary of the bulk operation</returns>
    public async Task<Result<BulkUpdateResult>> BulkUpdateWithConcurrencyControlAsync(
        IEnumerable<string> fileHashes,
        Action<TrackedFile> updateAction,
        string operation,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (fileHashes == null)
            return Result<BulkUpdateResult>.Failure("File hashes cannot be null");

        var hashesArray = fileHashes.ToArray();
        if (hashesArray.Length == 0)
        {
            return Result<BulkUpdateResult>.Success(new BulkUpdateResult
            {
                TotalFiles = 0,
                SuccessfulUpdates = 0,
                FailedUpdates = 0,
                Errors = new List<string>()
            });
        }

        var result = new BulkUpdateResult
        {
            TotalFiles = hashesArray.Length,
            Errors = new List<string>()
        };

        _logger.LogInformation("Starting bulk update of {FileCount} files for operation: {Operation}", 
            hashesArray.Length, operation);

        // Process files in batches
        for (int i = 0; i < hashesArray.Length; i += batchSize)
        {
            var batch = hashesArray.Skip(i).Take(batchSize);
            
            foreach (var fileHash in batch)
            {
                var updateResult = await UpdateFileWithConcurrencyControlAsync(
                    fileHash, updateAction, operation, cancellationToken);

                if (updateResult.IsSuccess)
                {
                    result.SuccessfulUpdates++;
                }
                else
                {
                    result.FailedUpdates++;
                    result.Errors.Add($"File {fileHash}: {updateResult.Error}");
                    
                    _logger.LogWarning("Failed to update file {FileHash} in bulk operation: {Error}", 
                        fileHash, updateResult.Error);
                }

                // Check for cancellation between files
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Small delay between batches to prevent overwhelming the system
            if (i + batchSize < hashesArray.Length)
            {
                await Task.Delay(10, cancellationToken);
            }
        }

        _logger.LogInformation("Completed bulk update: {Successful}/{Total} files updated successfully", 
            result.SuccessfulUpdates, result.TotalFiles);

        return Result<BulkUpdateResult>.Success(result);
    }

    /// <summary>
    /// Creates a shallow copy of a TrackedFile for update operations.
    /// Preserves domain events and audit properties during cloning.
    /// </summary>
    private static TrackedFile CloneFile(TrackedFile original)
    {
        return new TrackedFile
        {
            Hash = original.Hash,
            FileName = original.FileName,
            OriginalPath = original.OriginalPath,
            FileSize = original.FileSize,
            Status = original.Status,
            SuggestedCategory = original.SuggestedCategory,
            Confidence = original.Confidence,
            Category = original.Category,
            TargetPath = original.TargetPath,
            MovedToPath = original.MovedToPath,
            ClassifiedAt = original.ClassifiedAt,
            MovedAt = original.MovedAt,
            LastError = original.LastError,
            LastErrorAt = original.LastErrorAt,
            RetryCount = original.RetryCount,
            // BaseEntity properties
            CreatedDate = original.CreatedDate,
            LastUpdateDate = original.LastUpdateDate,
            Note = original.Note,
            IsActive = original.IsActive
        };
    }
}

/// <summary>
/// Result summary for bulk update operations.
/// </summary>
public record BulkUpdateResult
{
    public int TotalFiles { get; set; }
    public int SuccessfulUpdates { get; set; }
    public int FailedUpdates { get; set; }
    public List<string> Errors { get; set; } = new();
}