using MediaButler.Core.Common;

namespace MediaButler.Core.Services;

/// <summary>
/// Service interface for managing file operation rollback functionality.
/// Provides simple rollback mechanisms without complex transaction systems.
/// Follows "Simple Made Easy" principles by relying on atomic OS operations and existing audit infrastructure.
/// </summary>
public interface IRollbackService
{
    /// <summary>
    /// Creates a rollback point for a file operation.
    /// Stores rollback information in the ProcessingLog table for audit trail and recovery.
    /// </summary>
    /// <param name="fileHash">The hash of the file being operated on</param>
    /// <param name="operationType">The type of operation being performed</param>
    /// <param name="originalPath">The original file path before the operation</param>
    /// <param name="targetPath">The target file path after the operation</param>
    /// <param name="additionalInfo">Optional additional information needed for rollback</param>
    /// <returns>Result containing the rollback operation ID, or failure</returns>
    Task<Result<Guid>> CreateRollbackPointAsync(
        string fileHash,
        string operationType,
        string originalPath,
        string? targetPath = null,
        string? additionalInfo = null);

    /// <summary>
    /// Executes a rollback operation by reversing the original file operation.
    /// Uses simple file-based rollback (move file back to original location).
    /// </summary>
    /// <param name="operationId">The ID of the rollback operation to execute</param>
    /// <returns>Result indicating success or failure of the rollback operation</returns>
    Task<Result> ExecuteRollbackAsync(Guid operationId);

    /// <summary>
    /// Executes rollback for the most recent operation on a specific file.
    /// Convenience method for common rollback scenarios.
    /// </summary>
    /// <param name="fileHash">The hash of the file to rollback</param>
    /// <returns>Result indicating success or failure of the rollback operation</returns>
    Task<Result> RollbackLastOperationAsync(string fileHash);

    /// <summary>
    /// Validates the integrity of a rollback point to ensure it can be executed.
    /// Checks that original and target paths exist and are accessible.
    /// </summary>
    /// <param name="operationId">The ID of the rollback operation to validate</param>
    /// <returns>Result containing validation details, or failure if invalid</returns>
    Task<Result<RollbackValidationResult>> ValidateRollbackIntegrityAsync(Guid operationId);

    /// <summary>
    /// Cleans up rollback history by removing old rollback points.
    /// Maintains audit trail while preventing unlimited growth of rollback data.
    /// </summary>
    /// <param name="olderThan">Remove rollback points older than this date</param>
    /// <returns>Result containing the number of rollback points cleaned up</returns>
    Task<Result<int>> CleanupRollbackHistoryAsync(DateTime olderThan);

    /// <summary>
    /// Gets the rollback history for a specific file.
    /// Returns available rollback points in chronological order (newest first).
    /// </summary>
    /// <param name="fileHash">The hash of the file to get rollback history for</param>
    /// <returns>Result containing the list of available rollback points</returns>
    Task<Result<IReadOnlyList<RollbackPoint>>> GetRollbackHistoryAsync(string fileHash);
}

/// <summary>
/// Represents a rollback point in the system.
/// Contains all information needed to reverse a file operation.
/// </summary>
public record RollbackPoint
{
    /// <summary>
    /// Unique identifier for this rollback point.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Hash of the file this rollback point relates to.
    /// </summary>
    public string FileHash { get; init; } = string.Empty;

    /// <summary>
    /// Type of operation that can be rolled back.
    /// </summary>
    public string OperationType { get; init; } = string.Empty;

    /// <summary>
    /// Original file path before the operation.
    /// </summary>
    public string OriginalPath { get; init; } = string.Empty;

    /// <summary>
    /// Target file path after the operation.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Additional information needed for rollback.
    /// </summary>
    public string? AdditionalInfo { get; init; }

    /// <summary>
    /// When this rollback point was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Whether this rollback point can still be executed.
    /// </summary>
    public bool CanRollback { get; init; }
}

/// <summary>
/// Result of rollback validation operation.
/// Contains detailed information about rollback feasibility.
/// </summary>
public record RollbackValidationResult
{
    /// <summary>
    /// Whether the rollback can be executed successfully.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Detailed validation messages explaining any issues.
    /// </summary>
    public IReadOnlyList<string> ValidationMessages { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether the original file location is accessible.
    /// </summary>
    public bool OriginalLocationAccessible { get; init; }

    /// <summary>
    /// Whether the target file exists and can be moved.
    /// </summary>
    public bool TargetFileExists { get; init; }

    /// <summary>
    /// Estimated success probability of the rollback operation.
    /// </summary>
    public double SuccessProbability { get; init; }
}