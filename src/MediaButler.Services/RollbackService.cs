using System.Text.Json;
using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Core.Services;
using MediaButler.Data.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services;

/// <summary>
/// Service for managing file operation rollback functionality.
/// Implements simple rollback mechanisms using ProcessingLog for audit trail.
/// Follows "Simple Made Easy" principles with atomic OS operations and clear separation of concerns.
/// </summary>
public class RollbackService : IRollbackService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RollbackService> _logger;
    
    private const string RollbackCategory = "FileOperation.Rollback";

    public RollbackService(
        IUnitOfWork unitOfWork,
        ILogger<RollbackService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateRollbackPointAsync(
        string fileHash,
        string operationType,
        string originalPath,
        string? targetPath = null,
        string? additionalInfo = null)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return Result<Guid>.Failure("File hash cannot be null or empty");
        
        if (string.IsNullOrWhiteSpace(operationType))
            return Result<Guid>.Failure("Operation type cannot be null or empty");
        
        if (string.IsNullOrWhiteSpace(originalPath))
            return Result<Guid>.Failure("Original path cannot be null or empty");

        try
        {
            // Create rollback data structure
            var rollbackData = new
            {
                OperationType = operationType,
                OriginalPath = originalPath,
                TargetPath = targetPath,
                AdditionalInfo = additionalInfo,
                CreatedAt = DateTime.UtcNow,
                CanRollback = true
            };

            // Serialize rollback data for storage
            var rollbackJson = JsonSerializer.Serialize(rollbackData, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            // Create ProcessingLog entry for rollback point
            var rollbackLog = ProcessingLog.Info(
                fileHash,
                RollbackCategory,
                $"Rollback point created for {operationType}",
                rollbackJson);

            // Store the rollback point
            _unitOfWork.ProcessingLogs.Add(rollbackLog);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Created rollback point {RollbackId} for file {FileHash}, operation: {Operation}",
                rollbackLog.Id, fileHash, operationType);

            return Result<Guid>.Success(rollbackLog.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to create rollback point for file {FileHash}, operation: {Operation}",
                fileHash, operationType);
            
            return Result<Guid>.Failure($"Failed to create rollback point: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteRollbackAsync(Guid operationId)
    {
        try
        {
            // Find the rollback point
            var rollbackLog = await _unitOfWork.ProcessingLogs.GetByIdAsync(new object[] { operationId });
            if (rollbackLog == null)
                return Result.Failure($"Rollback point {operationId} not found");

            if (rollbackLog.Category != RollbackCategory)
                return Result.Failure($"Log entry {operationId} is not a rollback point");

            // Parse rollback data
            if (string.IsNullOrEmpty(rollbackLog.Details))
                return Result.Failure("Rollback point contains no rollback data");

            var rollbackData = JsonSerializer.Deserialize<RollbackData>(rollbackLog.Details);
            if (rollbackData == null)
                return Result.Failure("Failed to parse rollback data");

            // Validate rollback is still possible
            var validationResult = await ValidateRollbackIntegrityAsync(operationId);
            if (!validationResult.IsSuccess)
                return Result.Failure($"Rollback validation failed: {validationResult.Error}");

            if (!validationResult.Value.IsValid)
                return Result.Failure($"Rollback is not valid: {string.Join(", ", validationResult.Value.ValidationMessages)}");

            // Execute the rollback based on operation type
            var rollbackResult = rollbackData.OperationType.ToUpper() switch
            {
                "MOVE" => await ExecuteFileMoveRollback(rollbackData),
                "COPY" => await ExecuteFileCopyRollback(rollbackData),
                "RENAME" => await ExecuteFileRenameRollback(rollbackData),
                _ => Result.Failure($"Unsupported rollback operation type: {rollbackData.OperationType}")
            };

            if (!rollbackResult.IsSuccess)
                return rollbackResult;

            // Log successful rollback
            var successLog = ProcessingLog.Info(
                rollbackLog.FileHash,
                RollbackCategory,
                $"Successfully executed rollback for {rollbackData.OperationType}",
                $"Restored file from {rollbackData.TargetPath} to {rollbackData.OriginalPath}");

            _unitOfWork.ProcessingLogs.Add(successLog);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully executed rollback {RollbackId} for file {FileHash}",
                operationId, rollbackLog.FileHash);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute rollback {RollbackId}", operationId);
            return Result.Failure($"Rollback execution failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> RollbackLastOperationAsync(string fileHash)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return Result.Failure("File hash cannot be null or empty");

        try
        {
            // Find the most recent rollback point for this file
            var recentRollbackPoints = await _unitOfWork.ProcessingLogs
                .FindAsync(log => log.FileHash == fileHash);

            var lastRollbackPoint = recentRollbackPoints
                .Where(log => log.Category == RollbackCategory)
                .OrderByDescending(log => log.CreatedDate)
                .FirstOrDefault();

            if (lastRollbackPoint == null)
                return Result.Failure($"No rollback points found for file {fileHash}");

            return await ExecuteRollbackAsync(lastRollbackPoint.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback last operation for file {FileHash}", fileHash);
            return Result.Failure($"Failed to rollback last operation: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<RollbackValidationResult>> ValidateRollbackIntegrityAsync(Guid operationId)
    {
        try
        {
            // Find the rollback point
            var rollbackLog = await _unitOfWork.ProcessingLogs.GetByIdAsync(new object[] { operationId });
            if (rollbackLog == null)
                return Result<RollbackValidationResult>.Failure($"Rollback point {operationId} not found");

            if (rollbackLog.Category != RollbackCategory)
                return Result<RollbackValidationResult>.Failure($"Log entry {operationId} is not a rollback point");

            // Parse rollback data
            if (string.IsNullOrEmpty(rollbackLog.Details))
                return Result<RollbackValidationResult>.Failure("Rollback point contains no rollback data");

            var rollbackData = JsonSerializer.Deserialize<RollbackData>(rollbackLog.Details);
            if (rollbackData == null)
                return Result<RollbackValidationResult>.Failure("Failed to parse rollback data");

            var validationMessages = new List<string>();
            var originalLocationAccessible = false;
            var targetFileExists = false;
            var successProbability = 0.0;

            // Validate original location
            try
            {
                var originalDir = Path.GetDirectoryName(rollbackData.OriginalPath);
                originalLocationAccessible = !string.IsNullOrEmpty(originalDir) && Directory.Exists(originalDir);
                
                if (!originalLocationAccessible)
                    validationMessages.Add($"Original directory does not exist: {originalDir}");
            }
            catch (Exception ex)
            {
                validationMessages.Add($"Cannot access original location: {ex.Message}");
            }

            // Validate target file
            try
            {
                if (!string.IsNullOrEmpty(rollbackData.TargetPath))
                {
                    targetFileExists = File.Exists(rollbackData.TargetPath);
                    if (!targetFileExists)
                        validationMessages.Add($"Target file does not exist: {rollbackData.TargetPath}");
                }
                else
                {
                    validationMessages.Add("No target path specified for rollback");
                }
            }
            catch (Exception ex)
            {
                validationMessages.Add($"Cannot access target file: {ex.Message}");
            }

            // Calculate success probability
            if (originalLocationAccessible && targetFileExists)
                successProbability = 0.95; // High confidence for simple file operations
            else if (originalLocationAccessible)
                successProbability = 0.3;  // Medium confidence if directory exists
            else
                successProbability = 0.1;  // Low confidence

            var isValid = validationMessages.Count == 0 && originalLocationAccessible && targetFileExists;

            var result = new RollbackValidationResult
            {
                IsValid = isValid,
                ValidationMessages = validationMessages.AsReadOnly(),
                OriginalLocationAccessible = originalLocationAccessible,
                TargetFileExists = targetFileExists,
                SuccessProbability = successProbability
            };

            return Result<RollbackValidationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate rollback integrity for {OperationId}", operationId);
            return Result<RollbackValidationResult>.Failure($"Validation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> CleanupRollbackHistoryAsync(DateTime olderThan)
    {
        try
        {
            var allLogs = await _unitOfWork.ProcessingLogs.GetAllAsync();
            var rollbackLogsToDelete = allLogs
                .Where(log => log.Category == RollbackCategory && log.CreatedDate < olderThan)
                .ToList();

            var deleteCount = rollbackLogsToDelete.Count;
            
            foreach (var log in rollbackLogsToDelete)
            {
                log.SoftDelete();
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} rollback points older than {Date}", 
                deleteCount, olderThan);

            return Result<int>.Success(deleteCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup rollback history older than {Date}", olderThan);
            return Result<int>.Failure($"Cleanup failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RollbackPoint>>> GetRollbackHistoryAsync(string fileHash)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return Result<IReadOnlyList<RollbackPoint>>.Failure("File hash cannot be null or empty");

        try
        {
            var logs = await _unitOfWork.ProcessingLogs.FindAsync(log => log.FileHash == fileHash);
            var rollbackLogs = logs
                .Where(log => log.Category == RollbackCategory)
                .OrderByDescending(log => log.CreatedDate)
                .ToList();

            var rollbackPoints = new List<RollbackPoint>();

            foreach (var log in rollbackLogs)
            {
                if (string.IsNullOrEmpty(log.Details))
                    continue;

                try
                {
                    var rollbackData = JsonSerializer.Deserialize<RollbackData>(log.Details);
                    if (rollbackData == null)
                        continue;

                    var rollbackPoint = new RollbackPoint
                    {
                        Id = log.Id,
                        FileHash = log.FileHash,
                        OperationType = rollbackData.OperationType,
                        OriginalPath = rollbackData.OriginalPath,
                        TargetPath = rollbackData.TargetPath,
                        AdditionalInfo = rollbackData.AdditionalInfo,
                        CreatedAt = log.CreatedDate,
                        CanRollback = rollbackData.CanRollback
                    };

                    rollbackPoints.Add(rollbackPoint);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse rollback data for log {LogId}", log.Id);
                    continue;
                }
            }

            return Result<IReadOnlyList<RollbackPoint>>.Success(rollbackPoints.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rollback history for file {FileHash}", fileHash);
            return Result<IReadOnlyList<RollbackPoint>>.Failure($"Failed to get rollback history: {ex.Message}");
        }
    }

    private async Task<Result> ExecuteFileMoveRollback(RollbackData rollbackData)
    {
        try
        {
            if (string.IsNullOrEmpty(rollbackData.TargetPath) || string.IsNullOrEmpty(rollbackData.OriginalPath))
                return Result.Failure("Invalid rollback data: missing paths");

            if (!File.Exists(rollbackData.TargetPath))
                return Result.Failure($"Target file does not exist: {rollbackData.TargetPath}");

            // Ensure original directory exists
            var originalDir = Path.GetDirectoryName(rollbackData.OriginalPath);
            if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
            {
                Directory.CreateDirectory(originalDir);
            }

            // Atomic move operation
            File.Move(rollbackData.TargetPath, rollbackData.OriginalPath);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"File move rollback failed: {ex.Message}");
        }
    }

    private async Task<Result> ExecuteFileCopyRollback(RollbackData rollbackData)
    {
        try
        {
            if (string.IsNullOrEmpty(rollbackData.TargetPath))
                return Result.Failure("Invalid rollback data: missing target path");

            if (!File.Exists(rollbackData.TargetPath))
                return Result.Failure($"Target file does not exist: {rollbackData.TargetPath}");

            // For copy operations, rollback means deleting the copied file
            File.Delete(rollbackData.TargetPath);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"File copy rollback failed: {ex.Message}");
        }
    }

    private async Task<Result> ExecuteFileRenameRollback(RollbackData rollbackData)
    {
        try
        {
            if (string.IsNullOrEmpty(rollbackData.TargetPath) || string.IsNullOrEmpty(rollbackData.OriginalPath))
                return Result.Failure("Invalid rollback data: missing paths");

            if (!File.Exists(rollbackData.TargetPath))
                return Result.Failure($"Target file does not exist: {rollbackData.TargetPath}");

            // Atomic rename operation (back to original name)
            File.Move(rollbackData.TargetPath, rollbackData.OriginalPath);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"File rename rollback failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal data structure for rollback information stored in ProcessingLog.Details
    /// </summary>
    private record RollbackData
    {
        public string OperationType { get; init; } = string.Empty;
        public string OriginalPath { get; init; } = string.Empty;
        public string? TargetPath { get; init; }
        public string? AdditionalInfo { get; init; }
        public DateTime CreatedAt { get; init; }
        public bool CanRollback { get; init; }
    }
}