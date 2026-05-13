using MediaButler.Web.Services;

namespace MediaButler.Web.Services;

public interface ISseNotificationService
{
    bool IsConnected { get; }
    string? ConnectionId { get; }
    DateTime? ConnectedAt { get; }
    Task StartAsync();
    Task StopAsync();
    
    // Subscriptions
    IDisposable SubscribeToFileDiscovery(Action<string, string, DateTime> handler);
    IDisposable SubscribeToFileProcessing(Action<string, string, string> handler); // For legacy compatibility with string updates
    IDisposable SubscribeToSystemStatus(Action<string, string, string> handler);
    IDisposable SubscribeToErrors(Action<string, string, string> handler);
    IDisposable SubscribeToJobProgress(Action<string, string, int> handler);
    IDisposable SubscribeToHealthCheckPing(Action<string, DateTime> handler);

    event EventHandler<bool>? ConnectionStateChanged;
    event EventHandler<string>? ErrorOccurred;
}
