using MediaButler.Core.Common;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Service interface for user notifications in the MediaButler system.
/// Provides simple notification methods without complex event systems, following "Simple Made Easy" principles.
/// </summary>
/// <remarks>
/// This service integrates with existing domain events to provide user-friendly notifications
/// about file processing operations. It keeps notification concerns separate from business logic
/// while leveraging the existing event infrastructure for simplicity.
/// 
/// Key design principles:
/// - Simple interface with clear, single-purpose methods
/// - Built on existing domain events (no separate alert system)
/// - Direct integration with existing API endpoints
/// - No complex notification queues or state management
/// </remarks>
public interface INotificationService
{
    /// <summary>
    /// Notifies when a file operation has started.
    /// </summary>
    /// <param name="fileHash">Hash of the file being processed</param>
    /// <param name="operation">Description of the operation being performed</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating notification success</returns>
    Task<Result> NotifyOperationStartedAsync(string fileHash, string operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies about progress updates for ongoing file operations.
    /// </summary>
    /// <param name="fileHash">Hash of the file being processed</param>
    /// <param name="progress">Progress information or status update</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating notification success</returns>
    Task<Result> NotifyOperationProgressAsync(string fileHash, string progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies when a file operation has completed successfully.
    /// </summary>
    /// <param name="fileHash">Hash of the file that was processed</param>
    /// <param name="result">Description of the successful operation result</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating notification success</returns>
    Task<Result> NotifyOperationCompletedAsync(string fileHash, string result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies when a file operation has failed.
    /// </summary>
    /// <param name="fileHash">Hash of the file that failed to process</param>
    /// <param name="error">Error message describing what went wrong</param>
    /// <param name="canRetry">Whether the operation can be retried</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating notification success</returns>
    Task<Result> NotifyOperationFailedAsync(string fileHash, string error, bool canRetry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies about general system status or information.
    /// </summary>
    /// <param name="message">Status message or information to notify</param>
    /// <param name="severity">Severity level of the notification (Info, Warning, Error)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating notification success</returns>
    Task<Result> NotifySystemStatusAsync(string message, NotificationSeverity severity = NotificationSeverity.Info, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the severity level of a notification.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>Informational message</summary>
    Info = 0,
    
    /// <summary>Warning message that requires attention</summary>
    Warning = 1,
    
    /// <summary>Error message that requires immediate attention</summary>
    Error = 2
}