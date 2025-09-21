using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using MediaButler.Web.Models;
using System.Collections.Concurrent;

namespace MediaButler.Web.Services;

/// <summary>
/// Centralized SignalR notification service implementation
/// Manages connection lifecycle, subscriptions, and automatic reconnection
/// Follows "Simple Made Easy" principles with clear state management
/// </summary>
public class SignalRNotificationService : ISignalRNotificationService, IAsyncDisposable
{
    private readonly ILogger<SignalRNotificationService> _logger;
    private readonly ApiSettings _apiSettings;
    private HubConnection? _hubConnection;
    private readonly ConcurrentDictionary<string, List<IDisposable>> _subscriptions = new();
    private volatile bool _disposed = false;
    private volatile bool _autoReconnectEnabled = true;

    // Connection statistics
    private DateTime? _connectedAt;
    private DateTime? _lastMessageAt;
    private int _reconnectAttempts = 0;
    private string? _lastError;
    private DateTime? _lastErrorAt;

    // Events
    public event EventHandler<HubConnectionState>? ConnectionStateChanged;
    public event EventHandler<Exception>? ConnectionError;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public SignalRNotificationService(
        ILogger<SignalRNotificationService> logger,
        IOptions<ApiSettings> apiSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiSettings = apiSettings?.Value ?? throw new ArgumentNullException(nameof(apiSettings));

        InitializeConnection();
    }

    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => ConnectionState == HubConnectionState.Connected;
    public bool AutoReconnectEnabled => _autoReconnectEnabled;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        // Check connection state without locking - race conditions are handled by SignalR
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            _logger.LogDebug("SignalR connection already established");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SignalR connection to {Url}", GetNotificationsUrl());

            if (_hubConnection == null)
                InitializeConnection();

            await _hubConnection!.StartAsync(cancellationToken);

            _connectedAt = DateTime.UtcNow;
            _reconnectAttempts = 0;
            _lastError = null;
            _lastErrorAt = null;

            _logger.LogInformation("SignalR connection established successfully");
            OnConnectionStateChanged(HubConnectionState.Connected);
            Connected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _lastErrorAt = DateTime.UtcNow;
            _reconnectAttempts++;

