using System.Text.Json;
using MediaButler.Web.Models;
using Microsoft.JSInterop;

namespace MediaButler.Web.Services;

/// <summary>
/// Server-Sent Events (SSE) notification service for real-time updates from the Go API
/// </summary>
public class SseNotificationService : ISseNotificationService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly string _sseEndpointUrl;
    private IJSObjectReference? _sseModule;
    private DotNetObjectReference<SseNotificationService>? _dotNetRef;

    // Connection state
    private string _connectionState = "Disconnected";
    private readonly List<Action<string, string, DateTime>> _fileDiscoveryHandlers = new();
    private readonly List<Action<string, string, string>> _fileProcessingHandlers = new();
    private readonly List<Action<string, string, string>> _systemStatusHandlers = new();
    private readonly List<Action<string, string, string>> _errorHandlers = new();
    private readonly List<Action<string, string, int>> _jobProgressHandlers = new();

    // Events
    public event EventHandler<string>? ConnectionStateChanged;
    public event EventHandler<Exception>? ConnectionError;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public string ConnectionState => _connectionState;
    public bool IsConnected => _connectionState == "Connected" || _connectionState == "Open";

    public SseNotificationService(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;

        // Get SSE endpoint URL from configuration (defaults to Go API at port 5001)
        var apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5001";
        _sseEndpointUrl = $"{apiBaseUrl.TrimEnd('/')}/events";

        Console.WriteLine($"[SSE Service] Initialized with endpoint: {_sseEndpointUrl}");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("[SSE Service] Starting connection...");

            // Load the JavaScript module
            _sseModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", cancellationToken, "./js/sse-client.js");

            // Create .NET object reference for JS callbacks
            _dotNetRef = DotNetObjectReference.Create(this);

            // Start the SSE connection (only pass URL and dotNetRef to JavaScript)
            await _sseModule.InvokeVoidAsync("connectSSE", _sseEndpointUrl, _dotNetRef);

            Console.WriteLine("[SSE Service] Connection initiated");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SSE Service] Failed to start: {ex.Message}");
            _connectionState = "Failed";
            ConnectionError?.Invoke(this, ex);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("[SSE Service] Stopping connection...");

            if (_sseModule != null)
            {
                await _sseModule.InvokeVoidAsync("disconnectSSE", cancellationToken);
            }

            _connectionState = "Disconnected";
            Disconnected?.Invoke(this, EventArgs.Empty);

            Console.WriteLine("[SSE Service] Disconnected");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SSE Service] Error during stop: {ex.Message}");
        }
    }

    /// <summary>
    /// JavaScript callback for SSE events
    /// </summary>
    [JSInvokable]
    public void HandleSseEvent(string eventType, string jsonData)
    {
        try
        {
            Console.WriteLine($"[SSE Service] Handling event: {eventType}");

            switch (eventType)
            {
                // Scan events
                case "scan.started":
                    HandleScanStarted(jsonData);
                    break;
                case "scan.found":
                    HandleScanFound(jsonData);
                    break;
                case "scan.completed":
                    HandleScanCompleted(jsonData);
                    break;

                // Move events
                case "move.started":
                    HandleMoveStarted(jsonData);
                    break;
                case "move.progress":
                    HandleMoveProgress(jsonData);
                    break;
                case "move.completed":
                    HandleMoveCompleted(jsonData);
                    break;

                // Training events
                case "training.started":
                    HandleTrainingStarted(jsonData);
                    break;
                case "training.completed":
                    HandleTrainingCompleted(jsonData);
                    break;

                // Batch events
                case "batch.started":
                    HandleBatchStarted(jsonData);
                    break;
                case "batch.progress":
                    HandleBatchProgress(jsonData);
                    break;
                case "batch.completed":
                    HandleBatchCompleted(jsonData);
                    break;
                case "batch.failed":
                    HandleBatchFailed(jsonData);
                    break;

                // Error events
                case "error.move_failed":
                case "error.classification_failed":
                    HandleError(eventType, jsonData);
                    break;

                // Connection events
                case "connected":
                    HandleConnected(jsonData);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SSE Service] Error handling event {eventType}: {ex.Message}");
            ConnectionError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// JavaScript callback for connection state changes
    /// </summary>
    [JSInvokable]
    public void HandleConnectionStateChanged(string newState)
    {
        Console.WriteLine($"[SSE Service] Connection state changed: {_connectionState} -> {newState}");
        _connectionState = newState;
        ConnectionStateChanged?.Invoke(this, newState);

        if (newState == "Connected" || newState == "Open")
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }
        else if (newState == "Disconnected" || newState == "Closed")
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    // Event handlers
    private void HandleScanStarted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<ScanStartedEvent>(jsonData);
        if (evt != null)
        {
            NotifySystemStatus("FileScanner", "Started", $"Scanning {evt.Path}");
        }
    }

    private void HandleScanFound(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<ScanFoundEvent>(jsonData);
        if (evt != null)
        {
            NotifySystemStatus("FileScanner", "Progress", $"Found {evt.FileCount} files");
        }
    }

    private void HandleScanCompleted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<ScanCompletedEvent>(jsonData);
        if (evt != null)
        {
            NotifySystemStatus("FileScanner", "Completed", $"Scan completed: {evt.TotalFiles} total, {evt.NewFiles} new");

            // Also notify file discovery for each new file (simplified)
            if (evt.NewFiles > 0)
            {
                NotifyFileDiscovery("Multiple files", evt.ScanId, evt.CompletedTime);
            }
        }
    }

    private void HandleMoveStarted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<MoveStartedEvent>(jsonData);
        if (evt != null)
        {
            NotifyFileProcessing(evt.FileId.ToString(), "MoveStarted", $"Moving {evt.FileName} from {evt.FromPath} to {evt.ToPath}");
        }
    }

    private void HandleMoveProgress(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<MoveProgressEvent>(jsonData);
        if (evt != null)
        {
            NotifyFileProcessing(evt.BatchId, "BatchProgress", $"{evt.Completed}/{evt.Total} ({evt.PercentComplete:F1}%)");
        }
    }

    private void HandleMoveCompleted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<MoveCompletedEvent>(jsonData);
        if (evt != null)
        {
            NotifyFileProcessing(evt.FileId.ToString(), "Completed", $"File {evt.FileName} moved successfully");
        }
    }

    private void HandleTrainingStarted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<TrainingStartedEvent>(jsonData);
        if (evt != null)
        {
            NotifyJobProgress("training", "Training started", 0);
        }
    }

    private void HandleTrainingCompleted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<TrainingCompletedEvent>(jsonData);
        if (evt != null)
        {
            NotifyJobProgress("training", $"Training completed with {evt.Accuracy:F2} accuracy", 100);
        }
    }

    private void HandleBatchStarted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<BatchStartedEvent>(jsonData);
        if (evt != null)
        {
            NotifyFileProcessing(evt.BatchId, "BatchStarted", $"Processing {evt.TotalFiles} files");
        }
    }

    private void HandleBatchProgress(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<BatchProgressEvent>(jsonData);
        if (evt != null)
        {
            NotifyFileProcessing(evt.BatchId, "BatchProgress", $"{evt.Processed}/{evt.Total} files processed ({evt.Succeeded} succeeded, {evt.Failed} failed)");
        }
    }

    private void HandleBatchCompleted(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<BatchCompletedEvent>(jsonData);
        if (evt != null)
        {
            NotifyFileProcessing(evt.BatchId, "BatchCompleted", $"Batch completed: {evt.Succeeded}/{evt.Total} succeeded, {evt.Failed} failed");
        }
    }

    private void HandleBatchFailed(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<BatchCompletedEvent>(jsonData);
        if (evt != null)
        {
            NotifyErrors("Batch", "BatchFailed", $"Batch {evt.BatchId} failed");
        }
    }

    private void HandleError(string eventType, string jsonData)
    {
        var evt = JsonSerializer.Deserialize<ErrorEvent>(jsonData);
        if (evt != null)
        {
            NotifyErrors(evt.FileId ?? "Unknown", evt.EventType, evt.Message);
        }
    }

    private void HandleConnected(string jsonData)
    {
        var evt = JsonSerializer.Deserialize<ConnectedEvent>(jsonData);
        if (evt != null)
        {
            Console.WriteLine($"[SSE Service] Connected with client ID: {evt.ClientId}");
        }
    }

    // Subscription methods
    public IDisposable SubscribeToFileDiscovery(Action<string, string, DateTime> handler)
    {
        _fileDiscoveryHandlers.Add(handler);
        return new Subscription(() => _fileDiscoveryHandlers.Remove(handler));
    }

    public IDisposable SubscribeToFileProcessing(Action<string, string, string> handler)
    {
        _fileProcessingHandlers.Add(handler);
        return new Subscription(() => _fileProcessingHandlers.Remove(handler));
    }

    public IDisposable SubscribeToSystemStatus(Action<string, string, string> handler)
    {
        _systemStatusHandlers.Add(handler);
        return new Subscription(() => _systemStatusHandlers.Remove(handler));
    }

    public IDisposable SubscribeToErrors(Action<string, string, string> handler)
    {
        _errorHandlers.Add(handler);
        return new Subscription(() => _errorHandlers.Remove(handler));
    }

    public IDisposable SubscribeToJobProgress(Action<string, string, int> handler)
    {
        _jobProgressHandlers.Add(handler);
        return new Subscription(() => _jobProgressHandlers.Remove(handler));
    }

    // Notification helpers
    private void NotifyFileDiscovery(string fileName, string filePath, DateTime discoveredAt)
    {
        foreach (var handler in _fileDiscoveryHandlers.ToList())
        {
            try
            {
                handler(fileName, filePath, discoveredAt);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SSE Service] Error in file discovery handler: {ex.Message}");
            }
        }
    }

    private void NotifyFileProcessing(string fileHash, string status, string details)
    {
        foreach (var handler in _fileProcessingHandlers.ToList())
        {
            try
            {
                handler(fileHash, status, details);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SSE Service] Error in file processing handler: {ex.Message}");
            }
        }
    }

    private void NotifySystemStatus(string component, string status, string message)
    {
        foreach (var handler in _systemStatusHandlers.ToList())
        {
            try
            {
                handler(component, status, message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SSE Service] Error in system status handler: {ex.Message}");
            }
        }
    }

    private void NotifyErrors(string source, string errorType, string message)
    {
        foreach (var handler in _errorHandlers.ToList())
        {
            try
            {
                handler(source, errorType, message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SSE Service] Error in error handler: {ex.Message}");
            }
        }
    }

    private void NotifyJobProgress(string jobType, string message, int progress)
    {
        foreach (var handler in _jobProgressHandlers.ToList())
        {
            try
            {
                handler(jobType, message, progress);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SSE Service] Error in job progress handler: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        _dotNetRef?.Dispose();

        if (_sseModule != null)
        {
            await _sseModule.DisposeAsync();
        }

        _fileDiscoveryHandlers.Clear();
        _fileProcessingHandlers.Clear();
        _systemStatusHandlers.Clear();
        _errorHandlers.Clear();
        _jobProgressHandlers.Clear();
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            _unsubscribe();
        }
    }
}
