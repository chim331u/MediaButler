namespace MediaButler.API.Modules.RealTime.SSE;

/// <summary>
/// Represents a connected Server-Sent Events client.
/// </summary>
public class SseClient
{
    public string ConnectionId { get; }
    public HttpResponse Response { get; }
    public CancellationToken CancellationToken { get; }
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SseClient(string connectionId, HttpResponse response, CancellationToken cancellationToken)
    {
        ConnectionId = connectionId;
        Response = response;
        CancellationToken = cancellationToken;
    }

    public async Task SendEventAsync(string eventName, object data)
    {
        if (CancellationToken.IsCancellationRequested) return;

        await _lock.WaitAsync(CancellationToken);
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            
            // Format:
            // event: eventName\n
            // data: json\n\n
            
            await Response.WriteAsync($"event: {eventName}\n", CancellationToken);
            await Response.WriteAsync($"data: {json}\n\n", CancellationToken);
            await Response.Body.FlushAsync(CancellationToken);
        }
        catch
        {
            // Ignore write errors (client likely disconnected)
            // The connection manager will handle removal
        }
        finally
        {
            _lock.Release();
        }
    }
}
