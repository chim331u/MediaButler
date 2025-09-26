using System.Net;
using System.Text;
using System.Text.Json;
using MediaButler.Web.Interfaces;
using MediaButler.Web.Models;

namespace MediaButler.Web.Services;

/// <summary>
/// Simple HTTP client service implementation following "Simple Made Easy" principles.
/// No state - each request is independent.
/// Values over exceptions - returns explicit Result objects.
/// No braiding - only handles HTTP communication.
/// </summary>
public class HttpClientService : IHttpClientService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Result<T>> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            return await ProcessResponse<T>(response);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Request failed: {ex.Message}");
        }
    }

    public async Task<Result<T>> PostAsync<T>(string endpoint, object? payload = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = CreateJsonContent(payload);
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            return await ProcessResponse<T>(response);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Request failed: {ex.Message}");
        }
    }

    public async Task<Result<T>> PutAsync<T>(string endpoint, object? payload = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = CreateJsonContent(payload);
            var response = await _httpClient.PutAsync(endpoint, content, cancellationToken);
            return await ProcessResponse<T>(response);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Request failed: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
            return ProcessResponse(response);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Request failed: {ex.Message}");
        }
    }

    public async Task<Result> PostAsync(string endpoint, object? payload = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = CreateJsonContent(payload);
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            return ProcessResponse(response);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Request failed: {ex.Message}");
        }
    }

    public async Task<Result> PutAsync(string endpoint, object? payload = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = CreateJsonContent(payload);
            var response = await _httpClient.PutAsync(endpoint, content, cancellationToken);
            return ProcessResponse(response);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes HTTP response and converts to typed Result.
    /// Pure function - no side effects, deterministic output.
    /// </summary>
    private async Task<Result<T>> ProcessResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            try
            {
                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    return Result<T>.Success(default!);
                }

                var data = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                return Result<T>.Success(data!);
            }
            catch (JsonException ex)
            {
                return Result<T>.Failure($"Failed to deserialize response: {ex.Message}", (int)response.StatusCode);
            }
        }

        var errorMessage = await GetErrorMessage(response);
        return Result<T>.HttpFailure((int)response.StatusCode, errorMessage);
    }

    /// <summary>
    /// Processes HTTP response and converts to non-generic Result.
    /// Pure function - no side effects, deterministic output.
    /// </summary>
    private static Result ProcessResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return Result.Success();
        }

        return Result.HttpFailure((int)response.StatusCode, response.ReasonPhrase);
    }

    /// <summary>
    /// Creates JSON content from payload object.
    /// Pure function - same input always produces same output.
    /// </summary>
    private StringContent? CreateJsonContent(object? payload)
    {
        if (payload == null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Extracts error message from HTTP response.
    /// Pure function - deterministic based on response content.
    /// </summary>
    private static async Task<string> GetErrorMessage(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }
        catch
        {
            // If we can't read the content, fall back to reason phrase
        }

        return response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}";
    }
}