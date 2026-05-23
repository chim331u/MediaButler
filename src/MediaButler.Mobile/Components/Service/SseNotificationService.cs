using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Models;
using MediaButler.Shared.UI.Models;
using MediaButler.Shared.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MediaButler.Mobile.Components.Service;

/// <summary>
/// SSE notification service implementation for MAUI.
/// Uses HttpClient to maintain a persistent connection to the Server-Sent Events endpoint.
/// </summary>
public class SseNotificationService : ISseNotificationService, IAsyncDisposable
{
    private readonly ILogger<SseNotificationService> _logger;
    private readonly IConfigurationService _configService;
    private readonly IHttpsClientHandlerService _httpsHandler;
    private readonly NotificationSettings _settings;
    
    private HttpClient? _httpClient;
    private CancellationTokenSource? _connectionCts;
    private Task? _connectionTask;
    private volatile bool _isConnected;
    
    // Event handlers
    private readonly List<Action<int, string, MoveFilesResults>> _fileProcessedHandlers = new();
    private readonly List<Action<string, MoveFilesResults>> _jobCompletedHandlers = new();
    private readonly List<Action<string, decimal>> _notificationHandlers = new();

    public bool IsConnected => _isConnected;
    public string? ConnectionId { get; private set; }
    public DateTime? ConnectedAt { get; private set; }

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public SseNotificationService(
        ILogger<SseNotificationService> logger,
        IConfigurationService configService,
        IHttpsClientHandlerService httpsHandler,
        IOptions<NotificationSettings> settings)
    {
        _logger = logger;
        _configService = configService;
        _httpsHandler = httpsHandler;
        _settings = settings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionTask != null && !_connectionTask.IsCompleted)
        {
            _logger.LogDebug("SSE connection already active or connecting");
            return;
        }

        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _connectionTask = ConnectAndReadLoopAsync(_connectionCts.Token);
        
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _connectionCts?.Cancel();
        
        if (_connectionTask != null)
        {
            try
            {
                await _connectionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
        
        _isConnected = false;
        _httpClient?.Dispose();
        _httpClient = null;
    }

    private async Task ConnectAndReadLoopAsync(CancellationToken token)
    {
        int retryCount = 0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var baseUrl = _configService.ApiBaseUrl.TrimEnd('/');
                var url = $"{baseUrl}/api/sse/connect";
                if (!string.IsNullOrEmpty(_configService.ApiKey))
                {
                    url = $"{url}?apiKey={Uri.EscapeDataString(_configService.ApiKey)}";
                }

                _logger.LogInformation("Connecting to SSE endpoint: {Url}", url);

                var handler = _httpsHandler.GetPlatformMessageHandler();
                _httpClient = new HttpClient(handler);
                _httpClient.Timeout = Timeout.InfiniteTimeSpan;
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SSE Connection failed: {StatusCode}", response.StatusCode);
                    await Task.Delay(5000, token);
                    continue;
                }

                _isConnected = true;
                _logger.LogInformation("✅ SSE Connected");
                retryCount = 0;
                
                using var stream = await response.Content.ReadAsStreamAsync(token);
                using var reader = new StreamReader(stream);

                string? currentEvent = null;

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        currentEvent = null;
                        continue;
                    }

