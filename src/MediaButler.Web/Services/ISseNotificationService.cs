namespace MediaButler.Web.Services;

/// <summary>
/// Server-Sent Events (SSE) notification service interface for managing real-time communication
/// Follows "Simple Made Easy" principles with clear separation of concerns
/// </summary>
public interface ISseNotificationService
{
    /// <summary>
    /// Gets the current connection state
    /// </summary>
    string ConnectionState { get; }

    /// <summary>
    /// Indicates if the service is currently connected and ready for notifications
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Starts the SSE connection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for operation</param>
    /// <returns>Task representing the connection operation</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the SSE connection gracefully
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
    /// Subscribes to job progress notifications (training, scanning, etc.)
    /// </summary>
    /// <param name="handler">Handler for job progress events (jobType, message, progress)</param>
    /// <returns>Disposable subscription that can be used to unsubscribe</returns>
    IDisposable SubscribeToJobProgress(Action<string, string, int> handler);

    /// <summary>
    /// Event fired when the connection state changes
    /// </summary>
    event EventHandler<string>? ConnectionStateChanged;

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
}
