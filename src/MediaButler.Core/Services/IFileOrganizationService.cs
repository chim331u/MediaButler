using MediaButler.Core.Common;
using MediaButler.Core.Models;

namespace MediaButler.Core.Services;

/// <summary>
/// Service for orchestrating complete file organization workflows.
/// Coordinates path generation, validation, movement, and error recovery following "Simple Made Easy" principles.
/// </summary>
/// <remarks>
/// The FileOrganizationService serves as the main coordinator for the file organization process,
/// orchestrating multiple specialized services to provide a simple, unified interface for file organization.
/// It handles the complete workflow from category confirmation through successful file placement.
/// </remarks>
public interface IFileOrganizationService
{
    /// <summary>
    /// Organizes a file by moving it from its original location to the appropriate target location.
    /// This is the main orchestration method that coordinates the complete organization workflow.
    /// </summary>
    /// <param name="fileHash">The unique hash identifier of the file to organize</param>
    /// <param name="confirmedCategory">The user-confirmed category for file organization</param>
    /// <returns>Result containing the organization outcome with target path and operation details</returns>
    Task<Result<FileOrganizationResult>> OrganizeFileAsync(string fileHash, string confirmedCategory);

    /// <summary>
    /// Performs a dry-run preview of file organization without executing any file operations.
    /// Provides complete visibility into what would happen during organization including potential issues.
    /// </summary>
    /// <param name="fileHash">The unique hash identifier of the file to preview organization for</param>
    /// <param name="proposedCategory">The proposed category for organization preview</param>
    /// <returns>Result containing the organization preview with all details and potential warnings</returns>
    Task<Result<FileOrganizationPreview>> PreviewOrganizationAsync(string fileHash, string proposedCategory);

    /// <summary>
    /// Validates the safety and feasibility of organizing a file to a specific target path.
    /// Performs comprehensive checks including permissions, space, conflicts, and system state.
    /// </summary>
    /// <param name="fileHash">The unique hash identifier of the file</param>
    /// <param name="targetPath">The proposed target path for the file</param>
    /// <returns>Result containing validation results with any issues or concerns identified</returns>
    Task<Result<OrganizationValidationResult>> ValidateOrganizationSafetyAsync(string fileHash, string targetPath);

    /// <summary>
    /// Handles organization errors by determining recovery actions and executing appropriate responses.
    /// Uses error classification to determine if retry, rollback, or user intervention is needed.
    /// </summary>
    /// <param name="fileHash">The unique hash identifier of the file that failed to organize</param>
    /// <param name="organizationError">The error that occurred during organization</param>
    /// <param name="attemptNumber">The current attempt number for this organization</param>
    /// <returns>Result containing the error recovery action and guidance</returns>
    Task<Result<OrganizationRecoveryResult>> HandleOrganizationErrorAsync(
        string fileHash, 
        Exception organizationError,
        int attemptNumber = 1);

    /// <summary>
    /// Gets the organization status for a specific file including current state and history.
    /// Provides complete visibility into the organization process and any previous attempts.
    /// </summary>
    /// <param name="fileHash">The unique hash identifier of the file</param>
    /// <returns>Result containing comprehensive organization status information</returns>
    Task<Result<FileOrganizationStatus>> GetOrganizationStatusAsync(string fileHash);
}

/// <summary>
/// Result of a file organization operation containing outcome details.
/// Provides comprehensive information about the organization process and results.
/// </summary>
public record FileOrganizationResult
{
    /// <summary>
    /// Whether the organization operation completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The final path where the file was organized.
    /// </summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>
    /// The actual path where the file ended up (may differ from target due to conflict resolution).
    /// </summary>
    public string ActualPath { get; init; } = string.Empty;

    /// <summary>
    /// List of related files that were also moved (subtitles, metadata, etc.).
    /// </summary>
    public IReadOnlyList<string> RelatedFilesMoved { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Duration of the organization operation in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Any warnings or issues encountered during organization that didn't prevent success.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Rollback operation ID for reverting this organization if needed.
    /// </summary>
    public Guid? RollbackOperationId { get; init; }
}

/// <summary>
/// Preview of what would happen during file organization without executing the operation.
/// Provides complete visibility into the planned organization process.
/// </summary>
public record FileOrganizationPreview
{
    /// <summary>
    /// The proposed target path for the file.
    /// </summary>
    public string ProposedPath { get; init; } = string.Empty;

    /// <summary>
    /// Whether the proposed organization appears safe to execute.
    /// </summary>
    public bool IsSafe { get; init; }

    /// <summary>
    /// List of related files that would also be moved.
    /// </summary>
    public IReadOnlyList<string> RelatedFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Estimated duration for the organization operation in milliseconds.
    /// </summary>
    public long EstimatedDurationMs { get; init; }

    /// <summary>
    /// Required disk space for the operation in bytes.
    /// </summary>
    public long RequiredSpaceBytes { get; init; }

    /// <summary>
    /// Available disk space at the target location in bytes.
    /// </summary>
    public long AvailableSpaceBytes { get; init; }

