using Microsoft.Extensions.Logging;
using MediaButler.Core.Common;
using MediaButler.Core.Services;
using MediaButler.Core.Models;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services.FileOperations;
using MediaButler.Services.Interfaces;
using System.Diagnostics;
using System.Text.Json;

namespace MediaButler.Services;

/// <summary>
/// Orchestrates complete file organization workflows by coordinating multiple specialized services.
/// Provides a simple, unified interface for complex file organization operations following "Simple Made Easy" principles.
/// </summary>
/// <remarks>
/// The FileOrganizationService serves as the main coordinator, orchestrating:
/// - Path generation and validation
/// - File operation execution
/// - Error classification and recovery
/// - Rollback operations when needed
/// - Progress tracking and audit logging
/// 
/// This service maintains simplicity by composing existing services rather than reimplementing their functionality.
/// </remarks>
public class FileOrganizationService : IFileOrganizationService
{
    private readonly ILogger<FileOrganizationService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPathGenerationService _pathGenerationService;
    private readonly IFileOperationService _fileOperationService;
    private readonly IErrorClassificationService _errorClassificationService;
    private readonly IRollbackService _rollbackService;

    private static readonly Dictionary<string, OrganizationState> _organizationStates = new();
    private static readonly object _stateLock = new();

    public FileOrganizationService(
        ILogger<FileOrganizationService> logger,
        IUnitOfWork unitOfWork,
        IPathGenerationService pathGenerationService,
        IFileOperationService fileOperationService,
        IErrorClassificationService errorClassificationService,
        IRollbackService rollbackService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _pathGenerationService = pathGenerationService;
        _fileOperationService = fileOperationService;
        _errorClassificationService = errorClassificationService;
        _rollbackService = rollbackService;
    }

