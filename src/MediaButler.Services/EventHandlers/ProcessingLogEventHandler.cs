using MediaButler.Core.Entities;
using MediaButler.Core.Events;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.EventHandlers;

/// <summary>
/// Event handler that creates audit trail entries in ProcessingLog for all file processing events.
/// Follows "Simple Made Easy" principles by focusing solely on audit logging without business logic.
/// </summary>
public class ProcessingLogEventHandler :
    INotificationHandler<FileDiscoveredEvent>,
    INotificationHandler<FileClassifiedEvent>,
    INotificationHandler<FileCategoryConfirmedEvent>,
    INotificationHandler<FileMovedEvent>,
    INotificationHandler<FileProcessingErrorEvent>,
    INotificationHandler<FileRetryScheduledEvent>,
    INotificationHandler<FileStatusChangedEvent>
{
    private readonly IProcessingLogRepository _processingLogRepository;
    private readonly ILogger<ProcessingLogEventHandler> _logger;

    private const string CategoryFileDiscovery = "File.Discovery";
    private const string CategoryMLClassification = "ML.Classification";
    private const string CategoryUserConfirmation = "User.Confirmation";
    private const string CategoryFileMovement = "File.Movement";
    private const string CategoryErrorHandling = "Error.Handling";
    private const string CategoryStatusChange = "Status.Change";

    public ProcessingLogEventHandler(
        IProcessingLogRepository processingLogRepository,
        ILogger<ProcessingLogEventHandler> logger)
    {
        _processingLogRepository = processingLogRepository ?? throw new ArgumentNullException(nameof(processingLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles file discovery events by creating audit log entries.
    /// </summary>
    public async Task Handle(FileDiscoveredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var logEntry = ProcessingLog.Info(
                notification.FileHash,
                CategoryFileDiscovery,
                $"File discovered: {notification.FileName}",
                $"Original path: {notification.OriginalPath}, Size: {notification.FileSize} bytes"
            );

            var result = await _processingLogRepository.AddAsync(logEntry, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to log file discovery event for {FileHash}: {Error}", 
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file discovery event for {FileHash}", notification.FileHash);
        }
    }

    /// <summary>
    /// Handles file classification events by creating audit log entries.
    /// </summary>
    public async Task Handle(FileClassifiedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var logEntry = ProcessingLog.Info(
                notification.FileHash,
                CategoryMLClassification,
                $"ML classification completed: {notification.SuggestedCategory}",
                $"Confidence: {notification.Confidence:F3}, Classified at: {notification.ClassifiedAt:yyyy-MM-dd HH:mm:ss} UTC"
            );

            var result = await _processingLogRepository.AddAsync(logEntry, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to log file classification event for {FileHash}: {Error}", 
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file classification event for {FileHash}", notification.FileHash);
        }
    }

    /// <summary>
    /// Handles category confirmation events by creating audit log entries.
    /// </summary>
    public async Task Handle(FileCategoryConfirmedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var details = $"Target path: {notification.TargetPath}";
            if (!string.IsNullOrEmpty(notification.PreviousSuggestedCategory))
            {
                details += $", Previous ML suggestion: {notification.PreviousSuggestedCategory}";
            }

            var logEntry = ProcessingLog.Info(
                notification.FileHash,
                CategoryUserConfirmation,
                $"Category confirmed: {notification.ConfirmedCategory}",
                details
            );

            var result = await _processingLogRepository.AddAsync(logEntry, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to log category confirmation event for {FileHash}: {Error}", 
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling category confirmation event for {FileHash}", notification.FileHash);
        }
    }

    /// <summary>
    /// Handles file movement events by creating audit log entries.
    /// </summary>
    public async Task Handle(FileMovedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var logEntry = ProcessingLog.Info(
                notification.FileHash,
                CategoryFileMovement,
                $"File moved successfully to category: {notification.Category}",
                $"From: {notification.OriginalPath} To: {notification.FinalPath}, Moved at: {notification.MovedAt:yyyy-MM-dd HH:mm:ss} UTC"
            );

            var result = await _processingLogRepository.AddAsync(logEntry, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to log file movement event for {FileHash}: {Error}", 
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file movement event for {FileHash}", notification.FileHash);
        }
    }

    /// <summary>
    /// Handles processing error events by creating audit log entries.
    /// </summary>
    public async Task Handle(FileProcessingErrorEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var details = $"Previous status: {notification.PreviousStatus}, New status: {notification.NewStatus}, Retry count: {notification.RetryCount}";

            var logEntry = ProcessingLog.Error(
                notification.FileHash,
                CategoryErrorHandling,
                $"Processing error: {notification.ErrorMessage}",
                !string.IsNullOrEmpty(notification.Exception) ? new Exception(notification.Exception) : null,
                details
            );

            var result = await _processingLogRepository.AddAsync(logEntry, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to log processing error event for {FileHash}: {Error}", 
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling processing error event for {FileHash}", notification.FileHash);
        }
    }

    /// <summary>
    /// Handles retry scheduling events by creating audit log entries.
    /// </summary>
    public async Task Handle(FileRetryScheduledEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var logEntry = ProcessingLog.Warning(
                notification.FileHash,
                CategoryErrorHandling,
                $"File scheduled for retry: {notification.Reason}",
                $"Retry attempt: {notification.RetryAttempt}/{notification.MaxRetries}"
            );

            var result = await _processingLogRepository.AddAsync(logEntry, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to log retry scheduling event for {FileHash}: {Error}", 
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling retry scheduling event for {FileHash}", notification.FileHash);
        }
    }

    /// <summary>
    /// Handles status change events by creating audit log entries.
    /// </summary>
    public async Task Handle(FileStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var details = !string.IsNullOrEmpty(notification.Reason) 
                ? $"Reason: {notification.Reason}" 
                : null;

            var logEntry = ProcessingLog.Info(
                notification.FileHash,
                CategoryStatusChange,
                $"Status changed: {notification.PreviousStatus} → {notification.NewStatus}",
                details
            );

            var result = await _processingLogRepository.AddAsync(logEntry, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to log status change event for {FileHash}: {Error}", 
                    notification.FileHash, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling status change event for {FileHash}", notification.FileHash);
        }
    }
}