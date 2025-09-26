using MediaButler.Web.Models;

namespace MediaButler.Web.Interfaces;

/// <summary>
/// Simple HTTP client service following "Simple Made Easy" principles.
/// One role: HTTP communication
/// One task: Execute requests
/// One objective: Return results
/// No braiding with business logic.
/// </summary>
public interface IHttpClientService
{
    /// <summary>
    /// Executes a GET request and returns the response as a typed result.
    /// Pure function: Given same inputs, produces same outputs.
    /// </summary>
    Task<Result<T>> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a POST request with a payload and returns the response as a typed result.
    /// Pure function: Given same inputs, produces same outputs.
    /// </summary>
    Task<Result<T>> PostAsync<T>(string endpoint, object? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PUT request with a payload and returns the response as a typed result.
    /// Pure function: Given same inputs, produces same outputs.
    /// </summary>
    Task<Result<T>> PutAsync<T>(string endpoint, object? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a DELETE request and returns the result.
    /// Pure function: Given same inputs, produces same outputs.
    /// </summary>
    Task<Result> DeleteAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a POST request without expecting a response body.
    /// Pure function: Given same inputs, produces same outputs.
    /// </summary>
    Task<Result> PostAsync(string endpoint, object? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PUT request without expecting a response body.
    /// Pure function: Given same inputs, produces same outputs.
    /// </summary>
    Task<Result> PutAsync(string endpoint, object? payload = null, CancellationToken cancellationToken = default);
}