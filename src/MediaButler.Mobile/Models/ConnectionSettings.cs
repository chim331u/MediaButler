namespace MediaButler.Mobile.Models;

/// <summary>
/// Connection behavior settings.
/// Immutable value object.
/// </summary>
public record ConnectionSettings
{
    /// <summary>
    /// Path to health check endpoint (e.g., "/api/health").
    /// </summary>
    public string HealthCheckPath { get; init; } = "/api/health";

    /// <summary>
    /// Timeout in seconds for health check requests.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// Number of retry attempts for failed requests.
    /// </summary>
    public int RetryAttempts { get; init; } = 3;

    /// <summary>
    /// Delay in milliseconds between retry attempts.
    /// </summary>
    public int RetryDelayMs { get; init; } = 1000;
}