    /// <summary>
    /// Any potential issues or warnings identified during preview.
    /// </summary>
    public IReadOnlyList<string> PotentialIssues { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Recommended actions or preparations before executing organization.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of organization safety validation containing comprehensive checks.
/// Ensures organization can proceed safely with minimal risk of failure.
/// </summary>
public record OrganizationValidationResult
{
    /// <summary>
    /// Whether the organization appears safe to proceed.
    /// </summary>
    public bool IsSafe { get; init; }

    /// <summary>
    /// List of safety issues identified that would prevent organization.
    /// </summary>
    public IReadOnlyList<string> SafetyIssues { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of warnings that don't prevent organization but should be noted.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Detailed validation results from system checks.
    /// </summary>
    public Dictionary<string, object> ValidationDetails { get; init; } = new();

    /// <summary>
    /// Recommended actions to resolve any identified issues.
    /// </summary>
    public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of organization error recovery containing recommended actions.
/// Provides guidance for handling organization failures and potential recovery.
/// </summary>
public record OrganizationRecoveryResult
{
    /// <summary>
    /// The recommended recovery action to take.
    /// </summary>
    public OrganizationRecoveryAction RecommendedAction { get; init; }

    /// <summary>
    /// Whether the organization should be retried automatically.
    /// </summary>
    public bool ShouldRetry { get; init; }

    /// <summary>
    /// Delay before retry attempt in milliseconds (if applicable).
    /// </summary>
    public int RetryDelayMs { get; init; }

    /// <summary>
    /// Maximum number of retry attempts recommended.
    /// </summary>
    public int MaxRetryAttempts { get; init; }

    /// <summary>
    /// Whether user intervention is required to resolve the issue.
    /// </summary>
    public bool RequiresUserIntervention { get; init; }

    /// <summary>
    /// Detailed explanation of the error and recovery approach.
    /// </summary>
    public string RecoveryDescription { get; init; } = string.Empty;

    /// <summary>
    /// Steps the user should take if intervention is required.
    /// </summary>
    public IReadOnlyList<string> UserActionSteps { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Rollback operation ID if a rollback was created for this error.
    /// </summary>
    public Guid? RollbackOperationId { get; init; }
}

/// <summary>
/// Current status of file organization including history and state.
/// Provides comprehensive visibility into the organization process.
/// </summary>
public record FileOrganizationStatus
{
    /// <summary>
    /// Current organization state of the file.
    /// </summary>
    public OrganizationState State { get; init; }

    /// <summary>
    /// Current target path for organization (if determined).
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Number of organization attempts made.
    /// </summary>
    public int AttemptCount { get; init; }

    /// <summary>
    /// Timestamp of the last organization attempt.
    /// </summary>
    public DateTime? LastAttemptAt { get; init; }

    /// <summary>
    /// Last error encountered during organization (if any).
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// History of organization attempts and their outcomes.
    /// </summary>
    public IReadOnlyList<OrganizationAttempt> AttemptHistory { get; init; } = Array.Empty<OrganizationAttempt>();

    /// <summary>
    /// Whether the file is currently being organized.
    /// </summary>
    public bool IsInProgress { get; init; }

    /// <summary>
    /// Estimated completion percentage (0-100) for current operation.
    /// </summary>
    public int ProgressPercentage { get; init; }
}

/// <summary>
/// Types of recovery actions available for organization errors.
/// </summary>
public enum OrganizationRecoveryAction
{
    /// <summary>
    /// No recovery action - operation failed permanently.
    /// </summary>
    None = 0,

    /// <summary>
    /// Retry the organization operation automatically.
    /// </summary>
    Retry = 1,

    /// <summary>
    /// Rollback any partial changes and mark as failed.
    /// </summary>
    Rollback = 2,

    /// <summary>
    /// Wait for user intervention before proceeding.
    /// </summary>
    WaitForUser = 3,

    /// <summary>
    /// Skip this file and continue processing others.
    /// </summary>
    Skip = 4,

    /// <summary>
    /// Escalate to system administrator or support.
    /// </summary>
    Escalate = 5
}

/// <summary>
/// Current state of file organization process.
/// </summary>
public enum OrganizationState
{
    /// <summary>
    /// File is awaiting organization.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Organization is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Organization completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Organization failed and requires attention.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Organization was cancelled by user or system.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// File was skipped due to errors or user decision.
    /// </summary>
    Skipped = 5
}

/// <summary>
/// Record of a single organization attempt with its outcome.
/// </summary>
public record OrganizationAttempt
{
    /// <summary>
    /// Timestamp when the attempt was made.
    /// </summary>
    public DateTime AttemptedAt { get; init; }

    /// <summary>
    /// Whether the attempt was successful.
    /// </summary>
    public bool WasSuccessful { get; init; }

    /// <summary>
    /// Duration of the attempt in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Error message if the attempt failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Target path that was attempted.
    /// </summary>
    public string? AttemptedPath { get; init; }

    /// <summary>
    /// Recovery action taken after this attempt.
    /// </summary>
    public OrganizationRecoveryAction RecoveryAction { get; init; }
}