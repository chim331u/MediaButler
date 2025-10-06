namespace MediaButler.Mobile.Models;

/// <summary>
/// Result of a connection test to an API endpoint.
/// Immutable value object.
/// </summary>
public record ConnectionResult
{
    /// <summary>
    /// Whether the connection test succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The endpoint that was tested.
    /// </summary>
    public required ApiEndpoint Endpoint { get; init; }

    /// <summary>
    /// Latency in milliseconds (null if connection failed).
    /// </summary>
    public long? LatencyMs { get; init; }

    /// <summary>
    /// Error message if connection failed (null if successful).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp when the test was performed.
    /// </summary>
    public DateTime TestedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful connection result.
    /// </summary>
    public static ConnectionResult Success(ApiEndpoint endpoint, long latencyMs)
    {
        return new ConnectionResult
        {
            IsSuccess = true,
            Endpoint = endpoint,
            LatencyMs = latencyMs,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Creates a failed connection result.
    /// </summary>
    public static ConnectionResult Failure(ApiEndpoint endpoint, string errorMessage)
    {
        return new ConnectionResult
        {
            IsSuccess = false,
            Endpoint = endpoint,
            LatencyMs = null,
            ErrorMessage = errorMessage
        };
    }
}
