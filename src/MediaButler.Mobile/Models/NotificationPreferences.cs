namespace MediaButler.Mobile.Models;

/// <summary>
/// User-specific notification preferences.
/// Stored locally and overrides NotificationSettings from appsettings.json.
/// Follows "Simple Made Easy" - clear user preferences separate from config.
/// </summary>
public class NotificationPreferences
{
    /// <summary>
    /// User preference for toast notifications.
    /// Null = use default from settings, true/false = user override.
    /// </summary>
    public bool? EnableToasts { get; set; }

    /// <summary>
    /// User-selected notification types to show.
    /// Empty = use default from settings.
    /// </summary>
    public List<string> EnabledTypes { get; set; } = new();

    /// <summary>
    /// User quiet hours preference.
    /// Null = use default from settings.
    /// </summary>
    public QuietHoursPreferences? QuietHours { get; set; }

    /// <summary>
    /// User sound preference.
    /// Null = use default from settings.
    /// </summary>
    public bool? Sound { get; set; }

    /// <summary>
    /// User vibration preference.
    /// Null = use default from settings.
    /// </summary>
    public bool? Vibration { get; set; }

    /// <summary>
    /// Gets effective setting (user preference or default).
    /// </summary>
    public bool GetEffectiveToastsEnabled(NotificationSettings defaults)
        => EnableToasts ?? defaults.EnableToasts;

    /// <summary>
    /// Gets effective enabled types (user preference or default).
    /// </summary>
    public List<string> GetEffectiveEnabledTypes(NotificationSettings defaults)
        => EnabledTypes.Any() ? EnabledTypes : defaults.EnabledTypes;

    /// <summary>
    /// Gets effective sound setting (user preference or default).
    /// </summary>
    public bool GetEffectiveSound(NotificationSettings defaults)
        => Sound ?? defaults.Sound;

    /// <summary>
    /// Gets effective vibration setting (user preference or default).
    /// </summary>
    public bool GetEffectiveVibration(NotificationSettings defaults)
        => Vibration ?? defaults.Vibration;

    /// <summary>
    /// Checks if currently in quiet hours (user preference or default).
    /// </summary>
    public bool IsQuietTime(NotificationSettings defaults)
    {
        var quietHours = QuietHours ?? new QuietHoursPreferences
        {
            Enabled = defaults.QuietHours.Enabled,
            StartTime = defaults.QuietHours.StartTime,
            EndTime = defaults.QuietHours.EndTime
        };

        return quietHours.IsQuietTime();
    }
}

/// <summary>
/// User-specific quiet hours preferences.
/// </summary>
public class QuietHoursPreferences
{
    public bool? Enabled { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }

    public bool IsQuietTime()
    {
        if (Enabled != true) return false;
        if (string.IsNullOrEmpty(StartTime) || string.IsNullOrEmpty(EndTime)) return false;

        try
        {
            var now = DateTime.Now.TimeOfDay;
            var start = TimeSpan.Parse(StartTime);
            var end = TimeSpan.Parse(EndTime);

            if (start > end)
            {
                return now >= start || now <= end;
            }

            return now >= start && now <= end;
        }
        catch
        {
            return false;
        }
    }
}
