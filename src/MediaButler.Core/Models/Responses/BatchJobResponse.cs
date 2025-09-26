namespace MediaButler.Core.Models.Responses;

/// <summary>
/// Response model for batch file organization operations.
/// Provides comprehensive status information and progress tracking for background jobs.
/// </summary>
public class BatchJobResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for this batch job.
    /// Used to track progress and retrieve status updates.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the batch job.
    /// Values: "Queued", "Processing", "Completed", "Failed", "Cancelled"
    /// </summary>
    public required string Status { get; set; } = "Queued";

    /// <summary>
    /// Gets or sets when this batch job was queued for processing.
    /// </summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this batch job started processing.
    /// Null if not yet started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when this batch job completed (successfully or with errors).
    /// Null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the total number of files in this batch.
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Gets or sets the number of files that have been processed (successfully or with errors).
    /// </summary>
    public int ProcessedFiles { get; set; }

    /// <summary>
    /// Gets or sets the number of files that were successfully organized.
    /// </summary>
    public int SuccessfulFiles { get; set; }

    /// <summary>
    /// Gets or sets the number of files that failed to be organized.
    /// </summary>
    public int FailedFiles { get; set; }

    /// <summary>
    /// Gets or sets the percentage of completion (0-100).
    /// </summary>
    public int ProgressPercentage => TotalFiles > 0 ? (ProcessedFiles * 100) / TotalFiles : 0;

    /// <summary>
    /// Gets or sets additional metadata about the batch operation.
    /// Includes batch name, configuration settings, and other relevant information.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of error messages encountered during processing.
    /// Useful for debugging and user feedback.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated time remaining for completion.
    /// Calculated based on current progress and processing speed.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Gets or sets the average processing time per file.
    /// Useful for performance monitoring and optimization.
    /// </summary>
    public TimeSpan? AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets detailed results for individual files.
    /// Only populated when the job is completed or when requested explicitly.
    /// </summary>
    public List<FileProcessingResult>? DetailedResults { get; set; }
}

/// <summary>
/// Represents the processing result for a single file within a batch operation.
/// Provides detailed information about success/failure and timing.
/// </summary>
public class FileProcessingResult
{
    /// <summary>
    /// Gets or sets the SHA256 hash of the processed file.
    /// </summary>
    public required string FileHash { get; set; }

    /// <summary>
    /// Gets or sets the filename of the processed file.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets whether the file was successfully processed.
    /// </summary>
    public required bool Success { get; set; }

    /// <summary>
    /// Gets or sets the target path where the file was (or would be) moved.
    /// </summary>
    public required string TargetPath { get; set; }

    /// <summary>
    /// Gets or sets the actual path where the file was moved.
    /// May differ from TargetPath due to conflict resolution.
    /// </summary>
    public string? ActualPath { get; set; }

    /// <summary>
    /// Gets or sets the error message if processing failed.
    /// Null if the operation was successful.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets how long this individual file took to process.
    /// </summary>
    public TimeSpan? ProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets whether this was a dry run (no actual file movement).
    /// </summary>
    public bool IsDryRun { get; set; }

    /// <summary>
    /// Gets or sets when this file was processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional metadata specific to this file's processing.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}