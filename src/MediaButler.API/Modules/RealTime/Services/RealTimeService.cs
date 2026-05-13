using MediaButler.API.Modules.RealTime.Models;

namespace MediaButler.API.Modules.RealTime.Services;

public class RealTimeService : IRealTimeService
{
    private readonly MediaButler.API.Modules.RealTime.SSE.SseConnectionManager _sseManager;
    private readonly ILogger<RealTimeService> _logger;

    public RealTimeService(
        MediaButler.API.Modules.RealTime.SSE.SseConnectionManager sseManager,
        ILogger<RealTimeService> logger)
    {
        _sseManager = sseManager;
        _logger = logger;
    }

    // --- File Operations (Legacy ISignalRNotificationService) ---

    public async Task NotifyFileMoveAsync(int fileId, string fileName, string status)
    {
        try
        {
            await _sseManager.BroadcastAsync("MoveFileNotification", new { fileId, fileName, status });
            _logger.LogDebug("Sent file move notification: {FileName} - {Status}", fileName, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send file move notification for {FileName}", fileName);
        }
    }

    public async Task NotifyFileDiscoveryAsync(string fileName, string filePath, DateTime discoveredAt)
    {
        try
        {
            await _sseManager.BroadcastAsync("FileDiscoveryNotification", new { fileName, filePath, discoveredAt });
            _logger.LogDebug("Sent file discovery notification: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send file discovery notification for {FileName}", fileName);
        }
    }

    public async Task NotifyFileClassificationAsync(int fileId, string fileName, string suggestedCategory, decimal confidence)
    {
        try
        {
            await _sseManager.BroadcastAsync("ClassificationNotification", new { fileId, fileName, suggestedCategory, confidence });
            _logger.LogDebug("Sent classification notification: {FileName} -> {Category} ({Confidence:P})",
                fileName, suggestedCategory, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send classification notification for {FileName}", fileName);
        }
    }

    // --- System & Jobs (Legacy ISignalRNotificationService) ---

    public async Task NotifyJobProgressAsync(string jobType, string message, int progress = 0)
    {
        try
        {
            await _sseManager.BroadcastAsync("JobProgressNotification", new { jobType, message, progress });
            _logger.LogDebug("Sent job progress notification: {JobType} - {Message} ({Progress}%)",
                jobType, message, progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send job progress notification for {JobType}", jobType);
        }
    }

    public async Task NotifySystemStatusAsync(string component, string status, string message)
    {
        try
        {
            await _sseManager.BroadcastAsync("SystemStatusNotification", new { component, status, message });
            _logger.LogDebug("Sent system status notification: {Component} - {Status}", component, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send system status notification for {Component}", component);
        }
    }

    public async Task NotifyErrorAsync(string errorType, string message, string? details = null)
    {
        try
        {
            await _sseManager.BroadcastAsync("ErrorNotification", new { errorType, message, details });
            _logger.LogDebug("Sent error notification: {ErrorType} - {Message}", errorType, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error notification: {ErrorType}", errorType);
        }
    }

    // --- Batch Dispatching (Legacy NotificationsController) ---

    public async Task DispatchBatchNotificationAsync(BatchNotification notification)
    {
        try
        {
            _logger.LogTrace("Dispatching notification: {EventType} for job {JobId}", notification.EventType, notification.JobId);

            switch (notification.EventType)
            {
                case "batch.started":
                    await _sseManager.BroadcastAsync("BatchStarted", new
                    {
                        notification.JobId,
                        notification.JobType,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                case "batch.progress":
                    await _sseManager.BroadcastAsync("BatchProgress", new
                    {
                        notification.JobId,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                case "batch.completed":
                    await _sseManager.BroadcastAsync("BatchCompleted", new
                    {
                        notification.JobId,
                        notification.JobType,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });
                    break;

                case "batch.failed":
                    await _sseManager.BroadcastAsync("BatchFailed", new
                    {
                        notification.JobId,
                        notification.JobType,
                        notification.Message,
                        notification.Data,
                        notification.Timestamp
                    });

                    // Also send to general notification hub for errors (Legacy behavior)
                    await _sseManager.BroadcastAsync("Error", new
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
                    await _sseManager.BroadcastAsync(notification.EventType.Replace(".", ""), new
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
                    await _sseManager.BroadcastAsync(notification.EventType.Replace(".", ""), new
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching batch notification: {EventType}", notification.EventType);
        }
    }
}
