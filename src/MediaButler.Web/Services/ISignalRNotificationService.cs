using Microsoft.AspNetCore.SignalR.Client;

namespace MediaButler.Web.Services;

/// <summary>
/// Centralized SignalR notification service interface for managing real-time communication
/// Follows "Simple Made Easy" principles with clear separation of concerns
/// </summary>
public interface ISignalRNotificationService
{
    /// <summary>
    /// Gets the current connection state
    /// </summary>
    HubConnectionState ConnectionState { get; }

    /// <summary>
    /// Indicates if the service is currently connected and ready for notifications
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Indicates if auto-reconnection is enabled
    /// </summary>
    bool AutoReconnectEnabled { get; }

    /// <summary>
    /// Starts the SignalR connection with automatic retry logic
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for operation</param>
    /// <returns>Task representing the connection operation</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the SignalR connection gracefully
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for operation</param>
    /// <returns>Task representing the disconnection operation</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to file discovery notifications
    /// </summary>
    /// <param name="handler">Handler for file discovery events</param>
    /// <returns>Disposable subscription that can be used to unsubscribe</returns>
    IDisposable SubscribeToFileDiscovery(Action<string, string, DateTime> handler);

    /// <summary>
    /// Subscribes to file processing status notifications
    /// </summary>
    /// <param name="handler">Handler for file processing events</param>
    /// <returns>Disposable subscription that can be used to unsubscribe</returns>
    IDisposable SubscribeToFileProcessing(Action<string, string, string> handler);

    /// <summary>
    /// Subscribes to system status notifications
    /// </summary>
    /// <param name="handler">Handler for system status events</param>
    /// <returns>Disposable subscription that can be used to unsubscribe</returns>
    IDisposable SubscribeToSystemStatus(Action<string, string, string> handler);

    /// <summary>
    /// Subscribes to error notifications
    /// </summary>
    /// <param name="handler">Handler for error events</param>
    /// <returns>Disposable subscription that can be used to unsubscribe</returns>
    IDisposable SubscribeToErrors(Action<string, string, string> handler);

    /// <summary>
    /// Event fired when the connection state changes
    /// </summary>
    event EventHandler<HubConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Event fired when a connection error occurs
    /// </summary>
    event EventHandler<Exception>? ConnectionError;

    /// <summary>
    /// Event fired when connection is successfully established
    /// </summary>
    event EventHandler? Connected;

    /// <summary>
    /// Event fired when connection is lost
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Enables or disables automatic reconnection on connection loss
    /// </summary>
    /// <param name="enabled">Whether to enable auto-reconnection</param>
    void SetAutoReconnect(bool enabled);

    /// <summary>
    /// Gets connection statistics and health information
    /// </summary>
    /// <returns>Connection statistics</returns>
    SignalRConnectionStats GetConnectionStats();
}

/// <summary>
/// Connection statistics for monitoring SignalR health
/// </summary>
public class SignalRConnectionStats
{
    public DateTime? ConnectedAt { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public int ReconnectAttempts { get; init; }
    public TimeSpan? TotalConnectedTime { get; init; }
    public HubConnectionState CurrentState { get; init; }
    public string? LastError { get; init; }
    public DateTime? LastErrorAt { get; init; }
}