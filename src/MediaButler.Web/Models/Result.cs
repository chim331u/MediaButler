namespace MediaButler.Web.Models;

/// <summary>
/// Represents a result that can either succeed with a value or fail with an error.
/// Follows "Simple Made Easy" principle: Values over exceptions, explicit success/failure.
/// </summary>
public readonly record struct Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string Error { get; init; }
    public int StatusCode { get; init; }

    private Result(bool isSuccess, T? value, string error, int statusCode)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, string.Empty, 200);

    /// <summary>
    /// Creates a failed result with an error message and optional status code.
    /// </summary>
    public static Result<T> Failure(string error, int statusCode = 0) =>
        new(false, default, error, statusCode);

    /// <summary>
    /// Creates a failed result from an HTTP status code.
    /// </summary>
    public static Result<T> HttpFailure(int statusCode, string? reasonPhrase = null) =>
        new(false, default, reasonPhrase ?? $"HTTP {statusCode}", statusCode);
}

/// <summary>
/// Non-generic result for operations that don't return a value.
/// </summary>
public readonly record struct Result
{
    public bool IsSuccess { get; init; }
    public string Error { get; init; }
    public int StatusCode { get; init; }

    private Result(bool isSuccess, string error, int statusCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, string.Empty, 200);

    /// <summary>
    /// Creates a failed result with an error message and optional status code.
    /// </summary>
    public static Result Failure(string error, int statusCode = 0) =>
        new(false, error, statusCode);

    /// <summary>
    /// Creates a failed result from an HTTP status code.
    /// </summary>
    public static Result HttpFailure(int statusCode, string? reasonPhrase = null) =>
        new(false, reasonPhrase ?? $"HTTP {statusCode}", statusCode);
}