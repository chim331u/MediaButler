using System;
using System.Threading;
using System.Threading.Tasks;
using MediaButler.Shared.UI.Models;

namespace MediaButler.Shared.UI.Services;

/// <summary>
/// Interface for SSE notification services.
/// </summary>
public interface ISseNotificationService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    bool IsConnected { get; }
    string? ConnectionId { get; }
    DateTime? ConnectedAt { get; }

    event EventHandler<bool>? ConnectionStateChanged;
    event EventHandler<string>? ErrorOccurred;

    void OnFileProcessed(Action<int, string, MoveFilesResults> handler);
    void OnJobCompleted(Action<string, MoveFilesResults> handler);
    void OnNotification(Action<string, decimal> handler);

    // Legacy subscription methods for backward compatibility
    IDisposable SubscribeToFileDiscovery(Action<string, string, DateTime> handler);
    IDisposable SubscribeToFileProcessing(Action<string, string, string> handler);
    IDisposable SubscribeToSystemStatus(Action<string, string, string> handler);
    IDisposable SubscribeToErrors(Action<string, string, string> handler);
    IDisposable SubscribeToHealthCheckPing(Action<string, DateTime> handler);
}
