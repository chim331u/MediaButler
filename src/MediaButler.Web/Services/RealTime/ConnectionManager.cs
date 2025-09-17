namespace MediaButler.Web.Services.RealTime;

/// <summary>
/// Simple connection management following "Simple Made Easy" principles.
/// Manages connection lifecycle without complecting with business logic.
/// </summary>
public interface IConnectionManager
{
    Task InitializeAsync();
    Task ReconnectAsync();
    bool IsConnected { get; }
    ConnectionHealth Health { get; }
    event Action<ConnectionHealth> HealthChanged;
}

public enum ConnectionHealth
{
    Healthy,
    Reconnecting,
    Degraded,
    Offline
}

public class ConnectionManager : IConnectionManager, IDisposable
{
    private readonly ISignalRService _signalRService;
    private readonly ILogger<ConnectionManager> _logger;
    private readonly Timer _healthCheckTimer;
    private readonly Timer _reconnectTimer;
    
    public bool IsConnected => _signalRService.IsConnected;
    public ConnectionHealth Health { get; private set; } = ConnectionHealth.Offline;
    
    public event Action<ConnectionHealth>? HealthChanged;

    private int _reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 5;
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromSeconds(10);

    public ConnectionManager(ISignalRService signalRService, ILogger<ConnectionManager> logger)
    {
        _signalRService = signalRService;
        _logger = logger;
        
        // Subscribe to connection state changes
        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;
        
        // Initialize timers (but don't start them yet)
        _healthCheckTimer = new Timer(CheckConnectionHealth, null, Timeout.Infinite, Timeout.Infinite);
        _reconnectTimer = new Timer(AttemptReconnect, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing connection manager");
            
            await _signalRService.StartAsync();
            
            // Start health monitoring
            _healthCheckTimer.Change(_healthCheckInterval, _healthCheckInterval);
            
            _logger.LogInformation("Connection manager initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize connection manager");
            SetHealth(ConnectionHealth.Offline);
            
            // Start reconnection attempts
            StartReconnectionAttempts();
        }
    }

    public async Task ReconnectAsync()
    {
        if (IsConnected) return;

        try
        {
            _logger.LogInformation("Manual reconnection requested");
            SetHealth(ConnectionHealth.Reconnecting);
            
            await _signalRService.StartAsync();
            _reconnectAttempts = 0;
            
            _logger.LogInformation("Manual reconnection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual reconnection failed");
            SetHealth(ConnectionHealth.Offline);
        }
    }

    private void OnConnectionStateChanged(bool isConnected)
    {
        if (isConnected)
        {
            _logger.LogInformation("SignalR connection established");
            SetHealth(ConnectionHealth.Healthy);
            _reconnectAttempts = 0;
            
            // Stop reconnection attempts
            _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        else
        {
            _logger.LogWarning("SignalR connection lost");
            SetHealth(ConnectionHealth.Offline);
            
            // Start reconnection attempts
            StartReconnectionAttempts();
        }
    }

    private void StartReconnectionAttempts()
    {
        if (_reconnectAttempts >= MaxReconnectAttempts)
        {
            _logger.LogError("Maximum reconnection attempts reached, giving up");
            SetHealth(ConnectionHealth.Offline);
            return;
        }

        SetHealth(ConnectionHealth.Reconnecting);
        _reconnectTimer.Change(_reconnectInterval, _reconnectInterval);
    }

    private async void AttemptReconnect(object? state)
    {
        if (IsConnected) return;

        _reconnectAttempts++;
        
        try
        {
            _logger.LogInformation("Reconnection attempt {Attempt}/{MaxAttempts}", _reconnectAttempts, MaxReconnectAttempts);
            
            await _signalRService.StartAsync();
            
            _logger.LogInformation("Reconnection successful");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reconnection attempt {Attempt} failed", _reconnectAttempts);
            
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                _logger.LogError("All reconnection attempts exhausted");
                SetHealth(ConnectionHealth.Offline);
                _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
    }

    private void CheckConnectionHealth(object? state)
    {
        try
        {
            if (!IsConnected)
            {
                if (Health != ConnectionHealth.Reconnecting && Health != ConnectionHealth.Offline)
                {
                    _logger.LogWarning("Connection health check failed - connection lost");
                    SetHealth(ConnectionHealth.Offline);
                    StartReconnectionAttempts();
                }
                return;
            }

            // Connection is active - ensure we're in healthy state
            if (Health != ConnectionHealth.Healthy)
            {
                _logger.LogInformation("Connection health restored");
                SetHealth(ConnectionHealth.Healthy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connection health check");
            SetHealth(ConnectionHealth.Degraded);
        }
    }

    private void SetHealth(ConnectionHealth newHealth)
    {
        if (Health != newHealth)
        {
            var previousHealth = Health;
            Health = newHealth;
            
            _logger.LogInformation("Connection health changed from {PreviousHealth} to {NewHealth}", 
                previousHealth, newHealth);
            
            HealthChanged?.Invoke(newHealth);
        }
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
        _reconnectTimer?.Dispose();
        _signalRService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}