using Microsoft.AspNetCore.Mvc;
using MediaButler.API.Services;
using Microsoft.AspNetCore.SignalR;
using MediaButler.API.Hubs;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for receiving notifications from Batch worker and forwarding to SignalR hubs.
/// This enables the Batch worker to send real-time updates to web clients.
/// </summary>
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IHubContext<FileProcessingHub> _fileProcessingHub;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IHubContext<NotificationHub> notificationHub,
        IHubContext<FileProcessingHub> fileProcessingHub,
        ILogger<NotificationsController> logger)
    {
        _notificationHub = notificationHub;
        _fileProcessingHub = fileProcessingHub;
        _logger = logger;
    }

    /// <summary>
    /// Receives batch notifications from Hangfire worker and forwards to SignalR clients.
    /// </summary>
    /// <param name="notifications">Array of job notifications from Batch worker</param>
    /// <returns>200 OK if notifications were processed successfully</returns>
    [HttpPost("batch")]
    public async Task<IActionResult> ReceiveBatchNotifications([FromBody] BatchNotification[] notifications)
    {
        if (notifications == null || notifications.Length == 0)
        {
            return BadRequest("No notifications provided");
        }

        _logger.LogDebug("Received {Count} notifications from Batch worker", notifications.Length);

        foreach (var notification in notifications)
        {
            await RouteNotificationAsync(notification);
        }

        return Ok(new { processed = notifications.Length });
    }

    private async Task RouteNotificationAsync(BatchNotification notification)
    {
        try
        {
            // Route based on event type
            switch (notification.EventType)
            {
                case "batch.started":
                    await _fileProcessingHub.Clients.All.SendAsync("BatchStarted", new
                    {
                        notification.JobId,
                        notification.JobType,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                case "batch.progress":
                    await _fileProcessingHub.Clients.All.SendAsync("BatchProgress", new
                    {
                        notification.JobId,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                case "batch.completed":
                    await _fileProcessingHub.Clients.All.SendAsync("BatchCompleted", new
                    {
                        notification.JobId,
                        notification.JobType,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                case "batch.failed":
                    await _fileProcessingHub.Clients.All.SendAsync("BatchFailed", new
                    {
                        notification.JobId,
                        notification.JobType,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });

                    // Also send to general notification hub for errors
                    await _notificationHub.Clients.All.SendAsync("Error", new
                    {
                        Title = "Batch Job Failed",
                        notification.Message,
                        JobId = notification.JobId,
                        notification.Timestamp
                    });
                    break;

                case "scan.started":
                case "scan.found":
                case "scan.completed":
                    // File discovery notifications
                    await _notificationHub.Clients.All.SendAsync(notification.EventType.Replace(".", ""), new
                    {
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                case "training.started":
                case "training.progress":
                case "training.completed":
                case "training.failed":
                    // ML training notifications
                    await _notificationHub.Clients.All.SendAsync(notification.EventType.Replace(".", ""), new
                    {
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                default:
                    _logger.LogWarning("Unknown notification event type: {EventType}", notification.EventType);
                    break;
            }

            _logger.LogTrace("Routed notification: {EventType} for job {JobId}",
                notification.EventType, notification.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing notification: {EventType}", notification.EventType);
        }
    }
}

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
