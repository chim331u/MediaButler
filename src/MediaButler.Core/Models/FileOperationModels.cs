namespace MediaButler.Core.Models;

/// <summary>
/// Result of a file move operation with detailed information.
/// </summary>
public record FileOperationResult
{
    /// <summary>
    /// Hash of the file that was moved.
    /// </summary>
    public required string FileHash { get; init; }

    /// <summary>
    /// Original path of the file before the operation.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Final path of the file after the operation.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Time taken to complete the operation.
    /// </summary>
    public TimeSpan OperationDuration { get; init; }

    /// <summary>
    /// Whether the operation required cross-drive copy.
    /// </summary>
    public bool WasCrossDriveOperation { get; init; }

    /// <summary>
    /// Whether directories were created as part of the operation.
    /// </summary>
    public bool DirectoriesCreated { get; init; }

    /// <summary>
    /// Timestamp when the operation was completed.
    /// </summary>
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata about the operation.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Result of a directory creation operation.
/// </summary>
public record DirectoryOperationResult
{
    /// <summary>
    /// The directory path that was created.
    /// </summary>
    public required string DirectoryPath { get; init; }

    /// <summary>
    /// Whether the directory already existed.
    /// </summary>
    public bool AlreadyExisted { get; init; }

    /// <summary>
    /// List of parent directories that were created.
    /// </summary>
    public List<string> CreatedDirectories { get; init; } = new();

    /// <summary>
    /// Permissions applied to the created directories.
    /// </summary>
    public string? AppliedPermissions { get; init; }

    /// <summary>
    /// Time taken to create the directory structure.
    /// </summary>
    public TimeSpan OperationDuration { get; init; }

    /// <summary>
    /// Timestamp when the operation was completed.
    /// </summary>
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of pre-flight validation for file operations.
/// </summary>
public record FileOperationValidationResult
{
    /// <summary>
    /// Whether the operation is valid and can proceed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation warnings (operation can proceed but with caveats).
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// List of validation errors (operation cannot proceed).
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Available disk space at the target location in bytes.
    /// </summary>
    public long AvailableSpaceBytes { get; init; }

    /// <summary>
    /// Required disk space for the operation in bytes.
    /// </summary>
    public long RequiredSpaceBytes { get; init; }

    /// <summary>
    /// Whether the target path already exists.
    /// </summary>
    public bool TargetExists { get; init; }

    /// <summary>
    /// Whether directories need to be created.
    /// </summary>
    public bool RequiresDirectoryCreation { get; init; }

    /// <summary>
    /// Whether the operation would require cross-drive copy.
    /// </summary>
    public bool IsCrossDriveOperation { get; init; }

    /// <summary>
    /// Estimated duration for the operation based on file size.
    /// </summary>
    public TimeSpan EstimatedDuration { get; init; }
}

/// <summary>
/// Record of a file operation for audit trail purposes.
/// </summary>
public record FileOperationRecord
{
    /// <summary>
    /// Unique identifier for this operation.
    /// </summary>
    public string OperationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Hash of the file involved in the operation.
    /// </summary>
    public required string FileHash { get; init; }

    /// <summary>
    /// Type of operation performed.
    /// </summary>
    public required FileOperationType OperationType { get; init; }

    /// <summary>
    /// Source path for the operation.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Target path for the operation.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time when the operation started.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Time when the operation completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of the operation.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Additional metadata about the operation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Statistics about file operations for monitoring and performance analysis.
/// </summary>
public record FileOperationStats
{
    /// <summary>
    /// Number of operations currently in progress.
    /// </summary>
    public int ActiveOperations { get; init; }

    /// <summary>
    /// Total number of completed operations in the current session.
    /// </summary>
    public int CompletedOperations { get; init; }

    /// <summary>
    /// Number of failed operations in the current session.
    /// </summary>
    public int FailedOperations { get; init; }

    /// <summary>
    /// Success rate as a percentage.
    /// </summary>
    public double SuccessRatePercent { get; init; }

    /// <summary>
    /// Average operation duration.
    /// </summary>
    public TimeSpan AverageOperationDuration { get; init; }

    /// <summary>
    /// Total bytes moved in the current session.
    /// </summary>
    public long TotalBytesMoved { get; init; }

    /// <summary>
    /// Available disk space on the most commonly used target drive.
    /// </summary>
    public long AvailableSpaceBytes { get; init; }

    /// <summary>
    /// Timestamp when these statistics were collected.
    /// </summary>
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Recent operation history for trend analysis.
    /// </summary>
    public List<FileOperationRecord> RecentOperations { get; init; } = new();
}

/// <summary>
/// Types of file operations supported by the system.
/// </summary>
public enum FileOperationType
{
    /// <summary>
    /// Move operation (rename within same drive or copy+delete across drives).
    /// </summary>
    Move = 1,

    /// <summary>
    /// Copy operation (duplicate file to new location).
    /// </summary>
    Copy = 2,

    /// <summary>
    /// Delete operation (remove file from filesystem).
    /// </summary>
    Delete = 3,

    /// <summary>
    /// Directory creation operation.
    /// </summary>
    CreateDirectory = 4,

    /// <summary>
    /// Validation operation (pre-flight check).
    /// </summary>
    Validate = 5
}