                    if (line.StartsWith("event: "))
                    {
                        currentEvent = line.Substring(7).Trim();
                    }
                    else if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6).Trim();
                        if (!string.IsNullOrEmpty(currentEvent))
                        {
                            DispatchEvent(currentEvent, data);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SSE connection stopped");
                break;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                retryCount++;
                _logger.LogError(ex, "SSE connection error (Attempt {Retry})", retryCount);

                var delaySeconds = Math.Min(30, Math.Pow(2, retryCount));
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
            }
            finally
            {
               _isConnected = false; 
            }
        }
    }

    private void DispatchEvent(string eventName, string dataJson)
    {
        try
        {
            _logger.LogDebug("Received SSE Event: {Event}", eventName);

            switch (eventName)
            {
                case "MoveFileNotification":
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson);
                    if (data != null && data.TryGetValue("fileId", out var idElem) && data.TryGetValue("status", out var statusElem))
                    {
                        var fileId = idElem.GetInt32();
                        var statusStr = statusElem.GetString();
                        var fileName = data.TryGetValue("fileName", out var fn) ? fn.GetString() : "Unknown";
                        
                        var result = MapStatusToResult(statusStr);
                        string message = $"File {fileName} status: {statusStr}";
                        
                        NotifyFileProcessed(fileId, message, result);
                    }
                    break;
                }

                case "BatchCompleted":
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson);
                    if (data != null)
                    {
                        string message = data.TryGetValue("Message", out var msg) ? msg.GetString() ?? "Batch Completed" : "Batch Completed";
                        NotifyJobCompleted(message, MoveFilesResults.Completed);
                    }
                    break;
                }
                
                case "BatchFailed":
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson);
                    if (data != null)
                    {
                         string message = data.TryGetValue("Message", out var msg) ? msg.GetString() ?? "Batch Failed" : "Batch Failed";
                         NotifyJobCompleted(message, MoveFilesResults.Failed);
                    }
                    break;
                }

                case "JobProgressNotification":
                {
                     var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson);
                     if (data != null)
                     {
                         string message = data.TryGetValue("message", out var m) ? m.GetString() ?? "" : "";
                         decimal progress = data.TryGetValue("progress", out var p) ? p.GetDecimal() : 0;
                         NotifyNotification(message, progress);
                     }
                     break;
                }
                
                case "FileDiscoveryNotification":
                     NotifyNotification("New file discovered", 0);
                     break;
                     
                case "SystemStatusNotification":
                    var sysData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson);
                    if (sysData != null)
                    {
                         string status = sysData.TryGetValue("status", out var s) ? s.GetString() ?? "" : "";
                         NotifyNotification($"System Update: {status}", 0);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching SSE event: {EventName}", eventName);
        }
    }

    private MoveFilesResults MapStatusToResult(string? status)
    {
        if (string.IsNullOrEmpty(status)) return MoveFilesResults.Failed;
        
        return status.ToLowerInvariant() switch
        {
            "moved" => MoveFilesResults.Moved,
            "completed" => MoveFilesResults.Completed,
            "idnotpresent" => MoveFilesResults.IdNotPresent,
            "failed" => MoveFilesResults.Failed,
            _ => MoveFilesResults.Completed
        };
    }

    private void NotifyFileProcessed(int fileId, string text, MoveFilesResults result)
    {
        foreach (var handler in _fileProcessedHandlers.ToList())
        {
             try { handler(fileId, text, result); } catch { }
        }
    }

    private void NotifyJobCompleted(string text, MoveFilesResults result)
    {
        foreach (var handler in _jobCompletedHandlers.ToList())
        {
             try { handler(text, result); } catch { }
        }
    }

    private void NotifyNotification(string message, decimal progress)
    {
        foreach (var handler in _notificationHandlers.ToList())
        {
             try { handler(message, progress); } catch { }
        }
    }

    public void OnFileProcessed(Action<int, string, MoveFilesResults> handler) => _fileProcessedHandlers.Add(handler);
    public void OnJobCompleted(Action<string, MoveFilesResults> handler) => _jobCompletedHandlers.Add(handler);
    public void OnNotification(Action<string, decimal> handler) => _notificationHandlers.Add(handler);

    // Legacy subscription methods (no-op or minimal implementation if not used in Mobile)
    public IDisposable SubscribeToFileDiscovery(Action<string, string, DateTime> handler) => new SubscriptionDisposable(() => { });
    public IDisposable SubscribeToFileProcessing(Action<string, string, string> handler) => new SubscriptionDisposable(() => { });
    public IDisposable SubscribeToSystemStatus(Action<string, string, string> handler) => new SubscriptionDisposable(() => { });
    public IDisposable SubscribeToErrors(Action<string, string, string> handler) => new SubscriptionDisposable(() => { });
    public IDisposable SubscribeToHealthCheckPing(Action<string, DateTime> handler) => new SubscriptionDisposable(() => { });

    private class SubscriptionDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public SubscriptionDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
