using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Data;
using MediaButler.Mobile.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaButler.Mobile.Components.Service;

/// <summary>
/// Battery-optimized SignalR notification service for MAUI.
/// Scoped lifecycle - connects on demand, disconnects on background.
/// Follows "Simple Made Easy" - clear connection lifecycle, composable subscriptions.
/// </summary>
public class SignalRNotificationService : ISignalRNotificationService, IAsyncDisposable
{
    private readonly ILogger<SignalRNotificationService> _logger;
    private readonly IConfigurationService _configService;
    private readonly NotificationSettings _settings;
    private HubConnection? _hubConnection;
    private volatile bool _disposed = false;
    private volatile bool _isConnecting = false;
    private int _reconnectAttempts = 0;

    // Event handlers storage
    private readonly List<Action<int, string, MoveFilesResults>> _fileProcessedHandlers = new();
    private readonly List<Action<string, MoveFilesResults>> _jobCompletedHandlers = new();
    private readonly List<Action<string, decimal>> _notificationHandlers = new();

    // Connection events
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<Exception>? ConnectionError;

    public SignalRNotificationService(
        ILogger<SignalRNotificationService> logger,
        IConfigurationService configService,
        IOptions<NotificationSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        // Prevent concurrent connection attempts
        if (_isConnecting)
        {
            _logger.LogDebug("Connection attempt already in progress");
            return;
        }

        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            _logger.LogDebug("SignalR already connected");
            return;
        }

