using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Domain;

/// <summary>
/// Service responsible for handling concurrent access scenarios in file processing operations.
/// Implements optimistic concurrency control and conflict resolution following "Simple Made Easy" principles.
/// </summary>
public class ConcurrencyHandler
{
    private readonly IProcessingLogRepository _processingLogRepository;
    private readonly ILogger<ConcurrencyHandler> _logger;

    public ConcurrencyHandler(
        IProcessingLogRepository processingLogRepository,
        ILogger<ConcurrencyHandler> logger)
    {
        _processingLogRepository = processingLogRepository ?? throw new ArgumentNullException(nameof(processingLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles concurrent updates to TrackedFile entities with conflict resolution.
    /// Uses optimistic concurrency control based on LastUpdateDate comparison.
    /// </summary>
    /// <param name="currentFile">The current version of the file from the database</param>
    /// <param name="updatedFile">The version with updates to apply</param>
    /// <param name="operation">Description of the operation being performed</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating success or failure with conflict resolution</returns>
    public async Task<Result<TrackedFile>> HandleConcurrentUpdateAsync(
        TrackedFile currentFile, 
        TrackedFile updatedFile, 
        string operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (currentFile == null)
                return Result<TrackedFile>.Failure("Current file cannot be null");

            if (updatedFile == null)
                return Result<TrackedFile>.Failure("Updated file cannot be null");

            if (currentFile.Hash != updatedFile.Hash)
                return Result<TrackedFile>.Failure("File hash mismatch - cannot update different files");

            // Check for concurrent modifications by comparing LastUpdateDate
            if (currentFile.LastUpdateDate != updatedFile.LastUpdateDate)
            {
                _logger.LogWarning(
                    "Concurrent modification detected for file {FileHash} during {Operation}. " +
                    "Current: {CurrentDate}, Updated: {UpdatedDate}",
                    currentFile.Hash, operation, currentFile.LastUpdateDate, updatedFile.LastUpdateDate);

                // Attempt conflict resolution
                var resolvedFile = await ResolveConflictAsync(currentFile, updatedFile, operation, cancellationToken);
                if (resolvedFile.IsSuccess)
                {
                    return resolvedFile;
                }

                // If conflict resolution fails, return failure
                return Result<TrackedFile>.Failure(
                    $"Concurrent modification conflict could not be resolved: {resolvedFile.Error}");
            }

            // No conflict detected, proceed with update
            _logger.LogDebug("No concurrent modification detected for file {FileHash} during {Operation}", 
                currentFile.Hash, operation);

            return Result<TrackedFile>.Success(updatedFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling concurrent update for file {FileHash} during {Operation}", 
                currentFile?.Hash ?? "unknown", operation);
            return Result<TrackedFile>.Failure($"Concurrency handling failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves conflicts between concurrent modifications using business rules.
    /// Implements "last writer wins" with conflict logging for audit purposes.
    /// </summary>
    private async Task<Result<TrackedFile>> ResolveConflictAsync(
        TrackedFile currentFile,
        TrackedFile updatedFile,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create conflict resolution log entry
            var conflictLog = ProcessingLog.Warning(
                currentFile.Hash,
                "Concurrency.Conflict",
                $"Concurrent modification conflict resolved using 'last writer wins' strategy",
                $"Operation: {operation}, " +
                $"Current LastUpdate: {currentFile.LastUpdateDate:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"Conflicting LastUpdate: {updatedFile.LastUpdateDate:yyyy-MM-dd HH:mm:ss.fff}, " +
                $"Current Status: {currentFile.Status}, " +
                $"Conflicting Status: {updatedFile.Status}"
            );

            await _processingLogRepository.AddAsync(conflictLog, cancellationToken);

            // Apply conflict resolution strategy: "Last writer wins"
            // Preserve the most recent changes and update timestamp
            var resolvedFile = ResolveFileState(currentFile, updatedFile);
            resolvedFile.MarkAsModified(); // Update LastUpdateDate to reflect resolution

            _logger.LogInformation(
                "Conflict resolved for file {FileHash} using 'last writer wins' strategy. " +
                "Final status: {Status}", currentFile.Hash, resolvedFile.Status);

            return Result<TrackedFile>.Success(resolvedFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve conflict for file {FileHash}", currentFile.Hash);
            return Result<TrackedFile>.Failure($"Conflict resolution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies business rules to resolve conflicting file states.
    /// Uses priority-based resolution to maintain data consistency.
    /// </summary>
    private TrackedFile ResolveFileState(TrackedFile currentFile, TrackedFile updatedFile)
    {
        // Priority order for status conflicts (higher priority wins):
        // 1. Error/Retry states (preserve error information)
        // 2. Moved (final state, should not be overridden)
        // 3. ReadyToMove (user confirmation is important)
        // 4. Classified (ML results are valuable)
        // 5. Processing (temporary state)
        // 6. New (initial state)

        var statusPriority = new Dictionary<FileStatus, int>
        {
            { FileStatus.Error, 6 },
            { FileStatus.Retry, 5 },
            { FileStatus.Moved, 4 },
            { FileStatus.ReadyToMove, 3 },
            { FileStatus.Classified, 2 },
            { FileStatus.Processing, 1 },
            { FileStatus.New, 0 }
        };

        var currentPriority = statusPriority.GetValueOrDefault(currentFile.Status, 0);
        var updatedPriority = statusPriority.GetValueOrDefault(updatedFile.Status, 0);

        // Use the file with higher status priority as the base
        var baseFile = currentPriority >= updatedPriority ? currentFile : updatedFile;
        var otherFile = currentPriority >= updatedPriority ? updatedFile : currentFile;

        // Merge properties based on business rules:
        // - Preserve ML classification results if available
        // - Keep user confirmations and target paths
        // - Maintain error information
        // - Use the most recent timestamps

        if (string.IsNullOrEmpty(baseFile.SuggestedCategory) && !string.IsNullOrEmpty(otherFile.SuggestedCategory))
        {
            baseFile.SuggestedCategory = otherFile.SuggestedCategory;
            baseFile.Confidence = otherFile.Confidence;
            baseFile.ClassifiedAt = otherFile.ClassifiedAt;
        }

        if (string.IsNullOrEmpty(baseFile.Category) && !string.IsNullOrEmpty(otherFile.Category))
        {
            baseFile.Category = otherFile.Category;
        }

        if (string.IsNullOrEmpty(baseFile.TargetPath) && !string.IsNullOrEmpty(otherFile.TargetPath))
        {
            baseFile.TargetPath = otherFile.TargetPath;
        }

        if (string.IsNullOrEmpty(baseFile.MovedToPath) && !string.IsNullOrEmpty(otherFile.MovedToPath))
        {
            baseFile.MovedToPath = otherFile.MovedToPath;
            baseFile.MovedAt = otherFile.MovedAt;
        }

        // Use the most recent error information
        if (otherFile.LastErrorAt > baseFile.LastErrorAt)
        {
            baseFile.LastError = otherFile.LastError;
            baseFile.LastErrorAt = otherFile.LastErrorAt;
            baseFile.RetryCount = Math.Max(baseFile.RetryCount, otherFile.RetryCount);
        }

        return baseFile;
    }
}