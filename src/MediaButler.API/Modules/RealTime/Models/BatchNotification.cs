namespace MediaButler.API.Modules.RealTime.Models;

/// <summary>
/// Model for batch notifications received from Hangfire worker.
/// Matches the JobNotification class in MediaButler.Batch.
/// </summary>
public class BatchNotification
{
    /// <summary>
    /// Unique job identifier from Hangfire.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Type of job (e.g., "batch.file.processing", "model.training").
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Event type (e.g., "batch.started", "batch.progress", "batch.completed", "batch.failed").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional data payload for the notification.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Timestamp when notification was created.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
