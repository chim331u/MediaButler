using MediaButler.Web.Components.UI;

namespace MediaButler.Web.Services.Notifications;

/// <summary>
/// Simple notification service following "Simple Made Easy" principles.
/// Manages notifications without complecting with UI concerns.
/// </summary>
public interface INotificationService
{
    void ShowSuccess(string title, string? message = null, TimeSpan? duration = null);
    void ShowError(string title, string? message = null, TimeSpan? duration = null);
    void ShowWarning(string title, string? message = null, TimeSpan? duration = null);
    void ShowInfo(string title, string? message = null, TimeSpan? duration = null);
    void Clear();
    event Action<NotificationItem> NotificationAdded;
    event Action<string> NotificationRemoved;
    event Action NotificationsCleared;
}

public record NotificationItem(
    string Id,
    string Title,
    string? Message,
    NotificationToast.NotificationLevel Level,
    TimeSpan? Duration,
    DateTime CreatedAt);

public class NotificationService : INotificationService
{
    private readonly Dictionary<string, NotificationItem> _notifications = new();
    private readonly object _lock = new();

    public event Action<NotificationItem>? NotificationAdded;
    public event Action<string>? NotificationRemoved;
    public event Action? NotificationsCleared;

    public void ShowSuccess(string title, string? message = null, TimeSpan? duration = null)
    {
        AddNotification(title, message, NotificationToast.NotificationLevel.Success, duration);
    }

    public void ShowError(string title, string? message = null, TimeSpan? duration = null)
    {
        // Errors should stay longer by default
        var errorDuration = duration ?? TimeSpan.FromSeconds(10);
        AddNotification(title, message, NotificationToast.NotificationLevel.Error, errorDuration);
    }

    public void ShowWarning(string title, string? message = null, TimeSpan? duration = null)
    {
        AddNotification(title, message, NotificationToast.NotificationLevel.Warning, duration);
    }

    public void ShowInfo(string title, string? message = null, TimeSpan? duration = null)
    {
        AddNotification(title, message, NotificationToast.NotificationLevel.Info, duration);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _notifications.Clear();
        }
        
        NotificationsCleared?.Invoke();
    }

    private void AddNotification(string title, string? message, NotificationToast.NotificationLevel level, TimeSpan? duration)
    {
        var id = Guid.NewGuid().ToString();
        var notification = new NotificationItem(
            id, 
            title, 
            message, 
            level, 
            duration ?? TimeSpan.FromSeconds(5),
            DateTime.UtcNow);

        lock (_lock)
        {
            _notifications[id] = notification;
        }

        NotificationAdded?.Invoke(notification);

        // Auto-remove after duration if specified
        if (notification.Duration.HasValue)
        {
            _ = Task.Delay(notification.Duration.Value).ContinueWith(_ => RemoveNotification(id));
        }
    }

    private void RemoveNotification(string id)
    {
        lock (_lock)
        {
            if (_notifications.ContainsKey(id))
            {
                _notifications.Remove(id);
                NotificationRemoved?.Invoke(id);
            }
        }
    }
}