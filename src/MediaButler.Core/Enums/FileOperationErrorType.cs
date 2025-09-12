namespace MediaButler.Core.Enums;

/// <summary>
/// Defines the classification of file operation errors for appropriate recovery strategies.
/// Following "Simple Made Easy" principles with clear, actionable error categories.
/// </summary>
/// <remarks>
/// This enum provides a simple classification system that guides both automatic retry logic
/// and user intervention requirements. Each category has specific recovery strategies:
/// - TransientError: Automatic retry with exponential backoff
/// - PermissionError: User intervention needed to resolve access rights
/// - SpaceError: User intervention needed to free disk space or change target
/// - PathError: Path validation and user correction required
/// - UnknownError: Manual investigation and potentially system-level debugging
/// </remarks>
public enum FileOperationErrorType
{
    /// <summary>
    /// Temporary error that may resolve automatically with retry.
    /// Examples: Network timeouts, temporary file locks, transient I/O errors.
    /// Recovery: Automatic retry with exponential backoff up to maximum attempts.
    /// </summary>
    TransientError = 1,

    /// <summary>
    /// File system permission error requiring user intervention.
    /// Examples: Insufficient permissions, read-only file systems, access denied.
    /// Recovery: User must resolve permission issues, then manual retry.
    /// </summary>
    PermissionError = 2,

    /// <summary>
    /// Insufficient disk space error requiring user action.
    /// Examples: Disk full, quota exceeded, insufficient free space.
    /// Recovery: User must free space or change target location, then retry.
    /// </summary>
    SpaceError = 3,

    /// <summary>
    /// Invalid or problematic path error requiring path correction.
    /// Examples: Path too long, invalid characters, non-existent directories.
    /// Recovery: Path validation and user correction, then retry.
    /// </summary>
    PathError = 4,

    /// <summary>
    /// Unknown or unexpected error requiring manual investigation.
    /// Examples: Corrupted files, hardware failures, unexpected exceptions.
    /// Recovery: Manual investigation, potential system-level debugging needed.
    /// </summary>
    UnknownError = 5
}