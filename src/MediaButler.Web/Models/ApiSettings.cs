namespace MediaButler.Web.Models;

/// <summary>
/// Configuration settings for API connections.
/// No hardcoded defaults - must be configured via appsettings.json.
/// </summary>
public class ApiSettings
{
    /// <summary>
    /// Base URL for MediaButler API. Must be configured in appsettings.json.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Validates that the API settings are properly configured.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(BaseUrl) && Uri.IsWellFormedUriString(BaseUrl, UriKind.Absolute);
}