        try
        {
            _isConnecting = true;

            _logger.LogInformation("Starting SignalR connection...");

            // Initialize connection if needed
            if (_hubConnection == null)
            {
                InitializeConnection();
            }

            await _hubConnection!.StartAsync(cancellationToken);

            _reconnectAttempts = 0;
            _logger.LogInformation("✅ SignalR connection established");
            Connected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _reconnectAttempts++;
            _logger.LogError(ex, "❌ Failed to start SignalR connection (attempt {Attempts})", _reconnectAttempts);
            ConnectionError?.Invoke(this, ex);

            // Retry with exponential backoff if enabled
            if (_settings.AutoReconnect && _reconnectAttempts < _settings.MaxReconnectAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, _reconnectAttempts));
                _logger.LogInformation("Retrying connection in {Delay} seconds...", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                await StartAsync(cancellationToken);
            }
            else
            {
                throw;
            }
        }
        finally
        {
            _isConnecting = false;
        }
    }

    public async Task StopAsync()
    {
        if (_disposed || _hubConnection == null)
            return;

        try
        {
            _logger.LogInformation("Stopping SignalR connection (battery optimization)");

            if (_hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.StopAsync();
            }

            _logger.LogInformation("SignalR connection stopped");
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping SignalR connection");
        }
    }

    public void OnFileProcessed(Action<int, string, MoveFilesResults> handler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _fileProcessedHandlers.Add(handler);
        _logger.LogDebug("Subscribed to file processed notifications (total subscribers: {Count})", _fileProcessedHandlers.Count);
    }

    public void OnJobCompleted(Action<string, MoveFilesResults> handler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _jobCompletedHandlers.Add(handler);
        _logger.LogDebug("Subscribed to job completed notifications (total subscribers: {Count})", _jobCompletedHandlers.Count);
    }

    public void OnNotification(Action<string, decimal> handler)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SignalRNotificationService));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _notificationHandlers.Add(handler);
        _logger.LogDebug("Subscribed to general notifications (total subscribers: {Count})", _notificationHandlers.Count);
    }

    private void InitializeConnection()
    {
        try
        {
            var hubUrl = GetNotificationsUrl();
            _logger.LogDebug("Initializing SignalR connection to {Url}", hubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new MobileRetryPolicy(_settings.MaxReconnectAttempts))
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .Build();

            SetupMessageHandlers();
            SetupConnectionEventHandlers();

            _logger.LogDebug("SignalR connection initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SignalR connection");
            throw;
        }
    }

    private void SetupMessageHandlers()
    {
        if (_hubConnection == null) return;

        // Handler for file processing notifications
        _hubConnection.On<int, string, MoveFilesResults>("moveFilesNotifications", (fileId, resultText, result) =>
        {
            try
            {
                _logger.LogDebug("📥 File processed: {FileId} - {Result}", fileId, result);

                // Notify all subscribed handlers
                foreach (var handler in _fileProcessedHandlers.ToList())
                {
                    try
                    {
                        handler(fileId, resultText, result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in file processed handler");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling moveFilesNotifications");
            }
        });

        // Handler for batch job completion notifications (PRIORITY)
        // M6: Listen for "BatchCompleted" from FileProcessingHub (not old "jobNotifications")
        _hubConnection.On<object>("BatchCompleted", (notification) =>
        {
            try
            {
                _logger.LogInformation("✅ Batch job completed notification received from FileProcessingHub");

                // Parse the notification object
                var notificationJson = System.Text.Json.JsonSerializer.Serialize(notification);
                _logger.LogDebug("Notification JSON: {Json}", notificationJson);

                // Default message and result
                string message = "Batch operation completed";
                var result = MoveFilesResults.Completed;

                // Try to extract message from notification if it's a JSON object
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(notificationJson);
                    if (doc.RootElement.TryGetProperty("Message", out var msgProp))
                    {
                        message = msgProp.GetString() ?? message;
                    }
                }
                catch
                {
                    // Use default message if parsing fails
                }

                _logger.LogInformation("Processing batch completion: {Message}", message);

                // Notify all subscribed handlers
                foreach (var handler in _jobCompletedHandlers.ToList())
                {
                    try
                    {
                        handler(message, result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in job completed handler");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling BatchCompleted notification");
            }
        });

        // Handler for general notifications
        _hubConnection.On<string, decimal>("notifications", (message, progress) =>
        {
            try
            {
                _logger.LogDebug("📢 Notification: {Message} ({Progress}%)", message, progress);

                // Notify all subscribed handlers
                foreach (var handler in _notificationHandlers.ToList())
                {
                    try
                    {
                        handler(message, progress);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in notification handler");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notifications");
            }
        });
    }

    private void SetupConnectionEventHandlers()
    {
        if (_hubConnection == null) return;

        _hubConnection.Closed += async (error) =>
        {
            _logger.LogWarning("SignalR connection closed. Error: {Error}", error?.Message ?? "None");
            Disconnected?.Invoke(this, EventArgs.Empty);

            if (error != null)
            {
                ConnectionError?.Invoke(this, error);
            }
        };

        _hubConnection.Reconnecting += (error) =>
        {
            _logger.LogInformation("SignalR reconnecting... Error: {Error}", error?.Message ?? "None");
            _reconnectAttempts++;
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger.LogInformation("✅ SignalR reconnected. Connection ID: {ConnectionId}", connectionId);
            _reconnectAttempts = 0;
            Connected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        };
    }

    private string GetNotificationsUrl()
    {
        var baseUrl = _configService.ApiBaseUrl.TrimEnd('/');
        // M6: Connect to FileProcessingHub for batch operation notifications
        return $"{baseUrl}/file-processing";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _logger.LogInformation("Disposing SignalR notification service");

        // Clear all subscriptions
        _fileProcessedHandlers.Clear();
        _jobCompletedHandlers.Clear();
        _notificationHandlers.Clear();

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
/// Battery-optimized retry policy for mobile.
/// Shorter retry attempts, faster backoff timeout.
/// </summary>
public class MobileRetryPolicy : IRetryPolicy
{
    private readonly int _maxAttempts;

    public MobileRetryPolicy(int maxAttempts = 5)
    {
        _maxAttempts = maxAttempts;
    }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // Stop retrying after max attempts
        if (retryContext.PreviousRetryCount >= _maxAttempts)
            return null;

        // Exponential backoff: 1s, 2s, 4s, 8s, 16s (max 20s for mobile)
        var delay = Math.Min(20, Math.Pow(2, retryContext.PreviousRetryCount));
        return TimeSpan.FromSeconds(delay);
    }
}
