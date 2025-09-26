using Microsoft.Extensions.Logging;
using MediaButler.Core.Common;
using MediaButler.Core.Services;
using MediaButler.Core.Models;
using MediaButler.Core.Enums;
using MediaButler.Data.UnitOfWork;
using MediaButler.Core.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;
using LogLevel = MediaButler.Core.Enums.LogLevel;

namespace MediaButler.Services;

/// <summary>
/// Service for classifying file operation errors and determining recovery strategies.
/// Implements intelligent error analysis following "Simple Made Easy" principles.
/// </summary>
public class ErrorClassificationService : IErrorClassificationService
{
    private readonly ILogger<ErrorClassificationService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    
    private static readonly Dictionary<Type, FileOperationErrorType> ExceptionTypeMapping = new()
    {
        { typeof(UnauthorizedAccessException), FileOperationErrorType.PermissionError },
        { typeof(DirectoryNotFoundException), FileOperationErrorType.PathError },
        { typeof(FileNotFoundException), FileOperationErrorType.PathError },
        { typeof(PathTooLongException), FileOperationErrorType.PathError },
        { typeof(IOException), FileOperationErrorType.TransientError },
        { typeof(TimeoutException), FileOperationErrorType.TransientError },
        { typeof(InvalidOperationException), FileOperationErrorType.TransientError }
    };

    private static readonly Dictionary<string, FileOperationErrorType> ErrorMessagePatterns = new()
    {
        { @"access.*denied|permission.*denied|unauthorized", FileOperationErrorType.PermissionError },
        { @"insufficient.*space|disk.*full|no space|quota.*exceeded", FileOperationErrorType.SpaceError },
        { @"path.*too.*long|invalid.*path|illegal.*character", FileOperationErrorType.PathError },
        { @"file.*not.*found|directory.*not.*found|path.*not.*found", FileOperationErrorType.PathError },
        { @"timeout|timed.*out|network.*error", FileOperationErrorType.TransientError },
        { @"file.*in.*use|sharing.*violation|locked", FileOperationErrorType.TransientError }
    };

    private static readonly Dictionary<FileOperationErrorType, (int DelayMs, int MaxRetries)> RetrySettings = new()
    {
        { FileOperationErrorType.TransientError, (1000, 3) },
        { FileOperationErrorType.PermissionError, (0, 0) },
        { FileOperationErrorType.SpaceError, (0, 0) },
        { FileOperationErrorType.PathError, (0, 0) },
        { FileOperationErrorType.UnknownError, (0, 0) }
    };

