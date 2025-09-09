using MediaButler.Core.Events;
using MediaButler.Core.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.EventHandlers;

/// <summary>
/// Event handler that automatically collects metrics from domain events.
/// Follows "Simple Made Easy" principles by separating metrics collection from business logic.
/// Integrates seamlessly with the existing event-driven architecture.
/// </summary>
/// <remarks>
/// This handler responds to all file processing events and automatically:
/// - Records processing events for queue metrics
/// - Tracks classification results for ML performance
/// - Monitors error conditions for alerting
/// - Measures performance data for optimization
/// 
/// No complecting of concerns - pure event-to-metrics translation.
/// </remarks>
public class MetricsEventHandler : 
    INotificationHandler<FileDiscoveredEvent>,
    INotificationHandler<FileClassifiedEvent>,
    INotificationHandler<FileCategoryConfirmedEvent>,
    INotificationHandler<FileMovedEvent>,
    INotificationHandler<FileProcessingErrorEvent>
{
    private readonly IMetricsCollectionService _metricsService;
    private readonly ILogger<MetricsEventHandler> _logger;

    public MetricsEventHandler(
        IMetricsCollectionService metricsService,
        ILogger<MetricsEventHandler> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(FileDiscoveredEvent notification, CancellationToken cancellationToken)
    {
        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.FileDiscovered, 
            notification.FileHash);

        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.QueuedForClassification,
            notification.FileHash);

        _logger.LogDebug("Recorded file discovery metrics for {FileHash}", notification.FileHash);
    }

    public async Task Handle(FileClassifiedEvent notification, CancellationToken cancellationToken)
    {
        // Record processing event
        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.ClassificationCompleted,
            notification.FileHash,
            notification.SuggestedCategory);

        // Record classification result for ML metrics
        await _metricsService.RecordClassificationResultAsync(
            notification.FileHash,
            notification.SuggestedCategory,
            notification.Confidence);

        // Record processing event for queue tracking
        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.AwaitingConfirmation,
            notification.FileHash,
            notification.SuggestedCategory);

        _logger.LogDebug("Recorded classification metrics for {FileHash}: {Category} (confidence: {Confidence:F2})", 
            notification.FileHash, notification.SuggestedCategory, notification.Confidence);
    }

    public async Task Handle(FileCategoryConfirmedEvent notification, CancellationToken cancellationToken)
    {
        // Update classification result with user decision
        await _metricsService.RecordClassificationResultAsync(
            notification.FileHash,
            notification.ConfirmedCategory,
            0, // Confidence not applicable for user confirmations
            wasAccepted: true); // User confirmed the category

        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.CategoryConfirmed,
            notification.FileHash,
            notification.ConfirmedCategory);

        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.QueuedForMove,
            notification.FileHash,
            notification.ConfirmedCategory);

        _logger.LogDebug("Recorded confirmation metrics for {FileHash}: {Category}", 
            notification.FileHash, notification.ConfirmedCategory);
    }

    // Note: There's no FileMoveStartedEvent in the current domain events
    // We'll handle move metrics in the FileMovedEvent handler

    public async Task Handle(FileMovedEvent notification, CancellationToken cancellationToken)
    {
        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.MoveCompleted,
            notification.FileHash,
            notification.Category);

        // Calculate and record move performance if timestamps are available
        if (notification.OccurredAt != default)
        {
            // Note: We don't have the move start time here, but we could enhance the event
            // to include processing duration for more accurate performance metrics
            await _metricsService.RecordPerformanceDataAsync(
                OperationType.FileMove,
                TimeSpan.FromSeconds(1)); // Placeholder duration
        }

        _logger.LogDebug("Recorded file move completion metrics for {FileHash}", notification.FileHash);
    }

    public async Task Handle(FileProcessingErrorEvent notification, CancellationToken cancellationToken)
    {
        // Determine error type from error message (simplified classification)
        var errorType = ClassifyError(notification.ErrorMessage);

        await _metricsService.RecordErrorEventAsync(
            errorType,
            notification.FileHash,
            notification.ErrorMessage);

        await _metricsService.RecordProcessingEventAsync(
            ProcessingEventType.ProcessingFailed,
            notification.FileHash);

        _logger.LogDebug("Recorded error metrics for {FileHash}: {ErrorType} - {ErrorMessage}", 
            notification.FileHash, errorType, notification.ErrorMessage);
    }

    /// <summary>
    /// Classifies error messages into error types for metrics categorization.
    /// Simple keyword-based classification without complecting concerns.
    /// </summary>
    private static ErrorType ClassifyError(string errorMessage)
    {
        var lowerMessage = errorMessage.ToLowerInvariant();

        if (lowerMessage.Contains("classification") || lowerMessage.Contains("ml") || lowerMessage.Contains("model"))
            return ErrorType.ClassificationError;

        if (lowerMessage.Contains("file") || lowerMessage.Contains("path") || lowerMessage.Contains("directory"))
            return ErrorType.FileAccessError;

        if (lowerMessage.Contains("database") || lowerMessage.Contains("sql") || lowerMessage.Contains("entity"))
            return ErrorType.DatabaseError;

        if (lowerMessage.Contains("network") || lowerMessage.Contains("connection") || lowerMessage.Contains("timeout"))
            return ErrorType.NetworkError;

        if (lowerMessage.Contains("validation") || lowerMessage.Contains("invalid") || lowerMessage.Contains("format"))
            return ErrorType.ValidationError;

        if (lowerMessage.Contains("concurrency") || lowerMessage.Contains("conflict") || lowerMessage.Contains("lock"))
            return ErrorType.ConcurrencyError;

        if (lowerMessage.Contains("memory") || lowerMessage.Contains("resource") || lowerMessage.Contains("disk"))
            return ErrorType.ResourceExhaustionError;

        // Default to validation error for unclassified errors
        return ErrorType.ValidationError;
    }
}