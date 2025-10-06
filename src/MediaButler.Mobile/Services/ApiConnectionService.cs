using MediaButler.Mobile.Models;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Http;

namespace MediaButler.Mobile.Services;

/// <summary>
/// Implementation of API connection testing and failover logic.
/// Tests endpoints via health check and manages active endpoint state.
/// </summary>
public class ApiConnectionService : IApiConnectionService
{
    private readonly IApiConfigurationService _configService;
    private readonly IHttpClientFactory _httpClientFactory;
    private ApiEndpoint? _activeEndpoint;
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);

    public event EventHandler<ApiEndpoint>? ActiveEndpointChanged;

    public ApiConnectionService(
        IApiConfigurationService configService,
        IHttpClientFactory httpClientFactory)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        // Subscribe to configuration changes to invalidate active endpoint
        _configService.ConfigurationChanged += (_, _) => InvalidateActiveEndpoint();
    }

    public async Task<ConnectionResult> DiscoverActiveEndpointAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent discovery attempts
        await _discoveryLock.WaitAsync(cancellationToken);
        try
        {
            // Return cached active endpoint if available
            if (_activeEndpoint != null)
            {
                return ConnectionResult.Success(_activeEndpoint, 0);
            }

            var config = _configService.GetCurrentConfiguration();
            var enabledEndpoints = config.GetEnabledEndpointsByPriority().ToList();

            if (!enabledEndpoints.Any())
            {
                return ConnectionResult.Failure(
                    new ApiEndpoint { Name = "None", Protocol = "http", Host = "localhost", Port = 0 },
                    "No enabled endpoints configured"
                );
            }

            // Test endpoints in priority order until one succeeds
            foreach (var endpoint in enabledEndpoints)
            {
                var result = await TestEndpointAsync(endpoint, cancellationToken);

                if (result.IsSuccess)
                {
                    _activeEndpoint = endpoint;
                    ActiveEndpointChanged?.Invoke(this, endpoint);
                    return result;
                }
            }

            // All endpoints failed
            return ConnectionResult.Failure(
                enabledEndpoints.First(),
                "All configured endpoints are unreachable"
            );
        }
        finally
        {
            _discoveryLock.Release();
        }
    }

    public async Task<ConnectionResult> TestEndpointAsync(ApiEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (!endpoint.IsValid())
        {
            return ConnectionResult.Failure(endpoint, "Invalid endpoint configuration");
        }

        // Check network connectivity first
        var networkAccess = Connectivity.Current.NetworkAccess;
        if (networkAccess != NetworkAccess.Internet)
        {
            return ConnectionResult.Failure(endpoint, "No internet connection");
        }

        var config = _configService.GetCurrentConfiguration();
        var healthCheckUrl = $"{endpoint.GetBaseUrl()}{config.ConnectionSettings.HealthCheckPath}";

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(config.ConnectionSettings.TimeoutSeconds);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync(healthCheckUrl, cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return ConnectionResult.Failure(
                    endpoint,
                    $"Health check failed with status {(int)response.StatusCode}"
                );
            }

            // Optional: Validate health check response body
            var contentString = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(contentString))
            {
                return ConnectionResult.Failure(endpoint, "Health check returned empty response");
            }

            // Try to parse as JSON to verify it's a valid health check response
            try
            {
                var jsonDoc = JsonDocument.Parse(contentString);
                if (jsonDoc.RootElement.TryGetProperty("status", out var statusProp))
                {
                    var status = statusProp.GetString();
                    if (status != "healthy" && status != "Healthy")
                    {
                        return ConnectionResult.Failure(endpoint, $"Health check status: {status}");
                    }
                }
            }
            catch (JsonException)
            {
                // Health check might return plain text "Healthy" - that's okay
            }

            return ConnectionResult.Success(endpoint, stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return ConnectionResult.Failure(endpoint, $"Connection failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return ConnectionResult.Failure(endpoint, "Connection timeout");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ConnectionResult.Failure(endpoint, $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<ConnectionResult>> TestAllEndpointsAsync(CancellationToken cancellationToken = default)
    {
        var config = _configService.GetCurrentConfiguration();
        var enabledEndpoints = config.GetEnabledEndpointsByPriority().ToList();

        var tasks = enabledEndpoints.Select(endpoint => TestEndpointAsync(endpoint, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.ToList();
    }

    public ApiEndpoint? GetActiveEndpoint()
    {
        return _activeEndpoint;
    }

    public void InvalidateActiveEndpoint()
    {
        _activeEndpoint = null;
    }

    public string? GetActiveBaseUrl()
    {
        return _activeEndpoint?.GetBaseUrl();
    }
}
