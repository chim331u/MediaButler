using MediaButler.Mobile.Data;

namespace MediaButler.Mobile.Components.Interfaces;

/// <summary>
/// SSE notification service for real-time updates.
/// Single responsibility: Real-time communication via Server-Sent Events.
/// Centralized connection management.
/// </summary>
public interface ISseNotificationService
{
    /// <summary>
    /// Starts SSE connection to the server.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops SSE connection.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Current connection state.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Subscribe to file processing notifications.
    /// Handler receives: fileId, resultText, moveResult
    /// </summary>
    void OnFileProcessed(Action<int, string, MoveFilesResults> handler);

    /// <summary>
    /// Subscribe to batch job completion notifications.
    /// Handler receives: resultText, moveResult
    /// </summary>
    void OnJobCompleted(Action<string, MoveFilesResults> handler);

    /// <summary>
    /// Subscribe to general notifications (scan progress, etc).
    /// Handler receives: message, progress percentage
    /// </summary>
    void OnNotification(Action<string, decimal> handler);
}
