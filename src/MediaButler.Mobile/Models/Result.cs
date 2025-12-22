namespace MediaButler.Mobile.Models;

/// <summary>
/// Represents the result of an operation that doesn't return a value.
/// Follows "Simple Made Easy" - explicit success/failure rather than exceptions.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; protected set; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string Error { get; protected set; } = string.Empty;

    /// <summary>
    /// HTTP status code if applicable (e.g., 404, 500).
    /// </summary>
    public int? StatusCode { get; protected set; }

    protected Result(bool isSuccess, string error, int? statusCode = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new Result(true, string.Empty);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static Result Failure(string error) => new Result(false, error);

    /// <summary>
    /// Creates a failed result with an error message and HTTP status code.
    /// </summary>
    public static Result Failure(string error, int statusCode) => new Result(false, error, statusCode);

    /// <summary>
    /// Creates a failed result from an HTTP status code.
    /// </summary>
    public static Result HttpFailure(int statusCode, string? reasonPhrase = null)
    {
        var error = reasonPhrase ?? $"HTTP {statusCode}";
        return new Result(false, error, statusCode);
    }
}

/// <summary>
/// Represents the result of an operation that returns a value of type T.
/// Follows "Simple Made Easy" - explicit success/failure with typed values.
/// </summary>
public class Result<T> : Result
{
    /// <summary>
    /// The value returned by the operation if successful.
    /// </summary>
    public T? Value { get; private set; }

    protected Result(bool isSuccess, T? value, string error, int? statusCode = null)
        : base(isSuccess, error, statusCode)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<T> Success(T value) => new Result<T>(true, value, string.Empty);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public new static Result<T> Failure(string error) => new Result<T>(false, default, error);

    /// <summary>
    /// Creates a failed result with an error message and HTTP status code.
    /// </summary>
    public new static Result<T> Failure(string error, int statusCode) => new Result<T>(false, default, error, statusCode);

    /// <summary>
    /// Creates a failed result from an HTTP status code.
    /// </summary>
    public new static Result<T> HttpFailure(int statusCode, string? reasonPhrase = null)
    {
        var error = reasonPhrase ?? $"HTTP {statusCode}";
        return new Result<T>(false, default, error, statusCode);
    }

    /// <summary>
    /// Converts a non-generic Result to a generic Result&lt;T&gt; with a default value.
    /// </summary>
    public static Result<T> FromResult(Result result, T? defaultValue = default)
    {
        if (result.IsSuccess)
            return Success(defaultValue!);

        return new Result<T>(false, defaultValue, result.Error, result.StatusCode);
    }
}