    public async Task<Result<FileOrganizationResult>> OrganizeFileAsync(string fileHash, string confirmedCategory)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting file organization for {FileHash} with category {Category}", 
                fileHash, confirmedCategory);

            // Set state to in-progress
            SetOrganizationState(fileHash, OrganizationState.InProgress);

            // 1. Get and validate tracked file
            var trackedFile = await _unitOfWork.TrackedFiles.GetByHashAsync(fileHash);
            if (trackedFile == null)
            {
                SetOrganizationState(fileHash, OrganizationState.Failed);
                return Result<FileOrganizationResult>.Failure($"File with hash {fileHash} not found");
            }

            // 2. Generate target path
            var pathResult = await _pathGenerationService.GenerateTargetPathAsync(trackedFile, confirmedCategory);
            
            if (pathResult.IsFailure)
            {
                SetOrganizationState(fileHash, OrganizationState.Failed);
                return Result<FileOrganizationResult>.Failure($"Path generation failed: {pathResult.Error}");
            }

            var targetPath = pathResult.Value;

            // 3. Validate organization safety
            var validationResult = await ValidateOrganizationSafetyAsync(fileHash, targetPath);
            if (validationResult.IsFailure || !validationResult.Value.IsSafe)
            {
                SetOrganizationState(fileHash, OrganizationState.Failed);
                var issues = validationResult.IsSuccess ? 
                    string.Join(", ", validationResult.Value.SafetyIssues) : 
                    validationResult.Error;
                return Result<FileOrganizationResult>.Failure($"Organization validation failed: {issues}");
            }

            // 4. Create rollback point before operation
            var rollbackResult = await _rollbackService.CreateRollbackPointAsync(
                fileHash, "ORGANIZE", trackedFile.OriginalPath, targetPath, 
                $"Category: {confirmedCategory}");

            Guid? rollbackId = null;
            if (rollbackResult.IsSuccess)
            {
                rollbackId = rollbackResult.Value;
                _logger.LogDebug("Created rollback point {RollbackId} for organization", rollbackId);
            }
            else
            {
                _logger.LogWarning("Failed to create rollback point: {Error}", rollbackResult.Error);
            }

            // 5. Execute file operation
            var moveResult = await _fileOperationService.MoveFileAsync(
                trackedFile.OriginalPath, targetPath);

            if (moveResult.IsFailure)
            {
                SetOrganizationState(fileHash, OrganizationState.Failed);
                
                // Handle error with classification and potential rollback
                var errorResult = await HandleOrganizationErrorAsync(fileHash, 
                    new InvalidOperationException(moveResult.Error));
                
                return Result<FileOrganizationResult>.Failure(
                    $"File move operation failed: {moveResult.Error}");
            }

            // 6. Update tracked file record
            trackedFile.Category = confirmedCategory;
            trackedFile.TargetPath = targetPath;
            trackedFile.MovedToPath = moveResult.Value.TargetPath;
            trackedFile.Status = FileStatus.Moved;
            trackedFile.MarkAsModified();

            _unitOfWork.TrackedFiles.Update(trackedFile);

            // 7. Record successful operation
            _unitOfWork.ProcessingLogs.Add(ProcessingLog.Info(
                fileHash, 
                "FILE_ORGANIZATION",
                $"File organized successfully to category {confirmedCategory}",
                JsonSerializer.Serialize(new {
                    Category = confirmedCategory,
                    OriginalPath = trackedFile.OriginalPath,
                    TargetPath = targetPath,
                    ActualPath = moveResult.Value.TargetPath,
                    RollbackId = rollbackId,
                    FileSizeBytes = moveResult.Value.FileSizeBytes
                }),
                stopwatch.ElapsedMilliseconds));

            await _unitOfWork.SaveChangesAsync();

            SetOrganizationState(fileHash, OrganizationState.Completed);

            stopwatch.Stop();

            var result = new FileOrganizationResult
            {
                IsSuccess = true,
                TargetPath = targetPath,
                ActualPath = moveResult.Value.TargetPath,
                RelatedFilesMoved = Array.Empty<string>(), // Would need to be implemented in FileOperationService
                DurationMs = stopwatch.ElapsedMilliseconds,
                Warnings = Array.Empty<string>(), // Would need to be implemented in FileOperationService
                RollbackOperationId = rollbackId
            };

            _logger.LogInformation("File organization completed successfully for {FileHash} in {Duration}ms", 
                fileHash, stopwatch.ElapsedMilliseconds);

            return Result<FileOrganizationResult>.Success(result);
        }
        catch (Exception ex)
        {
            SetOrganizationState(fileHash, OrganizationState.Failed);
            stopwatch.Stop();

            _logger.LogError(ex, "Unexpected error during file organization for {FileHash}", fileHash);

            // Record the error
            await RecordOrganizationError(fileHash, ex, stopwatch.ElapsedMilliseconds);

            return Result<FileOrganizationResult>.Failure($"Organization failed with unexpected error: {ex.Message}");
        }
    }

    public async Task<Result<FileOrganizationPreview>> PreviewOrganizationAsync(string fileHash, string proposedCategory)
    {
        try
        {
            _logger.LogDebug("Generating organization preview for {FileHash} with category {Category}",
                fileHash, proposedCategory);

            // 1. Get tracked file
            var trackedFile = await _unitOfWork.TrackedFiles.GetByHashAsync(fileHash);
            if (trackedFile == null)
            {
                return Result<FileOrganizationPreview>.Failure($"File with hash {fileHash} not found");
            }

            // 2. Generate proposed path
            var pathResult = await _pathGenerationService.GenerateTargetPathAsync(trackedFile, proposedCategory);
                
            if (pathResult.IsFailure)
            {
                return Result<FileOrganizationPreview>.Failure($"Path generation failed: {pathResult.Error}");
            }

            var proposedPath = pathResult.Value;

            // 3. Perform safety validation
            var validationResult = await ValidateOrganizationSafetyAsync(fileHash, proposedPath);
            var isSafe = validationResult.IsSuccess && validationResult.Value.IsSafe;
            
            // 4. Get related files that would be moved
            var relatedFiles = await DiscoverRelatedFiles(trackedFile.OriginalPath);

            // 5. Calculate space requirements
            var totalSize = trackedFile.FileSize;
            foreach (var relatedFile in relatedFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(relatedFile);
                    if (fileInfo.Exists)
                        totalSize += fileInfo.Length;
                }
                catch
                {
                    // Skip files we can't access
                }
            }

            // 6. Get available space
            var targetDirectory = Path.GetDirectoryName(proposedPath) ?? string.Empty;
            var availableSpace = GetAvailableSpace(targetDirectory);

            // 7. Estimate duration (simple heuristic)
            var estimatedDurationMs = EstimateOrganizationDuration(totalSize, relatedFiles.Count);

            // 8. Collect potential issues and recommendations
            var potentialIssues = new List<string>();
            var recommendations = new List<string>();

            if (!isSafe && validationResult.IsSuccess)
            {
                potentialIssues.AddRange(validationResult.Value.SafetyIssues);
                recommendations.AddRange(validationResult.Value.RecommendedActions);
            }

            if (totalSize > availableSpace)
            {
                potentialIssues.Add($"Insufficient disk space: {totalSize:N0} bytes required, {availableSpace:N0} bytes available");
                recommendations.Add("Free up disk space before proceeding with organization");
            }

            if (File.Exists(proposedPath))
            {
                potentialIssues.Add($"Target file already exists: {proposedPath}");
                recommendations.Add("The system will automatically resolve filename conflicts");
            }

            var preview = new FileOrganizationPreview
            {
                ProposedPath = proposedPath,
                IsSafe = isSafe,
                RelatedFiles = relatedFiles,
                EstimatedDurationMs = estimatedDurationMs,
                RequiredSpaceBytes = totalSize,
                AvailableSpaceBytes = availableSpace,
                PotentialIssues = potentialIssues,
                Recommendations = recommendations
            };

            return Result<FileOrganizationPreview>.Success(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate organization preview for {FileHash}", fileHash);
            return Result<FileOrganizationPreview>.Failure($"Preview generation failed: {ex.Message}");
        }
    }

    public async Task<Result<OrganizationValidationResult>> ValidateOrganizationSafetyAsync(string fileHash, string targetPath)
    {
        try
        {
            var safetyIssues = new List<string>();
            var warnings = new List<string>();
            var validationDetails = new Dictionary<string, object>();
            var recommendations = new List<string>();

            // 1. Get tracked file for context
            var trackedFile = await _unitOfWork.TrackedFiles.GetByHashAsync(fileHash);
            if (trackedFile == null)
            {
                return Result<OrganizationValidationResult>.Failure($"File with hash {fileHash} not found");
            }

            // 2. Validate source file exists and is accessible
            if (!File.Exists(trackedFile.OriginalPath))
            {
                safetyIssues.Add($"Source file not found: {trackedFile.OriginalPath}");
            }
            else
            {
                try
                {
                    // Test file access
                    using var stream = File.OpenRead(trackedFile.OriginalPath);
                    validationDetails["SourceFileAccessible"] = true;
                }
                catch (Exception ex)
                {
                    safetyIssues.Add($"Cannot access source file: {ex.Message}");
                    validationDetails["SourceFileAccessible"] = false;
                }
            }

            // 3. Validate target directory
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(targetDirectory))
            {
                safetyIssues.Add("Invalid target path - no directory specified");
            }
            else
            {
                // Check if target directory exists or can be created
                try
                {
                    if (!Directory.Exists(targetDirectory))
                    {
                        warnings.Add($"Target directory will be created: {targetDirectory}");
                        validationDetails["TargetDirectoryExists"] = false;
                    }
                    else
                    {
                        validationDetails["TargetDirectoryExists"] = true;
                    }

                    // Test write permissions
                    var testFile = Path.Combine(targetDirectory, $"test_{Guid.NewGuid()}.tmp");
                    await File.WriteAllTextAsync(testFile, "test");
                    File.Delete(testFile);
                    validationDetails["TargetDirectoryWritable"] = true;
                }
                catch (Exception ex)
                {
                    safetyIssues.Add($"Cannot write to target directory: {ex.Message}");
                    recommendations.Add("Check directory permissions and ensure the path is writable");
                    validationDetails["TargetDirectoryWritable"] = false;
                }
            }

            // 4. Check disk space
            var availableSpace = GetAvailableSpace(targetDirectory ?? "");
            var requiredSpace = trackedFile.FileSize;

            validationDetails["AvailableSpaceBytes"] = availableSpace;
            validationDetails["RequiredSpaceBytes"] = requiredSpace;

            if (availableSpace < requiredSpace * 1.1) // 10% buffer
            {
                safetyIssues.Add($"Insufficient disk space: {requiredSpace:N0} bytes required, {availableSpace:N0} bytes available");
                recommendations.Add("Free up disk space before attempting organization");
            }

            // 5. Check for conflicts
            if (File.Exists(targetPath))
            {
                warnings.Add($"Target file already exists: {targetPath}");
                recommendations.Add("File will be renamed automatically to avoid conflicts");
                validationDetails["ConflictExists"] = true;
            }
            else
            {
                validationDetails["ConflictExists"] = false;
            }

            // 6. Path length validation
            if (targetPath.Length > 260) // Windows path length limit
            {
                safetyIssues.Add($"Target path too long: {targetPath.Length} characters (limit: 260)");
                recommendations.Add("Choose a shorter category name or enable long path support");
            }

            validationDetails["TargetPathLength"] = targetPath.Length;

            var isSafe = safetyIssues.Count == 0;

            var result = new OrganizationValidationResult
            {
                IsSafe = isSafe,
                SafetyIssues = safetyIssues,
                Warnings = warnings,
                ValidationDetails = validationDetails,
                RecommendedActions = recommendations
            };

            return Result<OrganizationValidationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate organization safety for {FileHash}", fileHash);
            return Result<OrganizationValidationResult>.Failure($"Safety validation failed: {ex.Message}");
        }
    }

    public async Task<Result<OrganizationRecoveryResult>> HandleOrganizationErrorAsync(
        string fileHash, 
        Exception organizationError, 
        int attemptNumber = 1)
    {
        try
        {
            _logger.LogWarning(organizationError, "Handling organization error for {FileHash}, attempt {Attempt}", 
                fileHash, attemptNumber);

            // 1. Get tracked file for context
            var trackedFile = await _unitOfWork.TrackedFiles.GetByHashAsync(fileHash);
            if (trackedFile == null)
            {
                return Result<OrganizationRecoveryResult>.Failure($"File with hash {fileHash} not found");
            }

            // 2. Create error context for classification
            var errorContext = new ErrorContext
            {
                Exception = organizationError,
                OperationType = "ORGANIZE_FILE",
                SourcePath = trackedFile.OriginalPath,
                TargetPath = trackedFile.TargetPath,
                FileSize = trackedFile.FileSize,
                FileHash = fileHash,
                RetryAttempts = attemptNumber - 1,
                AdditionalContext = new Dictionary<string, object>
                {
                    ["Category"] = trackedFile.Category ?? "Unknown",
                    ["Status"] = trackedFile.Status.ToString(),
                    ["AttemptNumber"] = attemptNumber
                }
            };

            // 3. Classify the error
            var classificationResult = await _errorClassificationService.ClassifyErrorAsync(errorContext);
            if (classificationResult.IsFailure)
            {
                _logger.LogError("Failed to classify organization error: {Error}", classificationResult.Error);
                // Fallback to generic recovery
                return CreateGenericRecoveryResult(attemptNumber);
            }

            var classification = classificationResult.Value;

            // 4. Determine recovery action based on classification
            var recoveryResult = await _errorClassificationService.DetermineRecoveryActionAsync(
                errorContext, classification);
                
            if (recoveryResult.IsFailure)
            {
                _logger.LogError("Failed to determine recovery action: {Error}", recoveryResult.Error);
                return CreateGenericRecoveryResult(attemptNumber);
            }

            var recovery = recoveryResult.Value;

            // 5. Create rollback if needed and possible
            Guid? rollbackId = null;
            if (recovery.ActionType == ErrorRecoveryType.AutomaticRetry && trackedFile.TargetPath != null)
            {
                var rollbackResult = await _rollbackService.CreateRollbackPointAsync(
                    fileHash, "ORGANIZE_ERROR_RECOVERY", trackedFile.OriginalPath, trackedFile.TargetPath);
                
                if (rollbackResult.IsSuccess)
                {
                    rollbackId = rollbackResult.Value;
                }
            }

            // 6. Record the error outcome
            await _errorClassificationService.RecordErrorOutcomeAsync(
                errorContext, classification, recovery, false);

            // 7. Create organization recovery result
            var organizationRecovery = new OrganizationRecoveryResult
            {
                RecommendedAction = MapToOrganizationRecoveryAction(recovery.ActionType),
                ShouldRetry = classification.CanRetry && attemptNumber < classification.MaxRetryAttempts,
                RetryDelayMs = classification.RecommendedRetryDelayMs,
                MaxRetryAttempts = classification.MaxRetryAttempts,
                RequiresUserIntervention = classification.RequiresUserIntervention,
                RecoveryDescription = recovery.Description,
                UserActionSteps = classification.ResolutionSteps.ToList(),
                RollbackOperationId = rollbackId
            };

            return Result<OrganizationRecoveryResult>.Success(organizationRecovery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle organization error for {FileHash}", fileHash);
            return Result<OrganizationRecoveryResult>.Failure($"Error handling failed: {ex.Message}");
        }
    }

    public async Task<Result<FileOrganizationStatus>> GetOrganizationStatusAsync(string fileHash)
    {
        try
        {
            // 1. Get tracked file
            var trackedFile = await _unitOfWork.TrackedFiles.GetByHashAsync(fileHash);
            if (trackedFile == null)
            {
                return Result<FileOrganizationStatus>.Failure($"File with hash {fileHash} not found");
            }

            // 2. Get current state
            var currentState = GetOrganizationState(fileHash);

            // 3. Get processing logs for this file
            var logs = (await _unitOfWork.ProcessingLogs.GetAllAsync())
                .Where(log => log.FileHash == fileHash && 
                             (log.Category.Contains("ORGANIZATION") || log.Category.Contains("ORGANIZE")))
                .OrderBy(log => log.CreatedDate)
                .ToList();

            // 4. Build attempt history from logs
            var attemptHistory = new List<OrganizationAttempt>();
            var attemptCount = 0;
            DateTime? lastAttemptAt = null;
            string? lastError = null;

            foreach (var log in logs)
            {
                if (log.Category.Contains("ERROR") && log.Level == MediaButler.Core.Enums.LogLevel.Error)
                {
                    attemptCount++;
                    lastAttemptAt = log.CreatedDate;
                    lastError = log.Message;

                    attemptHistory.Add(new OrganizationAttempt
                    {
                        AttemptedAt = log.CreatedDate,
                        WasSuccessful = false,
                        DurationMs = log.DurationMs ?? 0,
                        ErrorMessage = log.Message,
                        AttemptedPath = trackedFile.TargetPath,
                        RecoveryAction = OrganizationRecoveryAction.None // Would need to parse from details
                    });
                }
                else if (log.Level == MediaButler.Core.Enums.LogLevel.Information && log.Message.Contains("successfully"))
                {
                    attemptCount++;
                    lastAttemptAt = log.CreatedDate;

                    attemptHistory.Add(new OrganizationAttempt
                    {
                        AttemptedAt = log.CreatedDate,
                        WasSuccessful = true,
                        DurationMs = log.DurationMs ?? 0,
                        ErrorMessage = null,
                        AttemptedPath = trackedFile.MovedToPath ?? trackedFile.TargetPath,
                        RecoveryAction = OrganizationRecoveryAction.None
                    });
                }
            }

            // 5. Calculate progress percentage based on status
            var progressPercentage = trackedFile.Status switch
            {
                FileStatus.New => 0,
                FileStatus.Processing => 10,
                FileStatus.Classified => 25,
                FileStatus.ReadyToMove => 50,
                FileStatus.Moving => 75,
                FileStatus.Moved => 100,
                FileStatus.Error => 0,
                FileStatus.Retry => 5,
                FileStatus.Ignored => 0,
                _ => 0
            };

            var status = new FileOrganizationStatus
            {
                State = currentState,
                TargetPath = trackedFile.TargetPath,
                AttemptCount = attemptCount,
                LastAttemptAt = lastAttemptAt,
                LastError = lastError,
                AttemptHistory = attemptHistory,
                IsInProgress = currentState == OrganizationState.InProgress,
                ProgressPercentage = progressPercentage
            };

            return Result<FileOrganizationStatus>.Success(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get organization status for {FileHash}", fileHash);
            return Result<FileOrganizationStatus>.Failure($"Status retrieval failed: {ex.Message}");
        }
    }

    #region Private Helper Methods

    private void SetOrganizationState(string fileHash, OrganizationState state)
    {
        lock (_stateLock)
        {
            _organizationStates[fileHash] = state;
        }
    }

    private OrganizationState GetOrganizationState(string fileHash)
    {
        lock (_stateLock)
        {
            return _organizationStates.TryGetValue(fileHash, out var state) ? state : OrganizationState.Pending;
        }
    }

    private async Task<List<string>> DiscoverRelatedFiles(string originalPath)
    {
        var relatedFiles = new List<string>();
        
        try
        {
            var directory = Path.GetDirectoryName(originalPath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExt))
                return relatedFiles;

            var relatedExtensions = new[] { ".srt", ".sub", ".ass", ".nfo", ".jpg", ".png" };
            
            foreach (var ext in relatedExtensions)
            {
                var relatedFile = Path.Combine(directory, fileNameWithoutExt + ext);
                if (File.Exists(relatedFile))
                {
                    relatedFiles.Add(relatedFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover related files for {OriginalPath}", originalPath);
        }

        return relatedFiles;
    }

    private long GetAvailableSpace(string directory)
    {
        try
        {
            if (string.IsNullOrEmpty(directory))
                return 0;

            var drive = new DriveInfo(Path.GetPathRoot(directory) ?? directory);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0; // Default to 0 if we can't determine space
        }
    }

    private long EstimateOrganizationDuration(long totalSize, int relatedFileCount)
    {
        // Simple heuristic: 100MB/s transfer rate + 100ms per related file
        var transferTime = (long)(totalSize / (100.0 * 1024 * 1024) * 1000); // Convert to milliseconds
        var relatedFileTime = relatedFileCount * 100;
        var baseOverhead = 500; // Base overhead for validation, etc.

        return Math.Max(transferTime + relatedFileTime + baseOverhead, 1000);
    }

    private Result<OrganizationRecoveryResult> CreateGenericRecoveryResult(int attemptNumber)
    {
        var recovery = new OrganizationRecoveryResult
        {
            RecommendedAction = OrganizationRecoveryAction.WaitForUser,
            ShouldRetry = false,
            RetryDelayMs = 0,
            MaxRetryAttempts = 0,
            RequiresUserIntervention = true,
            RecoveryDescription = "An unexpected error occurred during organization. Manual investigation required.",
            UserActionSteps = new[]
            {
                "Check system logs for detailed error information",
                "Verify file system permissions and available space",
                "Contact system administrator if the problem persists"
            }
        };

        return Result<OrganizationRecoveryResult>.Success(recovery);
    }

    private OrganizationRecoveryAction MapToOrganizationRecoveryAction(ErrorRecoveryType errorRecoveryType)
    {
        return errorRecoveryType switch
        {
            ErrorRecoveryType.AutomaticRetry => OrganizationRecoveryAction.Retry,
            ErrorRecoveryType.WaitForUserIntervention => OrganizationRecoveryAction.WaitForUser,
            ErrorRecoveryType.LogAndFail => OrganizationRecoveryAction.Skip,
            ErrorRecoveryType.EscalateToAdmin => OrganizationRecoveryAction.Escalate,
            _ => OrganizationRecoveryAction.None
        };
    }

    private async Task RecordOrganizationError(string fileHash, Exception error, long durationMs)
    {
        try
        {
            _unitOfWork.ProcessingLogs.Add(ProcessingLog.Error(
                fileHash,
                "ORGANIZATION_ERROR",
                "File organization failed with unexpected error",
                error,
                JsonSerializer.Serialize(new { 
                    ErrorType = error.GetType().Name,
                    ErrorMessage = error.Message,
                    StackTrace = error.StackTrace 
                }),
                durationMs));

            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record organization error for {FileHash}", fileHash);
        }
    }

    #endregion
}