            _logger.LogError(ex, "Failed to start SignalR connection (attempt {Attempts})", _reconnectAttempts);
            ConnectionError?.Invoke(this, ex);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _hubConnection == null)
            return;

        try
        {
            _logger.LogInformation("Stopping SignalR connection");

            // Disable auto-reconnect before stopping
            _autoReconnectEnabled = false;

            await _hubConnection.StopAsync(cancellationToken);

            _logger.LogInformation("SignalR connection stopped");
            OnConnectionStateChanged(HubConnectionState.Disconnected);
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error occurred while stopping SignalR connection");
        }
    }

    public IDisposable SubscribeToFileDiscovery(Action<string, string, DateTime> handler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        return SubscribeToNotification("FileDiscoveryNotification", handler, "FileDiscovery");
    }

    public IDisposable SubscribeToFileProcessing(Action<string, string, string> handler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        return SubscribeToNotification("FileProcessingNotification", handler, "FileProcessing");
    }

    public IDisposable SubscribeToSystemStatus(Action<string, string, string> handler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        return SubscribeToNotification("SystemStatusNotification", handler, "SystemStatus");
    }

    public IDisposable SubscribeToErrors(Action<string, string, string> handler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        return SubscribeToNotification("ErrorNotification", handler, "Error");
    }

    public void SetAutoReconnect(bool enabled)
    {
        _autoReconnectEnabled = enabled;
        _logger.LogDebug("Auto-reconnect {Status}", enabled ? "enabled" : "disabled");
    }

    public SignalRConnectionStats GetConnectionStats()
    {
        var connectedTime = _connectedAt.HasValue && IsConnected
            ? DateTime.UtcNow - _connectedAt.Value
            : (TimeSpan?)null;

        return new SignalRConnectionStats
        {
            ConnectedAt = _connectedAt,
            LastMessageAt = _lastMessageAt,
            ReconnectAttempts = _reconnectAttempts,
            TotalConnectedTime = connectedTime,
            CurrentState = ConnectionState,
            LastError = _lastError,
            LastErrorAt = _lastErrorAt
        };
    }

    private void InitializeConnection()
    {
        try
        {
            var hubUrl = GetNotificationsUrl();
            _logger.LogDebug("Initializing SignalR connection to {Url}", hubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(null);
                })
                .WithAutomaticReconnect(new CustomRetryPolicy())
                .Build();

            SetupConnectionEventHandlers();
            _logger.LogDebug("SignalR connection initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SignalR connection");
            throw;
        }
    }

    private void SetupConnectionEventHandlers()
    {
        if (_hubConnection == null) return;

        _hubConnection.Closed += async (error) =>
        {
            _logger.LogWarning("SignalR connection closed. Error: {Error}", error?.Message ?? "None");
            OnConnectionStateChanged(HubConnectionState.Disconnected);
            Disconnected?.Invoke(this, EventArgs.Empty);

            if (error != null)
            {
                _lastError = error.Message;
                _lastErrorAt = DateTime.UtcNow;
                ConnectionError?.Invoke(this, error);
            }

            // Attempt reconnection if enabled and not disposed
            if (_autoReconnectEnabled && !_disposed)
            {
                await TryReconnectAsync();
            }
        };

        _hubConnection.Reconnecting += (error) =>
        {
            _logger.LogInformation("SignalR attempting to reconnect. Error: {Error}", error?.Message ?? "None");
            OnConnectionStateChanged(HubConnectionState.Reconnecting);
            _reconnectAttempts++;
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger.LogInformation("SignalR reconnected successfully. Connection ID: {ConnectionId}", connectionId);
            _connectedAt = DateTime.UtcNow;
            OnConnectionStateChanged(HubConnectionState.Connected);
            Connected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        };
    }

    private async Task TryReconnectAsync()
    {
        if (_disposed || !_autoReconnectEnabled)
            return;

        const int maxRetries = 5;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (_disposed || !_autoReconnectEnabled)
                break;

            try
            {
                _logger.LogInformation("Attempting manual reconnection {Attempt}/{MaxAttempts}", attempt, maxRetries);
                await StartAsync();
                return; // Success
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Manual reconnection attempt {Attempt} failed", attempt);

                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay);
                }
            }
        }

        _logger.LogError("All manual reconnection attempts failed");
    }

    private IDisposable SubscribeToNotification(string methodName, Delegate handler, string category)
    {
        if (_hubConnection == null)
            throw new InvalidOperationException("SignalR connection not initialized");

        // Create subscription wrapper with common error handling and logging
        var subscription = CreateNotificationSubscription(methodName, handler, category);

        // Track subscription for cleanup
        _subscriptions.AddOrUpdate(category,
            new List<IDisposable> { subscription },
            (key, existingList) =>
            {
                existingList.Add(subscription);
                return existingList;
            });

        _logger.LogDebug("Subscribed to {Category} notifications", category);

        return new SubscriptionWrapper(subscription, () =>
        {
            if (_subscriptions.TryGetValue(category, out var list))
            {
                list.Remove(subscription);
                if (list.Count == 0)
                {
                    _subscriptions.TryRemove(category, out _);
                }
            }
            _logger.LogDebug("Unsubscribed from {Category} notifications", category);
        });
    }

    private IDisposable CreateNotificationSubscription(string methodName, Delegate handler, string category)
    {
        // Use SignalR's built-in On method with error handling wrapper
        return handler switch
        {
            Action<string, string, DateTime> h => _hubConnection!.On(methodName, (string a1, string a2, DateTime a3) =>
            {
                try
                {
                    _lastMessageAt = DateTime.UtcNow;
                    _logger.LogDebug("Received {Category} notification: {Method}", category, methodName);
                    h(a1, a2, a3);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling {Category} notification", category);
                }
            }),
            Action<string, string, string> h => _hubConnection!.On(methodName, (string a1, string a2, string a3) =>
            {
                try
                {
                    _lastMessageAt = DateTime.UtcNow;
                    _logger.LogDebug("Received {Category} notification: {Method}", category, methodName);
                    h(a1, a2, a3);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling {Category} notification", category);
                }
            }),
            _ => throw new ArgumentException($"Unsupported handler type: {handler.GetType()}")
        };
    }

    private void OnConnectionStateChanged(HubConnectionState newState)
    {
        _logger.LogDebug("SignalR connection state changed to {State}", newState);
        ConnectionStateChanged?.Invoke(this, newState);
    }

    private string GetNotificationsUrl()
    {
        var baseUrl = _apiSettings.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/notifications";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _autoReconnectEnabled = false;

        _logger.LogInformation("Disposing SignalR notification service");

        // Clear all subscriptions
        foreach (var subscriptionList in _subscriptions.Values)
        {
            foreach (var subscription in subscriptionList)
            {
                subscription?.Dispose();
            }
        }
        _subscriptions.Clear();

        // Dispose connection
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing SignalR connection");
            }
        }

        _logger.LogInformation("SignalR notification service disposed");
    }
}

/// <summary>
/// Custom retry policy for SignalR automatic reconnection
/// Implements exponential backoff with reasonable limits
/// </summary>
public class CustomRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // Stop retrying after 10 attempts
        if (retryContext.PreviousRetryCount >= 10)
            return null;

        // Exponential backoff: 1s, 2s, 4s, 8s, 16s, then 30s max
        var delay = Math.Min(30, Math.Pow(2, retryContext.PreviousRetryCount));
        return TimeSpan.FromSeconds(delay);
    }
}

/// <summary>
/// Wrapper for subscription disposables to enable cleanup tracking
/// </summary>
internal class SubscriptionWrapper : IDisposable
{
    private readonly IDisposable _subscription;
    private readonly Action _onDispose;
    private bool _disposed = false;

    public SubscriptionWrapper(IDisposable subscription, Action onDispose)
    {
        _subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscription.Dispose();
        _onDispose();
    }
}