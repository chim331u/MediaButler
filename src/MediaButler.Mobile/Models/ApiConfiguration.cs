namespace MediaButler.Mobile.Models;

/// <summary>
/// Complete API configuration including all endpoints and connection settings.
/// Immutable value object that represents the entire configuration state.
/// </summary>
public record ApiConfiguration
{
    /// <summary>
    /// List of configured API endpoints, ordered by priority.
    /// </summary>
    public IReadOnlyList<ApiEndpoint> ApiEndpoints { get; init; } = Array.Empty<ApiEndpoint>();

    /// <summary>
    /// Connection behavior settings.
    /// </summary>
    public ConnectionSettings ConnectionSettings { get; init; } = new();

    /// <summary>
    /// Gets all enabled endpoints sorted by priority (ascending).
    /// </summary>
    public IEnumerable<ApiEndpoint> GetEnabledEndpointsByPriority()
    {
        return ApiEndpoints
            .Where(e => e.Enabled && e.IsValid())
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.Name);
    }

    /// <summary>
    /// Creates a default configuration with a single local endpoint.
    /// </summary>
    public static ApiConfiguration CreateDefault()
    {
        return new ApiConfiguration
        {
            ApiEndpoints = new List<ApiEndpoint>
            {
                new ApiEndpoint
                {
                    Name = "Local",
                    Protocol = "http",
                    Host = "192.168.1.100",
                    Port = 5271,
                    Priority = 1,
                    Enabled = true
                }
            },
            ConnectionSettings = new ConnectionSettings()
        };
    }
}
