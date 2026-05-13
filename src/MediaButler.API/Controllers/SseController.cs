using Microsoft.AspNetCore.Mvc;
using MediaButler.API.Modules.RealTime.SSE;

namespace MediaButler.API.Controllers;

[ApiController]
[Route("api/sse")]
public class SseController : ControllerBase
{
    private readonly SseConnectionManager _connectionManager;

    public SseController(SseConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    [HttpGet("connect")]
    public async Task Connect()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        
        // Remove buffering to ensure events are sent immediately
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
        feature?.DisableBuffering();

        var connectionId = HttpContext.Connection.Id; // Use Kestrel connection ID
        var client = new SseClient(connectionId, Response, HttpContext.RequestAborted);
        
        _connectionManager.AddClient(client);

        try
        {
            // Send initial connection confirmation
            await client.SendEventAsync("connected", new { ConnectionId = connectionId, Timestamp = DateTime.UtcNow });

            // Keep connection open until client disconnects
            await Task.Delay(Timeout.Infinite, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
        finally
        {
            _connectionManager.RemoveClient(connectionId);
        }
    }

    [HttpPost("ping")]
    public async Task<IActionResult> Ping([FromBody] PingRequest request)
    {
        var timestamp = DateTime.UtcNow;
        await _connectionManager.BroadcastAsync("HealthCheckPing", new 
        { 
            PingId = request.PingId, 
            Timestamp = timestamp 
        });
        return Ok(new { SentAt = timestamp });
    }

    public class PingRequest 
    {
        public required string PingId { get; set; }
    }
}