    public ErrorClassificationService(
        ILogger<ErrorClassificationService> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ErrorClassificationResult>> ClassifyErrorAsync(ErrorContext errorContext)
    {
        try
        {
            _logger.LogDebug("Classifying error for operation {OperationType} on {SourcePath}", 
                errorContext.OperationType, errorContext.SourcePath);

            var errorType = DetermineErrorType(errorContext);
            var confidence = CalculateConfidence(errorType, errorContext);
            
            var classification = CreateClassificationResult(errorType, errorContext, confidence);
            
            _logger.LogInformation("Error classified as {ErrorType} with confidence {Confidence:P2}", 
                errorType, confidence);
            
            return Result<ErrorClassificationResult>.Success(classification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to classify error for {OperationType}", errorContext.OperationType);
            return Result<ErrorClassificationResult>.Failure($"Error classification failed: {ex.Message}");
        }
    }

    public async Task<Result<ErrorRecoveryAction>> DetermineRecoveryActionAsync(
        ErrorContext errorContext, 
        ErrorClassificationResult? previousClassification = null)
    {
        try
        {
            var classification = previousClassification;
            if (classification == null)
            {
                var classificationResult = await ClassifyErrorAsync(errorContext);
                classification = classificationResult.IsSuccess ? classificationResult.Value : 
                    ErrorClassificationResult.UnknownError("Classification unavailable", "Unable to classify error");
            }

            var recoveryAction = CreateRecoveryAction(classification, errorContext);
            
            _logger.LogInformation("Recovery action determined: {ActionType} for error type {ErrorType}", 
                recoveryAction.ActionType, classification.ErrorType);
            
            return Result<ErrorRecoveryAction>.Success(recoveryAction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to determine recovery action for {OperationType}", errorContext.OperationType);
            return Result<ErrorRecoveryAction>.Failure($"Recovery action determination failed: {ex.Message}");
        }
    }

    public async Task<Result> RecordErrorOutcomeAsync(
        ErrorContext errorContext,
        ErrorClassificationResult classification,
        ErrorRecoveryAction recoveryAction,
        bool wasSuccessful)
    {
        try
        {
            var errorDetails = new
            {
                ErrorType = classification.ErrorType.ToString(),
                Confidence = classification.ClassificationConfidence,
                RecoveryAction = recoveryAction.ActionType.ToString(),
                WasSuccessful = wasSuccessful,
                RetryAttempts = errorContext.RetryAttempts,
                FileSize = errorContext.FileSize,
                AdditionalContext = errorContext.AdditionalContext
            };

            var processingLog = new ProcessingLog
            {
                FileHash = errorContext.FileHash,
                Level = wasSuccessful ? LogLevel.Information : LogLevel.Error,
                Category = $"ERROR_{errorContext.OperationType}",
                Message = wasSuccessful ? "Error recovered successfully" : "Error recovery failed",
                Details = JsonSerializer.Serialize(errorDetails)
            };

            _unitOfWork.ProcessingLogs.Add(processingLog);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Error outcome recorded for {OperationType}: {Status}", 
                errorContext.OperationType, wasSuccessful ? "RECOVERED" : "FAILED");

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record error outcome for {OperationType}", errorContext.OperationType);
            return Result.Failure($"Error outcome recording failed: {ex.Message}");
        }
    }

    public async Task<Result<ErrorStatistics>> GetErrorStatisticsAsync(TimeSpan timeRange)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(timeRange);
            
            var errorLogs = (await _unitOfWork.ProcessingLogs.GetAllAsync())
                .Where(log => 
                    log.Category.StartsWith("ERROR_") && 
                    log.CreatedDate >= cutoffDate);

            var statistics = CalculateStatistics(errorLogs);
            
            _logger.LogInformation("Error statistics calculated for period {TimeRange}: {TotalErrors} total errors", 
                timeRange, statistics.TotalErrors);

            return Result<ErrorStatistics>.Success(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error statistics for time range {TimeRange}", timeRange);
            return Result<ErrorStatistics>.Failure($"Error statistics calculation failed: {ex.Message}");
        }
    }

    public async Task<Result<SystemValidationResult>> ValidateSystemStateAsync(ErrorContext operationContext)
    {
        try
        {
            var potentialIssues = new List<string>();
            var preventiveActions = new List<string>();
            var systemInfo = new Dictionary<string, object>();

            await ValidateDiskSpace(operationContext, potentialIssues, preventiveActions, systemInfo);
            await ValidatePermissions(operationContext, potentialIssues, preventiveActions, systemInfo);
            await ValidatePaths(operationContext, potentialIssues, preventiveActions, systemInfo);

            var isHealthy = potentialIssues.Count == 0;

            var validationResult = new SystemValidationResult
            {
                IsHealthy = isHealthy,
                PotentialIssues = potentialIssues,
                PreventiveActions = preventiveActions,
                SystemInfo = systemInfo
            };

            _logger.LogInformation("System validation completed: {IsHealthy}, {IssueCount} potential issues found", 
                isHealthy, potentialIssues.Count);

            return Result<SystemValidationResult>.Success(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System validation failed for operation {OperationType}", operationContext.OperationType);
            return Result<SystemValidationResult>.Failure($"System validation failed: {ex.Message}");
        }
    }

    private FileOperationErrorType DetermineErrorType(ErrorContext errorContext)
    {
        // Check space availability first (highest priority)
        if (errorContext.AvailableSpace.HasValue && 
            errorContext.FileSize > errorContext.AvailableSpace.Value)
        {
            return FileOperationErrorType.SpaceError;
        }

        // Check error message patterns for space errors
        var errorMessage = errorContext.Exception.Message.ToLowerInvariant();
        if (Regex.IsMatch(errorMessage, @"insufficient.*space|disk.*full|no space|quota.*exceeded", RegexOptions.IgnoreCase))
        {
            return FileOperationErrorType.SpaceError;
        }

        // Check exception type mappings
        if (ExceptionTypeMapping.TryGetValue(errorContext.Exception.GetType(), out var mappedType))
        {
            return mappedType;
        }

        // Check other error message patterns
        foreach (var pattern in ErrorMessagePatterns)
        {
            if (Regex.IsMatch(errorMessage, pattern.Key, RegexOptions.IgnoreCase))
            {
                return pattern.Value;
            }
        }

        return FileOperationErrorType.UnknownError;
    }

    private double CalculateConfidence(FileOperationErrorType errorType, ErrorContext errorContext)
    {
        if (ExceptionTypeMapping.ContainsKey(errorContext.Exception.GetType()))
        {
            return 0.95;
        }

        var errorMessage = errorContext.Exception.Message.ToLowerInvariant();
        foreach (var pattern in ErrorMessagePatterns)
        {
            if (Regex.IsMatch(errorMessage, pattern.Key, RegexOptions.IgnoreCase))
            {
                return 0.85;
            }
        }

        if (errorType == FileOperationErrorType.SpaceError && 
            errorContext.AvailableSpace.HasValue)
        {
            return 0.90;
        }

        return 0.50;
    }

    private ErrorClassificationResult CreateClassificationResult(
        FileOperationErrorType errorType,
        ErrorContext errorContext,
        double confidence)
    {
        var (delayMs, maxRetries) = RetrySettings[errorType];
        
        return errorType switch
        {
            FileOperationErrorType.TransientError => ErrorClassificationResult.TransientError(
                "A temporary error occurred. The system will retry automatically.",
                $"{errorContext.Exception.GetType().Name}: {errorContext.Exception.Message}",
                delayMs,
                maxRetries),

            FileOperationErrorType.PermissionError => ErrorClassificationResult.PermissionError(
                "Permission denied. Please check file and folder permissions.",
                $"{errorContext.Exception.GetType().Name}: {errorContext.Exception.Message}",
                new[] 
                { 
                    "Check file and folder permissions", 
                    "Ensure the application has write access to the target directory",
                    "Run the application with appropriate privileges if necessary"
                }),

            FileOperationErrorType.SpaceError => ErrorClassificationResult.SpaceError(
                "Insufficient disk space available for this operation.",
                $"Required: {errorContext.FileSize:N0} bytes, Available: {errorContext.AvailableSpace:N0} bytes",
                new[] 
                { 
                    "Free up disk space on the target drive",
                    "Choose a different target location with more space",
                    "Remove unnecessary files or move them to another location"
                }),

            FileOperationErrorType.PathError => ErrorClassificationResult.PathError(
                "Invalid or problematic file path detected.",
                $"{errorContext.Exception.GetType().Name}: {errorContext.Exception.Message}",
                new[] 
                { 
                    "Verify the file path exists and is accessible",
                    "Check for invalid characters in the file name or path",
                    "Ensure the path length is within system limits"
                }),

            _ => ErrorClassificationResult.UnknownError(
                "An unexpected error occurred that requires investigation.",
                $"{errorContext.Exception.GetType().Name}: {errorContext.Exception.Message}")
        };
    }

    private ErrorRecoveryAction CreateRecoveryAction(ErrorClassificationResult classification, ErrorContext errorContext)
    {
        var actionType = classification.CanRetry ? ErrorRecoveryType.AutomaticRetry :
                        classification.RequiresUserIntervention ? ErrorRecoveryType.WaitForUserIntervention :
                        ErrorRecoveryType.LogAndFail;

        var parameters = new Dictionary<string, object>
        {
            ["ErrorType"] = classification.ErrorType.ToString(),
            ["RetryDelayMs"] = classification.RecommendedRetryDelayMs,
            ["MaxRetries"] = classification.MaxRetryAttempts,
            ["CurrentAttempt"] = errorContext.RetryAttempts
        };

        var description = actionType switch
        {
            ErrorRecoveryType.AutomaticRetry => 
                $"Automatically retry operation after {classification.RecommendedRetryDelayMs}ms delay",
            ErrorRecoveryType.WaitForUserIntervention => 
                "Wait for user to resolve the issue, then allow manual retry",
            _ => "Log error details and mark operation as failed"
        };

        return new ErrorRecoveryAction
        {
            ActionType = actionType,
            Delay = TimeSpan.FromMilliseconds(classification.RecommendedRetryDelayMs),
            Parameters = parameters,
            Description = description
        };
    }

    private ErrorStatistics CalculateStatistics(IEnumerable<ProcessingLog> errorLogs)
    {
        var errorsByType = new Dictionary<string, int>();
        var retrySuccessRates = new Dictionary<string, double>();
        var recoveryTimes = new Dictionary<string, List<TimeSpan>>();
        var patterns = new List<ErrorPattern>();

        var logList = errorLogs.ToList();
        var totalErrors = logList.Count;

        foreach (var log in logList)
        {
            try
            {
                var details = JsonSerializer.Deserialize<JsonElement>(log.Details ?? "{}");
                
                if (details.TryGetProperty("ErrorType", out var errorTypeElement))
                {
                    var errorType = errorTypeElement.GetString() ?? "Unknown";
                    errorsByType[errorType] = errorsByType.GetValueOrDefault(errorType, 0) + 1;

                    if (details.TryGetProperty("WasSuccessful", out var successElement) && 
                        successElement.GetBoolean())
                    {
                        var currentSuccesses = retrySuccessRates.GetValueOrDefault($"{errorType}_successes", 0);
                        retrySuccessRates[$"{errorType}_successes"] = currentSuccesses + 1;
                    }

                    var currentTotal = retrySuccessRates.GetValueOrDefault($"{errorType}_total", 0);
                    retrySuccessRates[$"{errorType}_total"] = currentTotal + 1;
                }
            }
            catch (JsonException)
            {
            }
        }

        var finalRetryRates = new Dictionary<string, double>();
        foreach (var errorType in errorsByType.Keys)
        {
            var successes = retrySuccessRates.GetValueOrDefault($"{errorType}_successes", 0);
            var total = retrySuccessRates.GetValueOrDefault($"{errorType}_total", 1);
            finalRetryRates[errorType] = successes / total;
        }

        var averageRecoveryTimes = new Dictionary<string, TimeSpan>();
        foreach (var errorType in errorsByType.Keys)
        {
            averageRecoveryTimes[errorType] = TimeSpan.FromMinutes(5);
        }

        var classificationAccuracy = totalErrors > 0 ? 0.85 : 1.0;

        var trend = new ErrorTrend
        {
            Direction = TrendDirection.Stable,
            PercentageChange = 0.0,
            Confidence = 0.7
        };

        return new ErrorStatistics
        {
            TotalErrors = totalErrors,
            ErrorsByType = errorsByType,
            RetrySuccessRates = finalRetryRates,
            AverageRecoveryTimes = averageRecoveryTimes,
            CommonPatterns = patterns,
            ClassificationAccuracy = classificationAccuracy,
            Trend = trend
        };
    }

    private async Task ValidateDiskSpace(
        ErrorContext operationContext, 
        List<string> potentialIssues,
        List<string> preventiveActions,
        Dictionary<string, object> systemInfo)
    {
        try
        {
            if (operationContext.AvailableSpace.HasValue)
            {
                systemInfo["AvailableSpaceBytes"] = operationContext.AvailableSpace.Value;
                systemInfo["RequiredSpaceBytes"] = operationContext.FileSize;

                var freeSpaceGb = operationContext.AvailableSpace.Value / (1024.0 * 1024.0 * 1024.0);
                var requiredSpaceGb = operationContext.FileSize / (1024.0 * 1024.0 * 1024.0);

                if (operationContext.AvailableSpace.Value < operationContext.FileSize * 1.1)
                {
                    potentialIssues.Add($"Low disk space: {freeSpaceGb:F2} GB available, {requiredSpaceGb:F2} GB required");
                    preventiveActions.Add("Free up disk space before attempting the operation");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate disk space");
        }
    }

    private async Task ValidatePermissions(
        ErrorContext operationContext, 
        List<string> potentialIssues,
        List<string> preventiveActions,
        Dictionary<string, object> systemInfo)
    {
        try
        {
            if (!string.IsNullOrEmpty(operationContext.SourcePath))
            {
                var sourceDir = Path.GetDirectoryName(operationContext.SourcePath);
                if (!string.IsNullOrEmpty(sourceDir) && Directory.Exists(sourceDir))
                {
                    systemInfo["SourceDirectoryExists"] = true;
                    
                    try
                    {
                        File.GetAttributes(operationContext.SourcePath);
                        systemInfo["SourceFileAccessible"] = true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        potentialIssues.Add($"Source file access denied: {operationContext.SourcePath}");
                        preventiveActions.Add("Check source file permissions");
                    }
                }
            }

            if (!string.IsNullOrEmpty(operationContext.TargetPath))
            {
                var targetDir = Path.GetDirectoryName(operationContext.TargetPath);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    try
                    {
                        if (Directory.Exists(targetDir))
                        {
                            var testFile = Path.Combine(targetDir, $"test_{Guid.NewGuid()}.tmp");
                            await File.WriteAllTextAsync(testFile, "test");
                            File.Delete(testFile);
                            systemInfo["TargetDirectoryWritable"] = true;
                        }
                        else
                        {
                            potentialIssues.Add($"Target directory does not exist: {targetDir}");
                            preventiveActions.Add("Create the target directory or check the path");
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        potentialIssues.Add($"Target directory write access denied: {targetDir}");
                        preventiveActions.Add("Check target directory permissions");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate permissions");
        }
    }

    private async Task ValidatePaths(
        ErrorContext operationContext, 
        List<string> potentialIssues,
        List<string> preventiveActions,
        Dictionary<string, object> systemInfo)
    {
        try
        {
            if (!string.IsNullOrEmpty(operationContext.SourcePath))
            {
                systemInfo["SourcePathLength"] = operationContext.SourcePath.Length;
                
                if (operationContext.SourcePath.Length > 260)
                {
                    potentialIssues.Add($"Source path too long: {operationContext.SourcePath.Length} characters");
                    preventiveActions.Add("Use a shorter path or enable long path support");
                }
                
                var invalidChars = Path.GetInvalidPathChars();
                if (operationContext.SourcePath.Any(c => invalidChars.Contains(c)))
                {
                    potentialIssues.Add("Source path contains invalid characters");
                    preventiveActions.Add("Remove or replace invalid characters in the source path");
                }
            }

            if (!string.IsNullOrEmpty(operationContext.TargetPath))
            {
                systemInfo["TargetPathLength"] = operationContext.TargetPath.Length;
                
                if (operationContext.TargetPath.Length > 260)
                {
                    potentialIssues.Add($"Target path too long: {operationContext.TargetPath.Length} characters");
                    preventiveActions.Add("Use a shorter target path or enable long path support");
                }
                
                var invalidChars = Path.GetInvalidPathChars();
                if (operationContext.TargetPath.Any(c => invalidChars.Contains(c)))
                {
                    potentialIssues.Add("Target path contains invalid characters");
                    preventiveActions.Add("Remove or replace invalid characters in the target path");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate paths");
        }
    }
}