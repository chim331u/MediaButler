using MediaButler.Mobile.Models;
using MediaButler.Shared.UI.Models;

namespace MediaButler.Mobile.Components.Interfaces;

/// <summary>
/// HTTP client service for API communication following "Simple Made Easy" principles.
/// Single responsibility: HTTP operations only.
/// No state - each request is independent.
/// Values over exceptions - returns explicit Result objects.
/// </summary>
public interface IHttpClientService
{
    /// <summary>
    /// Performs HTTP GET request with typed response.
    /// </summary>
    Task<Result<T>> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs HTTP POST request with payload and typed response.
    /// </summary>
    Task<Result<T>> PostAsync<T>(string endpoint, object? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs HTTP PUT request with payload and typed response.
    /// </summary>
    Task<Result<T>> PutAsync<T>(string endpoint, object? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs HTTP DELETE request (no response body).
    /// </summary>
    Task<Result> DeleteAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs HTTP POST request (no response body).
    /// </summary>
    Task<Result> PostAsync(string endpoint, object? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs HTTP PUT request (no response body).
    /// </summary>
    Task<Result> PutAsync(string endpoint, object? payload = null, CancellationToken cancellationToken = default);
}
