using System;
using MediaButler.Core.Common;
using MediaButler.Core.Enums;

namespace MediaButler.Core.Entities;

/// <summary>
/// Represents a log entry for file processing operations in the MediaButler system.
/// This entity provides a complete audit trail of all operations performed on tracked files,
/// following "Simple Made Easy" principles by maintaining a single responsibility: logging.
/// </summary>
/// <remarks>
/// ProcessingLog entries are immutable after creation and provide detailed context
/// for debugging, performance analysis, and audit requirements. Each log entry
/// is associated with a specific file and contains comprehensive diagnostic information.
/// </remarks>
public class ProcessingLog : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this log entry.
    /// Serves as the primary key for the ProcessingLog entity.
    /// </summary>
    /// <value>A unique GUID identifying this log entry.</value>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the SHA256 hash of the file this log entry relates to.
    /// This creates a foreign key relationship to the TrackedFile entity.
    /// </summary>
    /// <value>The file hash this log entry is associated with.</value>
    public required string FileHash { get; set; }

    /// <summary>
    /// Gets or sets the severity level of this log entry.
    /// Determines the importance and filtering behavior of this log entry.
    /// </summary>
    /// <value>A LogLevel enum value indicating the severity of this entry.</value>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the category or component that generated this log entry.
    /// Used for filtering and organizing log entries by functional area.
    /// </summary>
    /// <value>A string identifying the source category (e.g., "ML.Classification", "File.Movement").</value>
    public required string Category { get; set; }

    /// <summary>
    /// Gets or sets the primary log message.
    /// Contains the main description of the event or operation being logged.
    /// </summary>
    /// <value>A descriptive message about the logged event.</value>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets additional detailed information about the logged event.
    /// Can contain structured data, stack traces, or other contextual information.
    /// </summary>
    /// <value>Optional detailed information, or null if not applicable.</value>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets exception information if this log entry represents an error.
    /// Contains the full exception details including stack trace for debugging.
    /// </summary>
    /// <value>Exception information as a string, or null if no exception occurred.</value>
    public string? Exception { get; set; }

    /// <summary>
    /// Gets or sets the duration of the operation being logged, in milliseconds.
    /// Used for performance monitoring and optimization analysis.
    /// </summary>
    /// <value>The operation duration in milliseconds, or null if not applicable.</value>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Creates an informational log entry for a file operation.
    /// Factory method that ensures consistent log entry creation for info-level events.
    /// </summary>
    /// <param name="fileHash">The hash of the file this log entry relates to.</param>
    /// <param name="category">The functional category generating this log entry.</param>
    /// <param name="message">The main log message describing the event.</param>
    /// <param name="details">Optional additional details about the event.</param>
    /// <param name="durationMs">Optional operation duration in milliseconds.</param>
    /// <returns>A new ProcessingLog instance with Information level.</returns>
    public static ProcessingLog Info(string fileHash, string category, string message, 
        string? details = null, long? durationMs = null)
    {
        return new ProcessingLog
        {
            FileHash = fileHash ?? throw new ArgumentNullException(nameof(fileHash)),
            Level = LogLevel.Information,
            Category = category ?? throw new ArgumentNullException(nameof(category)),
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Details = details,
            DurationMs = durationMs
        };
    }

    /// <summary>
    /// Creates a warning log entry for a file operation.
    /// Factory method that ensures consistent log entry creation for warning-level events.
    /// </summary>
    /// <param name="fileHash">The hash of the file this log entry relates to.</param>
    /// <param name="category">The functional category generating this log entry.</param>
    /// <param name="message">The main log message describing the warning.</param>
    /// <param name="details">Optional additional details about the warning condition.</param>
    /// <param name="durationMs">Optional operation duration in milliseconds.</param>
    /// <returns>A new ProcessingLog instance with Warning level.</returns>
    public static ProcessingLog Warning(string fileHash, string category, string message, 
        string? details = null, long? durationMs = null)
    {
        return new ProcessingLog
        {
            FileHash = fileHash ?? throw new ArgumentNullException(nameof(fileHash)),
            Level = LogLevel.Warning,
            Category = category ?? throw new ArgumentNullException(nameof(category)),
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Details = details,
            DurationMs = durationMs
        };
    }

    /// <summary>
    /// Creates an error log entry for a file operation.
    /// Factory method that ensures consistent log entry creation for error-level events.
    /// </summary>
    /// <param name="fileHash">The hash of the file this log entry relates to.</param>
    /// <param name="category">The functional category generating this log entry.</param>
    /// <param name="message">The main log message describing the error.</param>
    /// <param name="exception">Optional exception information for detailed error context.</param>
    /// <param name="details">Optional additional details about the error condition.</param>
    /// <param name="durationMs">Optional operation duration in milliseconds before failure.</param>
    /// <returns>A new ProcessingLog instance with Error level.</returns>
    public static ProcessingLog Error(string fileHash, string category, string message, 
        Exception? exception = null, string? details = null, long? durationMs = null)
    {
        return new ProcessingLog
        {
            FileHash = fileHash ?? throw new ArgumentNullException(nameof(fileHash)),
            Level = LogLevel.Error,
            Category = category ?? throw new ArgumentNullException(nameof(category)),
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Exception = exception?.ToString(),
            Details = details,
            DurationMs = durationMs
        };
    }
}