namespace MediaButler.Mobile.Models;

/// <summary>
/// API configuration settings.
/// Loaded from appsettings.json.
/// </summary>
public class ApiSettings
{
    /// <summary>
    /// Base URL for the MediaButler API.
    /// Example: "https://192.168.1.100:5000"
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout in seconds.
    /// Default: 30 seconds.
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Enable response caching for category lists.
    /// Default: false.
    /// </summary>
    public bool EnableCaching { get; set; } = false;

    /// <summary>
    /// Validates the API settings.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) &&
        Timeout > 0;
}
