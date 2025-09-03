using System;
using MediaButler.Core.Common;
using MediaButler.Core.Enums;

namespace MediaButler.Core.Entities;

/// <summary>
/// Represents a media file being tracked by the MediaButler system.
/// This entity maintains the complete lifecycle state of a file from discovery through organization.
/// Following "Simple Made Easy" principles, it focuses solely on file state management
/// without complecting file system operations or business logic.
/// </summary>
/// <remarks>
/// The TrackedFile entity serves as the single source of truth for file processing status,
/// ML classification results, and user decisions. It maintains a clear audit trail
/// through its BaseEntity properties and explicit state transitions.
/// </remarks>
public class TrackedFile : BaseEntity
{
    /// <summary>
    /// Gets or sets the SHA256 hash of the file content, serving as the primary key.
    /// This provides unique identification regardless of file location or name changes.
    /// </summary>
    /// <value>A SHA256 hash string uniquely identifying the file content.</value>
    public required string Hash { get; set; }

    /// <summary>
    /// Gets or sets the original filename including extension.
    /// This preserves the filename as discovered, which is crucial for ML classification.
    /// </summary>
    /// <value>The original filename with extension (e.g., "The.Office.S01E01.mkv").</value>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the full path where the file was originally discovered.
    /// This maintains a record of the file's source location for audit purposes.
    /// </summary>
    /// <value>The complete file system path where the file was found.</value>
    public required string OriginalPath { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// Used for ML classification hints and disk space management.
    /// </summary>
    /// <value>The file size in bytes.</value>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the current processing status of this file.
    /// Represents the file's position in the processing workflow.
    /// </summary>
    /// <value>A FileStatus enum value indicating current processing state.</value>
    public FileStatus Status { get; set; } = FileStatus.New;

    /// <summary>
    /// Gets or sets the category suggested by the ML classification system.
    /// This represents the system's best guess before user confirmation.
    /// </summary>
    /// <value>The ML-suggested category name, or null if not yet classified.</value>
    public string? SuggestedCategory { get; set; }

    /// <summary>
    /// Gets or sets the confidence score (0.0 to 1.0) from ML classification.
    /// Higher values indicate greater confidence in the suggested category.
    /// </summary>
    /// <value>A decimal value between 0.0 and 1.0 representing classification confidence.</value>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Gets or sets the final category confirmed by the user or system.
    /// This is the authoritative category used for file organization.
    /// </summary>
    /// <value>The confirmed category name, or null if not yet confirmed.</value>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the target path where the file will be moved.
    /// This is calculated based on the confirmed category and organization rules.
    /// </summary>
    /// <value>The full target path for file organization, or null if not yet determined.</value>
    public string? TargetPath { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when ML classification was completed.
    /// Used for performance tracking and audit purposes.
    /// </summary>
    /// <value>The UTC date and time of classification completion, or null if not classified.</value>
    public DateTime? ClassifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the file was successfully moved.
    /// Marks the completion of the organization workflow.
    /// </summary>
    /// <value>The UTC date and time of file movement, or null if not moved.</value>
    public DateTime? MovedAt { get; set; }

    /// <summary>
    /// Gets or sets the most recent error message encountered during processing.
    /// Used for debugging and user notification purposes.
    /// </summary>
    /// <value>The last error message, or null if no errors occurred.</value>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent error.
    /// Used in conjunction with LastError for troubleshooting.
    /// </summary>
    /// <value>The UTC date and time of the last error, or null if no errors occurred.</value>
    public DateTime? LastErrorAt { get; set; }

    /// <summary>
    /// Gets or sets the number of processing retry attempts.
    /// Used to implement retry limits and prevent infinite retry loops.
    /// </summary>
    /// <value>The count of retry attempts made for this file.</value>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Marks this file as classified by the ML system.
    /// Updates the classification timestamp and transitions to Classified status.
    /// </summary>
    /// <param name="suggestedCategory">The category suggested by ML classification.</param>
    /// <param name="confidence">The confidence score (0.0 to 1.0) for the classification.</param>
    /// <exception cref="ArgumentException">Thrown when confidence is not between 0.0 and 1.0.</exception>
    public void MarkAsClassified(string suggestedCategory, decimal confidence)
    {
        if (confidence < 0.0m || confidence > 1.0m)
            throw new ArgumentException("Confidence must be between 0.0 and 1.0", nameof(confidence));

        SuggestedCategory = suggestedCategory ?? throw new ArgumentNullException(nameof(suggestedCategory));
        Confidence = confidence;
        ClassifiedAt = DateTime.UtcNow;
        Status = FileStatus.Classified;
        MarkAsModified();
    }

    /// <summary>
    /// Confirms the category for this file, typically based on user input.
    /// Transitions the file to ReadyToMove status and sets the target path.
    /// </summary>
    /// <param name="confirmedCategory">The user-confirmed category.</param>
    /// <param name="targetPath">The calculated target path for file organization.</param>
    public void ConfirmCategory(string confirmedCategory, string targetPath)
    {
        Category = confirmedCategory ?? throw new ArgumentNullException(nameof(confirmedCategory));
        TargetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
        Status = FileStatus.ReadyToMove;
        MarkAsModified();
    }

    /// <summary>
    /// Marks this file as successfully moved to its target location.
    /// This represents the completion of the file organization workflow.
    /// </summary>
    /// <param name="finalPath">The actual path where the file was moved (may differ from TargetPath due to conflicts).</param>
    public void MarkAsMoved(string finalPath)
    {
        TargetPath = finalPath ?? throw new ArgumentNullException(nameof(finalPath));
        MovedAt = DateTime.UtcNow;
        Status = FileStatus.Moved;
        MarkAsModified();
    }

    /// <summary>
    /// Records an error that occurred during file processing.
    /// Updates error tracking fields and increments retry count.
    /// </summary>
    /// <param name="errorMessage">A description of the error that occurred.</param>
    /// <param name="shouldRetry">Whether this file should be queued for retry.</param>
    public void RecordError(string errorMessage, bool shouldRetry = true)
    {
        LastError = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        LastErrorAt = DateTime.UtcNow;
        RetryCount++;
        Status = shouldRetry ? FileStatus.Retry : FileStatus.Error;
        MarkAsModified();
    }
}