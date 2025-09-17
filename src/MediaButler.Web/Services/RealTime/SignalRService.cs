using Microsoft.AspNetCore.SignalR.Client;
using MediaButler.Web.Services.Events;
using MediaButler.Web.Services.State;

namespace MediaButler.Web.Services.RealTime;

/// <summary>
/// Simple SignalR service following "Simple Made Easy" principles.
/// Handles real-time communication without complecting with business logic.
/// </summary>
public interface ISignalRService
{
    Task StartAsync();
    Task StopAsync();
    bool IsConnected { get; }
    event Action<bool> ConnectionStateChanged;
    event Action<string, object?> MessageReceived;
}

public class SignalRService : ISignalRService, IAsyncDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IStateService _stateService;
    private readonly ILogger<SignalRService> _logger;
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;
    
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public event Action<bool>? ConnectionStateChanged;
    public event Action<string, object?>? MessageReceived;

    public SignalRService(
        IEventBus eventBus, 
        IStateService stateService, 
        ILogger<SignalRService> logger,
        IConfiguration configuration)
    {
        _eventBus = eventBus;
        _stateService = stateService;
        _logger = logger;
        
        // Get SignalR hub URL from configuration
        var baseUrl = configuration.GetValue<string>("ApiBaseUrl") ?? "http://localhost:5000";
        _hubUrl = $"{baseUrl.TrimEnd('/')}/mediahub";
    }

    public async Task StartAsync()
    {
        if (_hubConnection != null)
        {
            await StopAsync();
        }

        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            // Configure event handlers
            ConfigureEventHandlers();

            await _hubConnection.StartAsync();
            
            _logger.LogInformation("SignalR connection started");
            OnConnectionStateChanged(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
            OnConnectionStateChanged(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                
                _logger.LogInformation("SignalR connection stopped");
                OnConnectionStateChanged(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping SignalR connection");
            }
        }
    }

    private void ConfigureEventHandlers()
    {
        if (_hubConnection == null) return;

        // Connection state events
        _hubConnection.Reconnecting += (ex) =>
        {
            _logger.LogWarning("SignalR connection lost, attempting to reconnect...");
            OnConnectionStateChanged(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger.LogInformation("SignalR connection restored");
            OnConnectionStateChanged(true);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += (ex) =>
        {
            _logger.LogWarning(ex, "SignalR connection closed");
            OnConnectionStateChanged(false);
            return Task.CompletedTask;
        };

        // File processing events
        _hubConnection.On<string, string, decimal>("FileClassified", (fileHash, category, confidence) =>
        {
            OnMessageReceived("FileClassified", new { FileHash = fileHash, Category = category, Confidence = confidence });
            _eventBus.Publish(new FileClassifiedEvent(fileHash, category, confidence));
        });

        _hubConnection.On<string, string>("FileMoved", (fileHash, newPath) =>
        {
            OnMessageReceived("FileMoved", new { FileHash = fileHash, NewPath = newPath });
            _eventBus.Publish(new FileMovedEvent(fileHash, newPath));
        });

        _hubConnection.On<string, string>("FileError", (fileHash, error) =>
        {
            OnMessageReceived("FileError", new { FileHash = fileHash, Error = error });
            _eventBus.Publish(new FileErrorEvent(fileHash, error));
        });

        // System events
        _hubConnection.On<string[]>("ScanStarted", (paths) =>
        {
            OnMessageReceived("ScanStarted", new { Paths = paths });
            _eventBus.Publish(new ScanStartedEvent(paths));
        });

        _hubConnection.On<int>("ScanCompleted", (filesFound) =>
        {
            OnMessageReceived("ScanCompleted", new { FilesFound = filesFound });
            _eventBus.Publish(new ScanCompletedEvent(filesFound));
        });

        // Statistics updates
        _hubConnection.On<int, int, int>("StatisticsUpdated", (totalFiles, pendingFiles, processedToday) =>
        {
            OnMessageReceived("StatisticsUpdated", new { TotalFiles = totalFiles, PendingFiles = pendingFiles, ProcessedToday = processedToday });
            _stateService.Dispatch(new StateEvent.StatisticsUpdated(totalFiles, pendingFiles, processedToday));
        });

        // General system errors
        _hubConnection.On<string>("SystemError", (error) =>
        {
            OnMessageReceived("SystemError", new { Error = error });
            _eventBus.Publish(new SystemErrorEvent(error));
        });
    }

    private void OnConnectionStateChanged(bool isConnected)
    {
        ConnectionStateChanged?.Invoke(isConnected);
        _stateService.Dispatch(new StateEvent.ConnectionChanged(isConnected));
    }

    private void OnMessageReceived(string messageType, object? data)
    {
        MessageReceived?.Invoke(messageType, data);
        _logger.LogDebug("Received SignalR message: {MessageType}", messageType);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}