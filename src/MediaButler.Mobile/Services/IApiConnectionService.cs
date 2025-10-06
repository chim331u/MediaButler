using MediaButler.Mobile.Models;

namespace MediaButler.Mobile.Services;

/// <summary>
/// Service for managing API endpoint connections, health checks, and failover.
/// </summary>
public interface IApiConnectionService
{
    /// <summary>
    /// Event raised when the active endpoint changes.
    /// </summary>
    event EventHandler<ApiEndpoint>? ActiveEndpointChanged;

    /// <summary>
    /// Discovers the first working endpoint from the configuration.
    /// Tests endpoints in priority order until one succeeds.
    /// </summary>
    /// <returns>Connection result for the first successful endpoint, or failure if all fail.</returns>
    Task<ConnectionResult> DiscoverActiveEndpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests a specific endpoint's health check.
    /// </summary>
    /// <param name="endpoint">The endpoint to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection result indicating success/failure and latency.</returns>
    Task<ConnectionResult> TestEndpointAsync(ApiEndpoint endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests all enabled endpoints and returns results.
    /// </summary>
    Task<IReadOnlyList<ConnectionResult>> TestAllEndpointsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active endpoint (cached).
    /// Returns null if no active endpoint has been discovered.
    /// </summary>
    ApiEndpoint? GetActiveEndpoint();

    /// <summary>
    /// Invalidates the active endpoint cache and forces re-discovery on next request.
    /// </summary>
    void InvalidateActiveEndpoint();

    /// <summary>
    /// Gets the base URL of the active endpoint.
    /// Returns null if no active endpoint.
    /// </summary>
    string? GetActiveBaseUrl();
}
