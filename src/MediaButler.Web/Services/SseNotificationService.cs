using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MediaButler.Web.Models;
using MediaButler.Shared.UI.Models;
using MediaButler.Shared.UI.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace MediaButler.Web.Services;

public class SseNotificationService : ISseNotificationService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SseNotificationService> _logger;
    private readonly ApiSettings _apiSettings;
    
    private DotNetObjectReference<SseNotificationService>? _dotnetRef;
    private bool _isConnected;
    
    // Subscriptions
    private readonly ConcurrentDictionary<string, List<Delegate>> _handlers = new();

    public bool IsConnected { get; private set; }
    public string? ConnectionId { get; private set; }
    public DateTime? ConnectedAt { get; private set; }

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public SseNotificationService(
        IJSRuntime jsRuntime,
        ILogger<SseNotificationService> logger,
        IOptions<ApiSettings> apiSettings)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _apiSettings = apiSettings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_dotnetRef == null)
        {
            _dotnetRef = DotNetObjectReference.Create(this);
        }

        var url = $"{_apiSettings.BaseUrl.TrimEnd('/')}/api/sse/connect";
        await _jsRuntime.InvokeVoidAsync("sseInterop.start", url, _dotnetRef);
    }

    public async Task StopAsync()
    {
        await _jsRuntime.InvokeVoidAsync("sseInterop.stop");
        IsConnected = false;
        ConnectionId = null;
        ConnectedAt = null;
        ConnectionStateChanged?.Invoke(this, IsConnected);
    }

    [JSInvokable]
    public void OnConnected()
    {
        _logger.LogInformation("SSE Connected");
        // Initial connection established by JS (before receiving 'connected' event with data)
        // We'll wait for the specific event to set ID/Timestamp, but we can mark as connected here
        if (!IsConnected)
        {
            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, IsConnected);
        }
    }

    [JSInvokable]
    public void OnDisconnected()
    {
        _logger.LogWarning("SSE Disconnected");
        IsConnected = false;
        ConnectionId = null;
        ConnectedAt = null;
        ConnectionStateChanged?.Invoke(this, IsConnected);
        // Implement auto-reconnect logic here if needed, or rely on browser's EventSource auto-reconnect
    }

    [JSInvokable]
    public void OnError(string error)
    {
        _logger.LogError("SSE Error: {Error}", error);
        ErrorOccurred?.Invoke(this, error);
    }

    [JSInvokable]
    public void OnMessage(string eventName, string dataJson)
    {
        try
        {
            _logger.LogDebug("SSE Object Received: {Event} - {Data}", eventName, dataJson);
            
            // Dispatch to handlers based on eventName (or mapped internal category)
            DispatchEvent(eventName, dataJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling SSE message");
        }
    }

    private void DispatchEvent(string eventName, string json)
    {
        // Map SSE event names to internal subscription categories
        switch (eventName)
        {
            case "FileDiscoveryNotification":
                NotifyFileDiscovery(json);
                break;
            case "MoveFileNotification":
            case "BatchStarted":
            case "BatchProgress":
            case "BatchCompleted":
            case "BatchFailed":
                NotifyFileProcessing(eventName, json);
                break;
            case "JobProgressNotification":
                NotifyJobProgress(json);
                break;
            case "SystemStatusNotification":
                NotifySystemStatus(json);
                break;
            case "ErrorNotification":
            case "Error":
                NotifyError(json);
                break;
            case "HealthCheckPing":
                NotifyHealthCheckPing(json);
                break;
            case "connected":
                HandleConnectedEvent(json);
                break;
        }
    }

    private void HandleConnectedEvent(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            // Check for both PascalCase (default C#) and camelCase (web standard)
            if (data.TryGetProperty("ConnectionId", out var idProp) || 
                data.TryGetProperty("connectionId", out idProp))
            {
               ConnectionId = idProp.GetString();
            }

            if (data.TryGetProperty("Timestamp", out var tsProp) || 
                data.TryGetProperty("timestamp", out tsProp))
            {
                ConnectedAt = tsProp.GetDateTime();
            }
            
            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, IsConnected);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing connected event: {ex.Message}");
        }
    }

    private void NotifyFileDiscovery(string json)
    {
        if (_handlers.TryGetValue("FileDiscovery", out var handlers))
        {
            try 
            {
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                var fileName = data.GetProperty("fileName").GetString() ?? "";
                var filePath = data.GetProperty("filePath").GetString() ?? "";
                var discoveredAt = data.GetProperty("discoveredAt").GetDateTime();
                
                foreach (var handler in handlers)
                {
                    ((Action<string, string, DateTime>)handler)(fileName, filePath, discoveredAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse FileDiscoveryNotification");
            }
        }
    }

    // ISseNotificationService implementation
    private Action<int, string, MoveFilesResults>? _onFileProcessed;
    private Action<string, MoveFilesResults>? _onJobCompleted;
    private Action<string, decimal>? _onNotification;

    public void OnFileProcessed(Action<int, string, MoveFilesResults> handler) => _onFileProcessed = handler;
    public void OnJobCompleted(Action<string, MoveFilesResults> handler) => _onJobCompleted = handler;
    public void OnNotification(Action<string, decimal> handler) => _onNotification = handler;

    private void NotifyFileProcessing(string eventName, string json)
    {
         // Generic processing
         if (_handlers.TryGetValue("FileProcessing", out var handlers))
         {
             string status = eventName;
             string details = json;
             try 
             {
                 using var doc = JsonDocument.Parse(json);
                 if (doc.RootElement.TryGetProperty("message", out var msgProp))
                 {
                     details = msgProp.GetString() ?? "";
                 }
             }
             catch {}

             foreach (var handler in handlers)
             {
                 ((Action<string, string, string>)handler)("", status, details);
             }
         }

         // New interface processing
         try 
         {
             using var doc = JsonDocument.Parse(json);
             var root = doc.RootElement;
             
             if (eventName == "MoveFileNotification")
             {
                 var fileId = root.TryGetProperty("fileId", out var idProp) ? idProp.GetInt32() : 0;
                 var resultText = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
                 var result = root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "Success" 
                     ? MoveFilesResults.Moved : MoveFilesResults.Failed;
                 
                 _onFileProcessed?.Invoke(fileId, resultText, result);
             }
             else if (eventName == "BatchCompleted")
             {
                 var resultText = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "Batch completed";
                 _onJobCompleted?.Invoke(resultText, MoveFilesResults.Completed);
             }
             else if (eventName == "BatchFailed")
             {
                 var resultText = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "Batch failed";
                 _onJobCompleted?.Invoke(resultText, MoveFilesResults.Failed);
             }
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error parsing SSE for shared interface");
         }
    }

    private void NotifyJobProgress(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            var message = data.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
            var progress = data.TryGetProperty("progress", out var progProp) ? progProp.GetDecimal() : 0;

            _onNotification?.Invoke(message, progress);

            if (_handlers.TryGetValue("JobProgress", out var handlers))
            {
                var jobType = data.TryGetProperty("jobType", out var typeProp) ? typeProp.GetString() ?? "" : "";
                foreach (var handler in handlers)
                {
                    ((Action<string, string, int>)handler)(jobType, message, (int)progress);
                }
            }
        }
        catch {}
    }

    private void NotifySystemStatus(string json)
    {
        if (_handlers.TryGetValue("SystemStatus", out var handlers))
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                var component = data.GetProperty("component").GetString() ?? "";
                var status = data.GetProperty("status").GetString() ?? "";
                var message = data.GetProperty("message").GetString() ?? "";

                foreach (var handler in handlers)
                {
                    ((Action<string, string, string>)handler)(component, status, message);
                }
            }
            catch {}
        }
    }

    private void NotifyError(string json)
    {
        if (_handlers.TryGetValue("Error", out var handlers))
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                // Try different error formats
                var errorType = "General";
                var message = "";
                
                 if (data.TryGetProperty("errorType", out var typeProp)) errorType = typeProp.GetString() ?? "General";
                 if (data.TryGetProperty("message", out var msgProp)) message = msgProp.GetString() ?? "";
                 else if (data.TryGetProperty("title", out var titleProp)) message = titleProp.GetString() ?? ""; // Batch failed format

                foreach (var handler in handlers)
                {
                    ((Action<string, string, string>)handler)("", errorType, message);
                }
            }
            catch {}
        }
    }

    private void NotifyHealthCheckPing(string json)
    {
        if (_handlers.TryGetValue("HealthCheckPing", out var handlers))
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                var pingId = data.GetProperty("pingId").GetString() ?? "";
                var timestamp = data.GetProperty("timestamp").GetDateTime();

                foreach (var handler in handlers)
                {
                    ((Action<string, DateTime>)handler)(pingId, timestamp);
                }
            }
            catch {}
        }
    }

    // Subscription helpers
    private IDisposable AddHandler(string category, Delegate handler)
    {
        _handlers.AddOrUpdate(category, 
            new List<Delegate> { handler }, 
            (_, list) => { list.Add(handler); return list; });

        return new SubscriptionDisposable(() => 
        {
            if (_handlers.TryGetValue(category, out var list))
            {
                list.Remove(handler);
            }
        });
    }

    public IDisposable SubscribeToFileDiscovery(Action<string, string, DateTime> handler) => AddHandler("FileDiscovery", handler);
    public IDisposable SubscribeToFileProcessing(Action<string, string, string> handler) => AddHandler("FileProcessing", handler);
    public IDisposable SubscribeToSystemStatus(Action<string, string, string> handler) => AddHandler("SystemStatus", handler);
    public IDisposable SubscribeToErrors(Action<string, string, string> handler) => AddHandler("Error", handler);
    public IDisposable SubscribeToJobProgress(Action<string, string, int> handler) => AddHandler("JobProgress", handler);
    public IDisposable SubscribeToHealthCheckPing(Action<string, DateTime> handler) => AddHandler("HealthCheckPing", handler);

    public async ValueTask DisposeAsync()
    {
        _dotnetRef?.Dispose();
        await StopAsync();
    }
    
    private class SubscriptionDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public SubscriptionDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
