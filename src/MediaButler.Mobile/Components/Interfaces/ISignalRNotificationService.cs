using MediaButler.Mobile.Data;
using Microsoft.AspNetCore.SignalR.Client;

namespace MediaButler.Mobile.Components.Interfaces;

/// <summary>
/// SignalR notification service for real-time updates.
/// Single responsibility: Real-time communication only.
/// Centralized hub connection management.
/// </summary>
public interface ISignalRNotificationService
{
    /// <summary>
    /// Starts SignalR connection to notification hub.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops SignalR connection gracefully.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Current connection state.
    /// </summary>
    HubConnectionState ConnectionState { get; }

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
