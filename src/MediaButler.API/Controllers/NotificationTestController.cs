using Microsoft.AspNetCore.Mvc;
using MediaButler.API.Services;

namespace MediaButler.API.Controllers;

/// <summary>
/// Test controller for SignalR notifications.
/// Provides endpoints to test real-time notification functionality.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NotificationTestController : ControllerBase
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<NotificationTestController> _logger;

    public NotificationTestController(
        ISignalRNotificationService notificationService,
        ILogger<NotificationTestController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Test endpoint to send a job progress notification.
    /// </summary>
    [HttpPost("job-progress")]
    public async Task<IActionResult> TestJobProgress([FromQuery] string message = "Test notification", [FromQuery] int progress = 50)
    {
        try
        {
            await _notificationService.NotifyJobProgressAsync("Test", message, progress);
            _logger.LogInformation("Test job progress notification sent: {Message} ({Progress}%)", message, progress);
            return Ok(new { success = true, message = "Job progress notification sent", data = new { message, progress } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test job progress notification");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to send a file move notification.
    /// </summary>
    [HttpPost("file-move")]
    public async Task<IActionResult> TestFileMove([FromQuery] int fileId = 1, [FromQuery] string fileName = "test.mkv", [FromQuery] string status = "Moving")
    {
        try
        {
            await _notificationService.NotifyFileMoveAsync(fileId, fileName, status);
            _logger.LogInformation("Test file move notification sent: {FileName} - {Status}", fileName, status);
            return Ok(new { success = true, message = "File move notification sent", data = new { fileId, fileName, status } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test file move notification");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to send a system status notification.
    /// </summary>
    [HttpPost("system-status")]
    public async Task<IActionResult> TestSystemStatus([FromQuery] string component = "Scanner", [FromQuery] string status = "Active", [FromQuery] string message = "System is running normally")
    {
        try
        {
            await _notificationService.NotifySystemStatusAsync(component, status, message);
            _logger.LogInformation("Test system status notification sent: {Component} - {Status}", component, status);
            return Ok(new { success = true, message = "System status notification sent", data = new { component, status, message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test system status notification");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to send an error notification.
    /// </summary>
    [HttpPost("error")]
    public async Task<IActionResult> TestError([FromQuery] string errorType = "test_error", [FromQuery] string message = "This is a test error notification")
    {
        try
        {
            await _notificationService.NotifyErrorAsync(errorType, message, "Test error details");
            _logger.LogInformation("Test error notification sent: {ErrorType} - {Message}", errorType, message);
            return Ok(new { success = true, message = "Error notification sent", data = new { errorType, message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test error notification");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}