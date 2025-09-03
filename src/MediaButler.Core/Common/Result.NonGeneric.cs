using System;

namespace MediaButler.Core.Common;

/// <summary>
/// Represents the result of an operation that can either succeed or fail without returning a value.
/// This type provides explicit error handling for operations that only indicate success/failure,
/// following "Simple Made Easy" principles by making operation outcomes explicit.
/// </summary>
/// <remarks>
/// This non-generic Result is useful for operations like file moves, database updates,
/// or other actions where success is indicated by the absence of errors rather than
/// the presence of a return value.
/// </remarks>
public sealed class Result
{
    private readonly string _error;
    private readonly Exception? _exception;

    /// <summary>
    /// Gets a value indicating whether this result represents a successful operation.
    /// </summary>
    /// <value>true if the operation succeeded; otherwise, false.</value>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether this result represents a failed operation.
    /// </summary>
    /// <value>true if the operation failed; otherwise, false.</value>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message of this result.
    /// This property should only be accessed when IsFailure is true.
    /// </summary>
    /// <value>The error message describing why the operation failed.</value>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public string Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error on a successful result.");

            return _error;
        }
    }

    /// <summary>
    /// Gets the exception associated with this result, if any.
    /// This property can be accessed regardless of success/failure state and may be null.
    /// </summary>
    /// <value>The exception that caused the failure, or null if no exception was involved.</value>
    public Exception? Exception => _exception;

    /// <summary>
    /// Initializes a new instance of the Result class representing success.
    /// </summary>
    private Result()
    {
        _error = string.Empty;
        IsSuccess = true;
    }

    /// <summary>
    /// Initializes a new instance of the Result class representing failure.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="exception">Optional exception that caused the failure.</param>
    private Result(string error, Exception? exception = null)
    {
        _error = error ?? throw new ArgumentNullException(nameof(error));
        _exception = exception;
        IsSuccess = false;
    }

    /// <summary>
    /// Creates a successful result.
    /// Factory method that provides clear semantics for creating success results.
    /// </summary>
    /// <returns>A successful Result.</returns>
    /// <example>
    /// <code>
    /// public Result MoveFile(string source, string destination)
    /// {
    ///     try
    ///     {
    ///         File.Move(source, destination);
    ///         return Result.Success();
    ///     }
    ///     catch (IOException ex)
    ///     {
    ///         return Result.Failure("Failed to move file", ex);
    ///     }
    /// }
    /// </code>
    /// </example>
    public static Result Success() => new();

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// Factory method that provides clear semantics for creating failure results.
    /// </summary>
    /// <param name="error">A descriptive error message.</param>
    /// <returns>A failed Result with the specified error message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when error is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// if (!File.Exists(filePath))
    /// {
    ///     return Result.Failure("File not found");
    /// }
    /// </code>
    /// </example>
    public static Result Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentNullException(nameof(error), "Error message cannot be null or whitespace.");

        return new Result(error);
    }

    /// <summary>
    /// Creates a failed result with the specified error message and exception.
    /// Factory method for creating failure results that include exception information.
    /// </summary>
    /// <param name="error">A descriptive error message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed Result with the specified error message and exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when error is null or whitespace, or exception is null.</exception>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     // Some operation
    ///     return Result.Success();
    /// }
    /// catch (Exception ex)
    /// {
    ///     return Result.Failure("Operation failed", ex);
    /// }
    /// </code>
    /// </example>
    public static Result Failure(string error, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentNullException(nameof(error), "Error message cannot be null or whitespace.");

        return new Result(error, exception ?? throw new ArgumentNullException(nameof(exception)));
    }

    /// <summary>
    /// Combines this result with another result using logical AND semantics.
    /// Both results must be successful for the combined result to be successful.
    /// </summary>
    /// <param name="other">The other result to combine with this one.</param>
    /// <returns>A successful result if both are successful, otherwise the first failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when other is null.</exception>
    /// <example>
    /// <code>
    /// var result1 = ValidateInput(input);
    /// var result2 = CheckPermissions(user);
    /// var combined = result1.And(result2);
    /// </code>
    /// </example>
    public Result And(Result other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        return IsFailure ? this : other;
    }

    /// <summary>
    /// Executes the specified action if this result is successful.
    /// </summary>
    /// <param name="action">The action to execute on success.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <example>
    /// <code>
    /// result.OnSuccess(() => Console.WriteLine("Operation succeeded"))
    ///       .OnFailure(error => Console.WriteLine($"Error: {error}"));
    /// </code>
    /// </example>
    public Result OnSuccess(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (IsSuccess)
            action();

        return this;
    }

    /// <summary>
    /// Executes the specified action if this result is a failure.
    /// The action receives the error message as a parameter.
    /// </summary>
    /// <param name="action">The action to execute on failure.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <example>
    /// <code>
    /// result.OnSuccess(() => Console.WriteLine("Operation succeeded"))
    ///       .OnFailure(error => Console.WriteLine($"Error: {error}"));
    /// </code>
    /// </example>
    public Result OnFailure(Action<string> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (IsFailure)
            action(_error);

        return this;
    }

    /// <summary>
    /// Transforms this non-generic Result into a generic Result with the specified value on success.
    /// If this result is a failure, the failure is propagated.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="value">The value to use for the generic result on success.</param>
    /// <returns>A generic Result containing the specified value or the original failure.</returns>
    /// <example>
    /// <code>
    /// Result operationResult = PerformOperation();
    /// Result&lt;string&gt; stringResult = operationResult.Map("Success!");
    /// </code>
    /// </example>
    public Result<T> Map<T>(T value)
    {
        return IsSuccess
            ? Result<T>.Success(value)
            : _exception != null
                ? Result<T>.Failure(_error, _exception)
                : Result<T>.Failure(_error);
    }

    /// <summary>
    /// Returns a string representation of this result.
    /// </summary>
    /// <returns>A string describing the result state and error (if applicable).</returns>
    public override string ToString()
    {
        return IsSuccess ? "Success" : $"Failure: {_error}";
    }
}