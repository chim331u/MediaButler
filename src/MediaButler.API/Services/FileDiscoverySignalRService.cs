using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediaButler.Services.Background;
using MediaButler.API.Services;

namespace MediaButler.API.Services;

/// <summary>
/// Hosted service that integrates file discovery events with SignalR notifications.
/// Listens for file discovery events and sends real-time notifications to connected clients.
/// </summary>
public class FileDiscoverySignalRService : IHostedService
{
    private readonly IFileDiscoveryService _fileDiscoveryService;
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<FileDiscoverySignalRService> _logger;

    public FileDiscoverySignalRService(
        IFileDiscoveryService fileDiscoveryService,
        ISignalRNotificationService notificationService,
        ILogger<FileDiscoverySignalRService> logger)
    {
        _fileDiscoveryService = fileDiscoveryService ?? throw new ArgumentNullException(nameof(fileDiscoveryService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting File Discovery SignalR Service");

        // Subscribe to file discovery events
        _fileDiscoveryService.FileDiscovered += OnFileDiscovered;
        _fileDiscoveryService.DiscoveryError += OnDiscoveryError;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping File Discovery SignalR Service");

        // Unsubscribe from events
        _fileDiscoveryService.FileDiscovered -= OnFileDiscovered;
        _fileDiscoveryService.DiscoveryError -= OnDiscoveryError;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles file discovered events and sends SignalR notifications.
    /// </summary>
    private async void OnFileDiscovered(object? sender, FileDiscoveredEventArgs e)
    {
        try
        {
            var fileName = Path.GetFileName(e.FilePath);

            // Send dedicated file discovery notification to trigger UI refresh
            await _notificationService.NotifyFileDiscoveryAsync(fileName, e.FilePath, e.DiscoveredAt);

            // Also send system status notification for general awareness
            await _notificationService.NotifySystemStatusAsync(
                "FileDiscovery",
                "FileFound",
                $"New file discovered: {fileName}");

            _logger.LogDebug("Sent SignalR notifications for discovered file: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification for discovered file: {FilePath}", e.FilePath);
        }
    }

    /// <summary>
    /// Handles discovery error events and sends SignalR error notifications.
    /// </summary>
    private async void OnDiscoveryError(object? sender, FileDiscoveryErrorEventArgs e)
    {
        try
        {
            await _notificationService.NotifyErrorAsync(
                "file_discovery_error",
                e.ErrorMessage,
                e.FilePath);

            _logger.LogDebug("Sent SignalR error notification for discovery error: {ErrorMessage}", e.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR error notification: {ErrorMessage}", e.ErrorMessage);
        }
    }
}