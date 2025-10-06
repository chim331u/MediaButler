namespace MediaButler.Mobile.Models;

/// <summary>
/// Represents a single API endpoint configuration.
/// Immutable value object following "Simple Made Easy" principles.
/// </summary>
public record ApiEndpoint
{
    /// <summary>
    /// User-friendly name for this endpoint (e.g., "Primary", "Home Server", "Remote").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Protocol: "http" or "https".
    /// </summary>
    public required string Protocol { get; init; }

    /// <summary>
    /// Host: IP address (e.g., "192.168.1.5") or hostname (e.g., "mediabutler.local").
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Port number (e.g., 5271, 30139, 443).
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Priority for endpoint selection (1 = highest priority).
    /// Lower numbers are tried first during failover.
    /// </summary>
    public int Priority { get; init; } = 1;

    /// <summary>
    /// Whether this endpoint is enabled for connection attempts.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Builds the full base URL for this endpoint.
    /// </summary>
    /// <returns>Full URL in format: {protocol}://{host}:{port}</returns>
    public string GetBaseUrl() => $"{Protocol}://{Host}:{Port}";

    /// <summary>
    /// Validates that the endpoint configuration is correct.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Name)) return false;
        if (Protocol != "http" && Protocol != "https") return false;
        if (string.IsNullOrWhiteSpace(Host)) return false;
        if (Port <= 0 || Port > 65535) return false;
        return true;
    }
}
