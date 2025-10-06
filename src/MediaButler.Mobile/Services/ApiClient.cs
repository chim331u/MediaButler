using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Http;

namespace MediaButler.Mobile.Services;

/// <summary>
/// HTTP client wrapper that uses the active endpoint from ApiConnectionService.
/// Handles automatic failover and retry logic for API requests.
/// </summary>
public class ApiClient : IApiClient
{
    private readonly IApiConnectionService _connectionService;
    private readonly IApiConfigurationService _configService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiClient(
        IApiConnectionService connectionService,
        IApiConfigurationService configService,
        IHttpClientFactory httpClientFactory)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<T> GetAsync<T>(string relativePath, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async (httpClient, baseUrl) =>
        {
            var response = await httpClient.GetAsync($"{baseUrl}{relativePath}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new InvalidOperationException("Response deserialization returned null");
        }, cancellationToken);
    }

    public async Task<T> PostAsync<T>(string relativePath, object body, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async (httpClient, baseUrl) =>
        {
            var json = JsonSerializer.Serialize(body, GetJsonOptions());
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{baseUrl}{relativePath}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new InvalidOperationException("Response deserialization returned null");
        }, cancellationToken);
    }

    public async Task PostAsync(string relativePath, object body, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async (httpClient, baseUrl) =>
        {
            var json = JsonSerializer.Serialize(body, GetJsonOptions());
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{baseUrl}{relativePath}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true; // Dummy return for void method
        }, cancellationToken);
    }

    public async Task<T> PutAsync<T>(string relativePath, object body, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async (httpClient, baseUrl) =>
        {
            var json = JsonSerializer.Serialize(body, GetJsonOptions());
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PutAsync($"{baseUrl}{relativePath}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new InvalidOperationException("Response deserialization returned null");
        }, cancellationToken);
    }

    public async Task PutAsync(string relativePath, object body, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async (httpClient, baseUrl) =>
        {
            var json = JsonSerializer.Serialize(body, GetJsonOptions());
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PutAsync($"{baseUrl}{relativePath}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true; // Dummy return for void method
        }, cancellationToken);
    }

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async (httpClient, baseUrl) =>
        {
            var response = await httpClient.DeleteAsync($"{baseUrl}{relativePath}", cancellationToken);
            response.EnsureSuccessStatusCode();

            return true; // Dummy return for void method
        }, cancellationToken);
    }

    public string? GetCurrentBaseUrl()
    {
        return _connectionService.GetActiveBaseUrl();
    }

    /// <summary>
    /// Executes an HTTP operation with automatic retry and failover logic.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<HttpClient, string, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var config = _configService.GetCurrentConfiguration();
        var maxAttempts = config.ConnectionSettings.RetryAttempts;
        var retryDelay = TimeSpan.FromMilliseconds(config.ConnectionSettings.RetryDelayMs);

        Exception? lastException = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                // Discover active endpoint (uses cached if available)
                var connectionResult = await _connectionService.DiscoverActiveEndpointAsync(cancellationToken);

                if (!connectionResult.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"No active API endpoint available: {connectionResult.ErrorMessage}"
                    );
                }

                var baseUrl = connectionResult.Endpoint.GetBaseUrl();
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(config.ConnectionSettings.TimeoutSeconds);

                // Execute the operation
                return await operation(httpClient, baseUrl);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts - 1)
            {
                lastException = ex;

                // Server error or network error - try failover to next endpoint
                _connectionService.InvalidateActiveEndpoint();
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (TaskCanceledException ex) when (attempt < maxAttempts - 1 && !cancellationToken.IsCancellationRequested)
            {
                // Timeout - retry
                lastException = ex;
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        // All retries exhausted
        throw new InvalidOperationException(
            $"API request failed after {maxAttempts} attempts",
            lastException
        );
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}
