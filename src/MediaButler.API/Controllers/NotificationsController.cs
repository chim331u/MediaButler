using Microsoft.AspNetCore.Mvc;
using MediaButler.API.Modules.RealTime.Services;
using MediaButler.API.Modules.RealTime.Models;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for receiving notifications from Batch worker and forwarding to SignalR hubs.
/// This enables the Batch worker to send real-time updates to web clients.
/// </summary>
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IRealTimeService _realTimeService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IRealTimeService realTimeService,
        ILogger<NotificationsController> logger)
    {
        _realTimeService = realTimeService;
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
            await _realTimeService.DispatchBatchNotificationAsync(notification);
        }

        return Ok(new { processed = notifications.Length });
    }
}
