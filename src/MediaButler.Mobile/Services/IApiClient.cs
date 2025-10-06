namespace MediaButler.Mobile.Services;

/// <summary>
/// HTTP client wrapper for making API requests to the active MediaButler endpoint.
/// Handles automatic failover, retries, and response deserialization.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Executes a GET request and deserializes the response.
    /// </summary>
    /// <typeparam name="T">Response type.</typeparam>
    /// <param name="relativePath">Relative path (e.g., "/api/files").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response or throws exception on failure.</returns>
    Task<T> GetAsync<T>(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a POST request with a body and deserializes the response.
    /// </summary>
    /// <typeparam name="T">Response type.</typeparam>
    /// <param name="relativePath">Relative path (e.g., "/api/files").</param>
    /// <param name="body">Request body to serialize as JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response or throws exception on failure.</returns>
    Task<T> PostAsync<T>(string relativePath, object body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a POST request without expecting a response body.
    /// </summary>
    Task PostAsync(string relativePath, object body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PUT request with a body and deserializes the response.
    /// </summary>
    Task<T> PutAsync<T>(string relativePath, object body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PUT request without expecting a response body.
    /// </summary>
    Task PutAsync(string relativePath, object body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a DELETE request.
    /// </summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the base URL of the currently active endpoint.
    /// </summary>
    string? GetCurrentBaseUrl();
}
