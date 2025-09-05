using System.Text.Json.Serialization;

namespace MediaButler.API.Models.Response;

/// <summary>
/// Standard API response wrapper that provides consistent structure for all API responses.
/// Follows "Simple Made Easy" principles by separating response structure from business data.
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Gets or sets whether the operation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the response data when successful.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    /// Gets or sets error information when operation fails.
    /// </summary>
    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }

    /// <summary>
    /// Gets or sets metadata about the response.
    /// </summary>
    [JsonPropertyName("meta")]
    public ResponseMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Creates a successful API response with data.
    /// </summary>
    /// <param name="data">The response data</param>
    /// <param name="message">Optional success message</param>
    /// <returns>Successful API response</returns>
    public static ApiResponse<T> CreateSuccess(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Metadata = new ResponseMetadata
            {
                Timestamp = DateTime.UtcNow,
                Message = message
            }
        };
    }

    /// <summary>
    /// Creates a failed API response with error information.
    /// </summary>
    /// <param name="errorCode">Error code</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="details">Optional error details</param>
    /// <returns>Failed API response</returns>
    public static ApiResponse<T> CreateError(string errorCode, string errorMessage, object? details = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = new ApiError
            {
                Code = errorCode,
                Message = errorMessage,
                Details = details
            },
            Metadata = new ResponseMetadata
            {
                Timestamp = DateTime.UtcNow
            }
        };
    }
}

/// <summary>
/// Standard API response for operations that don't return data.
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    /// <summary>
    /// Creates a successful API response without data.
    /// </summary>
    /// <param name="message">Success message</param>
    /// <returns>Successful API response</returns>
    public static ApiResponse CreateSuccess(string? message = null)
    {
        return new ApiResponse
        {
            Success = true,
            Metadata = new ResponseMetadata
            {
                Timestamp = DateTime.UtcNow,
                Message = message ?? "Operation completed successfully"
            }
        };
    }

    /// <summary>
    /// Creates a failed API response without data.
    /// </summary>
    /// <param name="errorCode">Error code</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="details">Optional error details</param>
    /// <returns>Failed API response</returns>
    public new static ApiResponse CreateError(string errorCode, string errorMessage, object? details = null)
    {
        return new ApiResponse
        {
            Success = false,
            Error = new ApiError
            {
                Code = errorCode,
                Message = errorMessage,
                Details = details
            },
            Metadata = new ResponseMetadata
            {
                Timestamp = DateTime.UtcNow
            }
        };
    }
}

/// <summary>
/// Error information included in API responses.
/// </summary>
public class ApiError
{
    /// <summary>
    /// Gets or sets the error code for programmatic handling.
    /// </summary>
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    [JsonPropertyName("details")]
    public object? Details { get; set; }

    /// <summary>
    /// Gets or sets validation errors for model binding failures.
    /// </summary>
    [JsonPropertyName("validationErrors")]
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
}

/// <summary>
/// Response metadata providing context about the API response.
/// </summary>
public class ResponseMetadata
{
    /// <summary>
    /// Gets or sets when the response was generated.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets optional response message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the API version that generated this response.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets performance information about the request.
    /// </summary>
    [JsonPropertyName("performance")]
    public PerformanceInfo? Performance { get; set; }
}

/// <summary>
/// Performance information about API request processing.
/// </summary>
public class PerformanceInfo
{
    /// <summary>
    /// Gets or sets the processing duration in milliseconds.
    /// </summary>
    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    /// <summary>
    /// Gets or sets memory usage information.
    /// </summary>
    [JsonPropertyName("memoryUsageMB")]
    public double MemoryUsageMB { get; set; }
}