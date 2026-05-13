using MediaButler.Mobile.Components.Interface;
using MediaButler.Mobile.Components.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaButler.Mobile.Components.Service;

/// <summary>
/// Secure configuration service with validation and caching.
/// Reads API URL from active NetworkSetting (Settings page) instead of appsettings.json.
/// Enforces HTTPS for remote connections and validates API settings.
/// Following "Simple Made Easy": Single responsibility - configuration retrieval only.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IUtilityServices _utilityServices;
    private string? _cachedApiBaseUrl;

    public ConfigurationService(
        IConfiguration configuration,
        ILogger<ConfigurationService> logger,
        IUtilityServices utilityServices)
    {
        _configuration = configuration;
        _logger = logger;
        _utilityServices = utilityServices;
    }

    /// <summary>
    /// Gets the validated API base URL from active NetworkSetting.
    /// Validates on first access and caches the result.
    /// </summary>
    public string ApiBaseUrl => _cachedApiBaseUrl ??= ValidateAndGetApiUrl();

    /// <summary>
    /// Gets the API timeout in seconds.
    /// Default: 30 seconds.
    /// </summary>
    public int ApiTimeout =>
        _configuration.GetValue<int>("ApiSettings:Timeout", 30);

    /// <summary>
    /// Gets whether response caching is enabled.
    /// Default: false.
    /// </summary>
    public bool EnableCaching =>
        _configuration.GetValue<bool>("ApiSettings:EnableCaching", false);

    /// <summary>
    /// Indicates whether the configuration is valid.
    /// </summary>
    public bool IsValidConfiguration =>
        !string.IsNullOrWhiteSpace(_cachedApiBaseUrl);

    /// <summary>
    /// Validates and retrieves the API base URL from active NetworkSetting.
    /// Enforces security rules:
    /// - URL must be configured in NetworkSettings
    /// - URL must be valid absolute URI
    /// - HTTPS required for remote hosts
    /// - HTTP allowed only for localhost/local network
    /// </summary>
    private string ValidateAndGetApiUrl()
    {
        // Get URL from active NetworkSetting (managed in Settings page)
        var url = _utilityServices.SetApiUrl();

        // Validation 1: URL not empty
        if (string.IsNullOrWhiteSpace(url))
        {
            var error = "API URL not configured. Please configure network settings in the Settings page.";
            _logger.LogError(error);
            throw new InvalidOperationException(error);
        }

        // Validation 2: Valid URI format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var error = $"Invalid API URL format: {url}. Must be absolute URL (e.g., https://192.168.1.100:5000)";
            _logger.LogError(error);
            throw new InvalidOperationException(error);
        }

        // Validation 3: HTTPS required for remote hosts
        if (uri.Scheme != "https" && !IsLocalAddress(uri))
        {
            var error = $"Only HTTPS allowed for remote APIs. URL: {url}. Local network addresses can use HTTP.";
            _logger.LogError(error);
            throw new InvalidOperationException(error);
        }

        // Validation 4: HTTP allowed only for localhost/local network (with warning)
        if (uri.Scheme == "http" && !IsLocalAddress(uri))
        {
            _logger.LogWarning(
                "⚠️  HTTP connection to remote host is insecure: {Url}. Consider using HTTPS.", url);
        }

        _logger.LogInformation("✅ API configuration validated successfully: {Url}", url);
        return url;
    }

    /// <summary>
    /// Checks if the URI points to a local network address (RFC 1918).
    /// Supports localhost, 127.0.0.1, and private IP ranges.
    /// </summary>
    private bool IsLocalAddress(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();

        // Localhost
        if (host == "localhost" || host == "127.0.0.1")
            return true;

        // Class C private network (192.168.0.0/16)
        if (host.StartsWith("192.168."))
            return true;

        // Class A private network (10.0.0.0/8)
        if (host.StartsWith("10."))
            return true;

        // Class B private network (172.16.0.0/12)
        if (host.StartsWith("172.") && IsPrivateClassB(host))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if the host is in the Class B private network range (172.16.0.0 - 172.31.255.255).
    /// </summary>
    private bool IsPrivateClassB(string host)
    {
        if (!host.StartsWith("172."))
            return false;

        var parts = host.Split('.');
        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[1], out var secondOctet))
            return false;

        // Class B private range: 172.16 to 172.31
        return secondOctet >= 16 && secondOctet <= 31;
    }
}
