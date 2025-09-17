namespace MediaButler.Web.Services.RealTime;

/// <summary>
/// Simple offline service following "Simple Made Easy" principles.
/// Provides graceful degradation when real-time features are unavailable.
/// </summary>
public interface IOfflineService
{
    bool IsOfflineMode { get; }
    void EnableOfflineMode();
    void DisableOfflineMode();
    Task<T?> ExecuteWithFallback<T>(Func<Task<T>> primaryAction, Func<Task<T>> fallbackAction);
    Task ExecuteWithFallback(Func<Task> primaryAction, Func<Task> fallbackAction);
    event Action<bool> OfflineModeChanged;
}

public class OfflineService : IOfflineService
{
    private readonly ILogger<OfflineService> _logger;
    private readonly IConnectionManager _connectionManager;
    
    public bool IsOfflineMode { get; private set; }
    public event Action<bool>? OfflineModeChanged;

    public OfflineService(ILogger<OfflineService> logger, IConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        
        // Automatically enable offline mode when connection is lost
        _connectionManager.HealthChanged += OnConnectionHealthChanged;
    }

    public void EnableOfflineMode()
    {
        if (!IsOfflineMode)
        {
            IsOfflineMode = true;
            _logger.LogInformation("Offline mode enabled");
            OfflineModeChanged?.Invoke(true);
        }
    }

    public void DisableOfflineMode()
    {
        if (IsOfflineMode)
        {
            IsOfflineMode = false;
            _logger.LogInformation("Offline mode disabled");
            OfflineModeChanged?.Invoke(false);
        }
    }

    public async Task<T?> ExecuteWithFallback<T>(Func<Task<T>> primaryAction, Func<Task<T>> fallbackAction)
    {
        if (IsOfflineMode || !_connectionManager.IsConnected)
        {
            try
            {
                _logger.LogDebug("Executing fallback action due to offline mode");
                return await fallbackAction();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback action failed");
                return default;
            }
        }

        try
        {
            return await primaryAction();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary action failed, attempting fallback");
            
            try
            {
                return await fallbackAction();
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback action also failed");
                return default;
            }
        }
    }

    public async Task ExecuteWithFallback(Func<Task> primaryAction, Func<Task> fallbackAction)
    {
        if (IsOfflineMode || !_connectionManager.IsConnected)
        {
            try
            {
                _logger.LogDebug("Executing fallback action due to offline mode");
                await fallbackAction();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback action failed");
                return;
            }
        }

        try
        {
            await primaryAction();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary action failed, attempting fallback");
            
            try
            {
                await fallbackAction();
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback action also failed");
            }
        }
    }

    private void OnConnectionHealthChanged(ConnectionHealth health)
    {
        switch (health)
        {
            case ConnectionHealth.Offline:
            case ConnectionHealth.Degraded:
                EnableOfflineMode();
                break;
                
            case ConnectionHealth.Healthy:
                DisableOfflineMode();
                break;
                
            case ConnectionHealth.Reconnecting:
                // Keep current offline state during reconnection
                break;
        }
    }
}