namespace MediaButler.Mobile.Models;

/// <summary>
/// Configuration for notification behavior.
/// Loaded from appsettings.json and can be overridden by user preferences.
/// </summary>
public class NotificationSettings
{
    /// <summary>
    /// Master switch for all toast notifications.
    /// </summary>
    public bool EnableToasts { get; set; } = true;

    /// <summary>
    /// List of enabled notification types.
    /// Valid values: "notifications", "moveFilesNotifications", "jobNotifications"
    /// </summary>
    public List<string> EnabledTypes { get; set; } = new() { "jobNotifications" };

    /// <summary>
    /// Quiet hours configuration (no toasts during specified times).
    /// </summary>
    public QuietHoursSettings QuietHours { get; set; } = new();

    /// <summary>
    /// Enable notification sound.
    /// </summary>
    public bool Sound { get; set; } = true;

    /// <summary>
    /// Enable device vibration for notifications.
    /// </summary>
    public bool Vibration { get; set; } = true;

    /// <summary>
    /// Auto-reconnect SignalR on connection loss (not on background).
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Maximum reconnection attempts before giving up.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;
}

/// <summary>
/// Quiet hours configuration (no notifications during specified time range).
/// </summary>
public class QuietHoursSettings
{
    /// <summary>
    /// Enable quiet hours feature.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Start time in 24-hour format (e.g., "22:00").
    /// </summary>
    public string StartTime { get; set; } = "22:00";

    /// <summary>
    /// End time in 24-hour format (e.g., "08:00").
    /// </summary>
    public string EndTime { get; set; } = "08:00";

    /// <summary>
    /// Checks if current time is within quiet hours.
    /// </summary>
    public bool IsQuietTime()
    {
        if (!Enabled) return false;

        try
        {
            var now = DateTime.Now.TimeOfDay;
            var start = TimeSpan.Parse(StartTime);
            var end = TimeSpan.Parse(EndTime);

            // Handle overnight quiet hours (e.g., 22:00 to 08:00)
            if (start > end)
            {
                return now >= start || now <= end;
            }

            return now >= start && now <= end;
        }
        catch
        {
            return false; // Invalid time format, assume not quiet time
        }
    }
}
