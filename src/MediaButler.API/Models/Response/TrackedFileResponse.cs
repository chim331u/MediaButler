using MediaButler.Core.Enums;

namespace MediaButler.API.Models.Response;

/// <summary>
/// Data transfer object representing a tracked file for API responses.
/// Maps domain entity properties to API-appropriate format following "Simple Made Easy" principles.
/// Separates API concerns from domain entity structure.
/// </summary>
/// <remarks>
/// This DTO provides a stable API contract that can evolve independently of the domain model.
/// It excludes sensitive internal properties and formats data for client consumption.
/// All timestamps are returned in UTC format for consistency.
/// </remarks>
public class TrackedFileResponse
{
    /// <summary>
    /// Gets or sets the unique SHA256 hash identifier for this file.
    /// This serves as the primary key and ensures uniqueness regardless of file location.
    /// </summary>
    /// <value>A SHA256 hash string uniquely identifying the file content.</value>
    /// <example>abc123def456789...</example>
    public required string Hash { get; set; }

    /// <summary>
    /// Gets or sets the original filename including extension.
    /// Preserves the filename as discovered for reference and ML classification context.
    /// </summary>
    /// <value>The original filename with extension.</value>
    /// <example>The.Office.S01E01.mkv</example>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the original discovery path of the file.
    /// Provides audit trail of where the file was found.
    /// </summary>
    /// <value>The complete file system path where the file was originally located.</value>
    /// <example>/media/downloads/The.Office.S01E01.mkv</example>
    public required string OriginalPath { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// Used for display purposes and client-side validation.
    /// </summary>
    /// <value>The file size in bytes.</value>
    /// <example>524288000</example>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the human-readable file size string.
    /// Provides formatted file size for display (e.g., "500 MB").
    /// </summary>
    /// <value>A human-readable representation of the file size.</value>
    /// <example>500 MB</example>
    public string FormattedFileSize { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current processing status of this file.
    /// Indicates the file's position in the processing workflow.
    /// </summary>
    /// <value>A FileStatus enum value representing the current processing state.</value>
    /// <example>Classified</example>
    public FileStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the human-readable status description.
    /// Provides user-friendly status text for display purposes.
    /// </summary>
    /// <value>A descriptive status string for client display.</value>
    /// <example>Ready for review</example>
    public string StatusDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category suggested by the ML classification system.
    /// Represents the system's best guess before user confirmation.
    /// </summary>
    /// <value>The ML-suggested category name, or null if not yet classified.</value>
    /// <example>THE OFFICE</example>
    public string? SuggestedCategory { get; set; }

    /// <summary>
    /// Gets or sets the ML classification confidence score.
    /// Expressed as a percentage (0-100) for better user understanding.
    /// </summary>
    /// <value>A percentage value representing classification confidence.</value>
    /// <example>85.5</example>
    public decimal? ConfidencePercentage { get; set; }

    /// <summary>
    /// Gets or sets the confidence level description.
    /// Provides user-friendly confidence assessment (High, Medium, Low).
    /// </summary>
    /// <value>A descriptive confidence level string.</value>
    /// <example>High</example>
    public string? ConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets the final confirmed category.
    /// This is the authoritative category used for file organization.
    /// </summary>
    /// <value>The confirmed category name, or null if not yet confirmed.</value>
    /// <example>THE OFFICE</example>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the target path for file organization.
    /// Shows where the file will be moved when organization occurs.
    /// </summary>
    /// <value>The full target path for file organization, or null if not yet determined.</value>
    /// <example>/media/library/THE OFFICE/The.Office.S01E01.mkv</example>
    public string? TargetPath { get; set; }

    /// <summary>
    /// Gets or sets when this file was first discovered.
    /// Provides audit trail timing information.
    /// </summary>
    /// <value>The UTC date and time when the file was first tracked.</value>
    /// <example>2024-01-15T10:30:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this file was last updated.
    /// Tracks the most recent modification to the file's processing state.
    /// </summary>
    /// <value>The UTC date and time of the last update.</value>
    /// <example>2024-01-15T10:45:30Z</example>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when ML classification was completed.
    /// Used for performance tracking and processing timeline display.
    /// </summary>
    /// <value>The UTC date and time of classification completion, or null if not classified.</value>
    /// <example>2024-01-15T10:35:15Z</example>
    public DateTime? ClassifiedAt { get; set; }

    /// <summary>
    /// Gets or sets when the file was successfully moved.
    /// Marks the completion of the organization workflow.
    /// </summary>
    /// <value>The UTC date and time of file movement, or null if not moved.</value>
    /// <example>2024-01-15T10:50:00Z</example>
    public DateTime? MovedAt { get; set; }

    /// <summary>
    /// Gets or sets the most recent error message if any occurred.
    /// Used for debugging and user notification purposes.
    /// Null if no errors have occurred.
    /// </summary>
    /// <value>The last error message, or null if no errors occurred.</value>
    /// <example>Unable to access target directory</example>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets when the most recent error occurred.
    /// Used in conjunction with LastError for troubleshooting.
    /// </summary>
    /// <value>The UTC date and time of the last error, or null if no errors occurred.</value>
    /// <example>2024-01-15T10:40:00Z</example>
    public DateTime? LastErrorAt { get; set; }

    /// <summary>
    /// Gets or sets the number of processing retry attempts.
    /// Helps users understand processing effort and potential issues.
    /// </summary>
    /// <value>The count of retry attempts made for this file.</value>
    /// <example>2</example>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets whether this file requires user attention.
    /// Computed property based on status and error conditions.
    /// </summary>
    /// <value>True if the file needs user review or action.</value>
    /// <example>true</example>
    public bool RequiresAttention { get; set; }

    /// <summary>
    /// Gets or sets the processing duration in milliseconds.
    /// Shows how long the file took to process from discovery to classification.
    /// Null if processing is not complete.
    /// </summary>
    /// <value>Processing duration in milliseconds, or null if not applicable.</value>
    /// <example>1250</example>
    public long? ProcessingDurationMs { get; set; }

    /// <summary>
    /// Formats the file size into a human-readable string.
    /// Converts bytes into appropriate units (B, KB, MB, GB, TB).
    /// </summary>
    /// <param name="bytes">The file size in bytes.</param>
    /// <returns>A formatted string representing the file size.</returns>
    /// <example>FormatFileSize(524288000) returns "500.0 MB"</example>
    public static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 B";

        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }

    /// <summary>
    /// Gets the confidence level description based on percentage.
    /// Categorizes confidence scores into user-friendly levels.
    /// </summary>
    /// <param name="confidencePercentage">The confidence percentage (0-100).</param>
    /// <returns>A string describing the confidence level.</returns>
    /// <example>GetConfidenceLevel(85.5) returns "High"</example>
    public static string GetConfidenceLevel(decimal? confidencePercentage)
    {
        if (!confidencePercentage.HasValue) return "Unknown";

        return confidencePercentage.Value switch
        {
            >= 80m => "High",
            >= 60m => "Medium",
            >= 30m => "Low",
            _ => "Very Low"
        };
    }

    /// <summary>
    /// Gets the user-friendly status description.
    /// Converts FileStatus enum values to readable descriptions.
    /// </summary>
    /// <param name="status">The FileStatus enum value.</param>
    /// <returns>A human-readable status description.</returns>
    /// <example>GetStatusDescription(FileStatus.Classified) returns "Ready for review"</example>
    public static string GetStatusDescription(FileStatus status)
    {
        return status switch
        {
            FileStatus.New => "Newly discovered",
            FileStatus.Processing => "Processing in progress",
            FileStatus.Classified => "Ready for review",
            FileStatus.ReadyToMove => "Approved for organization",
            FileStatus.Moving => "Organizing file",
            FileStatus.Moved => "Successfully organized",
            FileStatus.Error => "Error occurred",
            FileStatus.Retry => "Queued for retry",
            FileStatus.Ignored => "Ignored by user",
            _ => "Unknown status"
        };
    }
}