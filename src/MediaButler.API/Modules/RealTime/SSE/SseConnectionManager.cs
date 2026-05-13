using System.Collections.Concurrent;

namespace MediaButler.API.Modules.RealTime.SSE;

/// <summary>
/// Manages active SSE connections and handles broadcasting events.
/// Replaces SignalR Hub logic.
/// </summary>
public class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();
    private readonly ILogger<SseConnectionManager> _logger;

    public SseConnectionManager(ILogger<SseConnectionManager> logger)
    {
        _logger = logger;
    }

    public void AddClient(SseClient client)
    {
        _clients.TryAdd(client.ConnectionId, client);
        _logger.LogInformation("SSE Client connected: {ConnectionId}", client.ConnectionId);
    }

    public void RemoveClient(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out _))
        {
            _logger.LogInformation("SSE Client disconnected: {ConnectionId}", connectionId);
        }
    }

    public async Task BroadcastAsync(string eventName, object data)
    {
        // Broadcast to all connected clients
        // "Simple Made Easy": No complex groups for now, just broadcast.
        // If we strictly need groups later, we can add a _groups dictionary.
        
        var tasks = _clients.Values.Select(client => client.SendEventAsync(eventName, data));
        await Task.WhenAll(tasks);
    }

    public int GetConnectionCount() => _clients.Count;
}
