using MediaButler.Core.Enums;

namespace MediaButler.Core.Models;

/// <summary>
/// Result of error classification analysis with recovery recommendations.
/// Provides actionable information for both automatic retry logic and user guidance.
/// </summary>
public record ErrorClassificationResult
{
    /// <summary>
    /// The classified error type determining recovery strategy.
    /// </summary>
    public FileOperationErrorType ErrorType { get; init; }

    /// <summary>
    /// Whether this error can be automatically retried.
    /// </summary>
    public bool CanRetry { get; init; }

    /// <summary>
    /// Whether user intervention is required for resolution.
    /// </summary>
    public bool RequiresUserIntervention { get; init; }

    /// <summary>
    /// Recommended retry delay in milliseconds for automatic retries.
    /// </summary>
    public int RecommendedRetryDelayMs { get; init; }

    /// <summary>
    /// Maximum number of automatic retry attempts recommended.
    /// </summary>
    public int MaxRetryAttempts { get; init; }

    /// <summary>
    /// User-friendly description of the error and required actions.
    /// </summary>
    public string UserMessage { get; init; } = string.Empty;

    /// <summary>
    /// Technical details for logging and debugging purposes.
    /// </summary>
    public string TechnicalDetails { get; init; } = string.Empty;

    /// <summary>
    /// Suggested resolution steps for user intervention.
    /// </summary>
    public IReadOnlyList<string> ResolutionSteps { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Confidence level of the error classification (0.0 to 1.0).
    /// </summary>
    public double ClassificationConfidence { get; init; }

    /// <summary>
    /// Creates a classification result for transient errors with automatic retry.
    /// </summary>
    public static ErrorClassificationResult TransientError(string message, string technical, int retryDelayMs = 1000, int maxRetries = 3)
    {
        return new ErrorClassificationResult
        {
            ErrorType = FileOperationErrorType.TransientError,
            CanRetry = true,
            RequiresUserIntervention = false,
            RecommendedRetryDelayMs = retryDelayMs,
            MaxRetryAttempts = maxRetries,
            UserMessage = message,
            TechnicalDetails = technical,
            ClassificationConfidence = 0.8,
            ResolutionSteps = new[] { "The system will automatically retry this operation" }
        };
    }

    /// <summary>
    /// Creates a classification result for permission errors requiring user action.
    /// </summary>
    public static ErrorClassificationResult PermissionError(string message, string technical, IReadOnlyList<string> steps)
    {
        return new ErrorClassificationResult
        {
            ErrorType = FileOperationErrorType.PermissionError,
            CanRetry = false,
            RequiresUserIntervention = true,
            RecommendedRetryDelayMs = 0,
            MaxRetryAttempts = 0,
            UserMessage = message,
            TechnicalDetails = technical,
            ClassificationConfidence = 0.9,
            ResolutionSteps = steps
        };
    }

    /// <summary>
    /// Creates a classification result for space-related errors.
    /// </summary>
    public static ErrorClassificationResult SpaceError(string message, string technical, IReadOnlyList<string> steps)
    {
        return new ErrorClassificationResult
        {
            ErrorType = FileOperationErrorType.SpaceError,
            CanRetry = false,
            RequiresUserIntervention = true,
            RecommendedRetryDelayMs = 0,
            MaxRetryAttempts = 0,
            UserMessage = message,
            TechnicalDetails = technical,
            ClassificationConfidence = 0.95,
            ResolutionSteps = steps
        };
    }

    /// <summary>
    /// Creates a classification result for path-related errors.
    /// </summary>
    public static ErrorClassificationResult PathError(string message, string technical, IReadOnlyList<string> steps)
    {
        return new ErrorClassificationResult
        {
            ErrorType = FileOperationErrorType.PathError,
            CanRetry = false,
            RequiresUserIntervention = true,
            RecommendedRetryDelayMs = 0,
            MaxRetryAttempts = 0,
            UserMessage = message,
            TechnicalDetails = technical,
            ClassificationConfidence = 0.85,
            ResolutionSteps = steps
        };
    }

    /// <summary>
    /// Creates a classification result for unknown errors requiring investigation.
    /// </summary>
    public static ErrorClassificationResult UnknownError(string message, string technical)
    {
        return new ErrorClassificationResult
        {
            ErrorType = FileOperationErrorType.UnknownError,
            CanRetry = false,
            RequiresUserIntervention = true,
            RecommendedRetryDelayMs = 0,
            MaxRetryAttempts = 0,
            UserMessage = message,
            TechnicalDetails = technical,
            ClassificationConfidence = 0.5,
            ResolutionSteps = new[] 
            { 
                "Check system logs for additional error details",
                "Verify file system health and integrity", 
                "Contact system administrator if problem persists"
            }
        };
    }
}

/// <summary>
/// Recovery action to be taken based on error classification.
/// Provides clear guidance for both automatic and manual recovery procedures.
/// </summary>
public record ErrorRecoveryAction
{
    /// <summary>
    /// The type of recovery action to perform.
    /// </summary>
    public ErrorRecoveryType ActionType { get; init; }

    /// <summary>
    /// Delay before executing this recovery action.
    /// </summary>
    public TimeSpan Delay { get; init; }

    /// <summary>
    /// Parameters specific to this recovery action.
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Human-readable description of this recovery action.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Types of recovery actions that can be taken for file operation errors.
/// </summary>
public enum ErrorRecoveryType
{
    /// <summary>
    /// No recovery action - operation has failed permanently.
    /// </summary>
    None = 0,

    /// <summary>
    /// Automatically retry the operation after a delay.
    /// </summary>
    AutomaticRetry = 1,

    /// <summary>
    /// Wait for user intervention then allow manual retry.
    /// </summary>
    WaitForUserIntervention = 2,

    /// <summary>
    /// Log error details and mark operation as failed.
    /// </summary>
    LogAndFail = 3,

    /// <summary>
    /// Escalate to system administrator or support.
    /// </summary>
    EscalateToAdmin = 4
}

/// <summary>
/// Comprehensive error context information for classification analysis.
/// Contains all relevant information needed to properly classify and handle errors.
/// </summary>
public record ErrorContext
{
    /// <summary>
    /// The original exception that occurred.
    /// </summary>
    public Exception Exception { get; init; } = null!;

    /// <summary>
    /// Type of file operation that failed.
    /// </summary>
    public string OperationType { get; init; } = string.Empty;

    /// <summary>
    /// Source file path for the operation.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Target file path for the operation.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Size of the file being operated on.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Available space at the target location.
    /// </summary>
    public long? AvailableSpace { get; init; }

    /// <summary>
    /// Number of previous retry attempts for this operation.
    /// </summary>
    public int RetryAttempts { get; init; }

    /// <summary>
    /// Hash of the file being processed.
    /// </summary>
    public string FileHash { get; init; } = string.Empty;

    /// <summary>
    /// Additional context information for classification.
    /// </summary>
    public Dictionary<string, object> AdditionalContext { get; init; } = new();
}