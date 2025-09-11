using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Common;

namespace MediaButler.Services.FileOperations;

/// <summary>
/// Defines atomic file operations with safety guarantees and audit trail integration.
/// Follows "Simple Made Easy" principles with clear separation between validation, 
/// operation execution, and audit recording.
/// </summary>
/// <remarks>
/// This service provides core file operations for MediaButler:
/// - Atomic file movement with rollback capability
/// - Safe directory structure creation
/// - Pre-flight validation to prevent operation failures
/// - Comprehensive audit trail integration
/// 
/// Design Principles:
/// - Use OS-level atomic operations (no custom transaction layer)
/// - Validate before executing (fail fast)
/// - Record all operations for audit and potential rollback
/// - Simple copy-then-delete for cross-drive scenarios
/// </remarks>
public interface IFileOperationService
{
    /// <summary>
    /// Moves a file to a new location atomically with comprehensive validation.
    /// </summary>
    /// <param name="fileHash">Hash of the tracked file to move</param>
    /// <param name="targetPath">Destination path for the file</param>
    /// <param name="createDirectories">Whether to create target directories if they don't exist</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing success status and operation details</returns>
    /// <remarks>
    /// This operation:
    /// - Validates source file exists and is accessible
    /// - Validates target path and creates directories if requested  
    /// - Performs atomic move or copy-then-delete for cross-drive moves
    /// - Records operation in audit trail for potential rollback
    /// - Updates TrackedFile entity with new path
    /// </remarks>
    Task<Result<FileOperationResult>> MoveFileAsync(
        string fileHash, 
        string targetPath, 
        bool createDirectories = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates directory structure safely with appropriate permissions.
    /// </summary>
    /// <param name="directoryPath">Full path to the directory to create</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating success and created directory information</returns>
    /// <remarks>
    /// - Creates parent directories recursively as needed
    /// - Sets appropriate permissions for ARM32/Linux compatibility
    /// - Handles path validation and sanitization
    /// - Records directory creation for audit trail
    /// </remarks>
    Task<Result<DirectoryOperationResult>> CreateDirectoryStructureAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a file operation before execution to prevent failures.
    /// </summary>
    /// <param name="fileHash">Hash of the file to validate</param>
    /// <param name="targetPath">Proposed target path for the operation</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Validation result with detailed feedback</returns>
    /// <remarks>
    /// Pre-flight checks include:
    /// - Source file existence and accessibility
    /// - Target path validity and permissions
    /// - Disk space availability
    /// - Path length and character validation
    /// - Conflict detection (existing files)
    /// </remarks>
    Task<Result<FileOperationValidationResult>> ValidateOperationAsync(
        string fileHash,
        string targetPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a file operation in the audit trail for tracking and potential rollback.
    /// </summary>
    /// <param name="operation">Details of the operation to record</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result indicating successful recording</returns>
    /// <remarks>
    /// - Integrates with ProcessingLog for comprehensive audit trail
    /// - Stores operation details for potential rollback scenarios
    /// - Links to TrackedFile entity for complete tracking
    /// - Includes timing and performance metrics
    /// </remarks>
    Task<Result> RecordOperationAsync(
        FileOperationRecord operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status and metrics for ongoing file operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Current operation statistics and performance metrics</returns>
    /// <remarks>
    /// Provides insights into:
    /// - Active operations count
    /// - Recent operation success/failure rates
    /// - Average operation time
    /// - Disk usage and available space
    /// </remarks>
    Task<Result<FileOperationStats>> GetOperationStatsAsync(
        CancellationToken cancellationToken = default);
}