using MediaButler.Core.Events;
using MediaButler.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.EventHandlers;

/// <summary>
/// Event handlers that integrate file processing events with the notification system.
/// Following "Simple Made Easy" principles, these handlers compose existing domain events 
/// with notification concerns without complecting business logic and notifications.
/// </summary>
/// <remarks>
/// These handlers demonstrate the "Simple Made Easy" principle of composition over complection:
/// - File processing events remain pure domain concerns
/// - Notification logic is separate and composable
/// - No coupling between business operations and notification delivery
/// - Easy to extend with additional notification channels (email, SignalR, etc.)
/// </remarks>
public class FileDiscoveredNotificationHandler : INotificationHandler<FileDiscoveredEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<FileDiscoveredNotificationHandler> _logger;

    public FileDiscoveredNotificationHandler(
        INotificationService notificationService,
        ILogger<FileDiscoveredNotificationHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles file discovered events by notifying about the start of file processing.
    /// </summary>
    public async Task Handle(FileDiscoveredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var operation = $"Processing new file: {notification.FileName} ({notification.FileSize} bytes)";
            var result = await _notificationService.NotifyOperationStartedAsync(
                notification.FileHash, 
                operation, 
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to send file discovered notification for {FileHash}: {Error}",
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling file discovered notification for {FileHash}",
                notification.FileHash);
        }
    }
}

public class FileClassifiedNotificationHandler : INotificationHandler<FileClassifiedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<FileClassifiedNotificationHandler> _logger;

    public FileClassifiedNotificationHandler(
        INotificationService notificationService,
        ILogger<FileClassifiedNotificationHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles file classification events by notifying about classification progress.
    /// </summary>
    public async Task Handle(FileClassifiedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var progress = $"Classified as '{notification.SuggestedCategory}' with {notification.Confidence:P1} confidence";
            var result = await _notificationService.NotifyOperationProgressAsync(
                notification.FileHash,
                progress,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to send file classification notification for {FileHash}: {Error}",
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling file classification notification for {FileHash}",
                notification.FileHash);
        }
    }
}

public class FileCategoryConfirmedNotificationHandler : INotificationHandler<FileCategoryConfirmedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<FileCategoryConfirmedNotificationHandler> _logger;

    public FileCategoryConfirmedNotificationHandler(
        INotificationService notificationService,
        ILogger<FileCategoryConfirmedNotificationHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles category confirmation events by notifying about confirmed categorization.
    /// </summary>
    public async Task Handle(FileCategoryConfirmedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var progress = $"Category confirmed as '{notification.ConfirmedCategory}', ready to move to {notification.TargetPath}";
            var result = await _notificationService.NotifyOperationProgressAsync(
                notification.FileHash,
                progress,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to send category confirmation notification for {FileHash}: {Error}",
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling category confirmation notification for {FileHash}",
                notification.FileHash);
        }
    }
}

public class FileMovedNotificationHandler : INotificationHandler<FileMovedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<FileMovedNotificationHandler> _logger;

    public FileMovedNotificationHandler(
        INotificationService notificationService,
        ILogger<FileMovedNotificationHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles file moved events by notifying about successful file operation completion.
    /// </summary>
    public async Task Handle(FileMovedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var result = $"File successfully moved to {notification.FinalPath} in category '{notification.Category}'";
            var notificationResult = await _notificationService.NotifyOperationCompletedAsync(
                notification.FileHash,
                result,
                cancellationToken);

            if (!notificationResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to send file moved notification for {FileHash}: {Error}",
                    notification.FileHash, notificationResult.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling file moved notification for {FileHash}",
                notification.FileHash);
        }
    }
}

public class FileProcessingErrorNotificationHandler : INotificationHandler<FileProcessingErrorEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<FileProcessingErrorNotificationHandler> _logger;

    public FileProcessingErrorNotificationHandler(
        INotificationService notificationService,
        ILogger<FileProcessingErrorNotificationHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles file processing error events by notifying about operation failures.
    /// </summary>
    public async Task Handle(FileProcessingErrorEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var canRetry = notification.RetryCount < 3; // Assume max 3 retries
            var result = await _notificationService.NotifyOperationFailedAsync(
                notification.FileHash,
                notification.ErrorMessage,
                canRetry,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to send error notification for {FileHash}: {Error}",
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling file processing error notification for {FileHash}",
                notification.FileHash);
        }
    }
}

public class FileRetryScheduledNotificationHandler : INotificationHandler<FileRetryScheduledEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<FileRetryScheduledNotificationHandler> _logger;

    public FileRetryScheduledNotificationHandler(
        INotificationService notificationService,
        ILogger<FileRetryScheduledNotificationHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles retry scheduled events by notifying about retry attempts.
    /// </summary>
    public async Task Handle(FileRetryScheduledEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var progress = $"Retry scheduled: attempt {notification.RetryAttempt}/{notification.MaxRetries} - {notification.Reason}";
            var result = await _notificationService.NotifyOperationProgressAsync(
                notification.FileHash,
                progress,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to send retry scheduled notification for {FileHash}: {Error}",
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling retry scheduled notification for {FileHash}",
                notification.FileHash);
        }
    }
}

public class FileStatusChangedNotificationHandler : INotificationHandler<FileStatusChangedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<FileStatusChangedNotificationHandler> _logger;

    public FileStatusChangedNotificationHandler(
        INotificationService notificationService,
        ILogger<FileStatusChangedNotificationHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles file status change events by notifying about status transitions.
    /// </summary>
    public async Task Handle(FileStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var progress = $"Status changed from {notification.PreviousStatus} to {notification.NewStatus}";
            if (!string.IsNullOrWhiteSpace(notification.Reason))
            {
                progress += $" - {notification.Reason}";
            }

            var result = await _notificationService.NotifyOperationProgressAsync(
                notification.FileHash,
                progress,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to send status change notification for {FileHash}: {Error}",
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling status change notification for {FileHash}",
                notification.FileHash);
        }
    }
}