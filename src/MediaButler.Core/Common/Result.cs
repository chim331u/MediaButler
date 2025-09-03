using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaButler.Core.Common;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// This type provides explicit error handling without exceptions, following "Simple Made Easy" principles
/// by making success and failure states explicit and composable.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <remarks>
/// The Result pattern eliminates the need for exception-based error handling in business logic,
/// making error states explicit and forcing callers to handle both success and failure cases.
/// This leads to more predictable and maintainable code.
/// </remarks>
public sealed class Result<T>
{
    private readonly T? _value;
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
    /// Gets the success value of this result.
    /// This property should only be accessed when IsSuccess is true.
    /// </summary>
    /// <value>The success value of type T.</value>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result.</exception>
    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException($"Cannot access Value on a failed result. Error: {_error}");

            return _value!;
        }
    }

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
    /// Initializes a new instance of the Result class with a success value.
    /// </summary>
    /// <param name="value">The success value.</param>
    private Result(T value)
    {
        _value = value;
        _error = string.Empty;
        IsSuccess = true;
    }

    /// <summary>
    /// Initializes a new instance of the Result class with an error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="exception">Optional exception that caused the failure.</param>
    private Result(string error, Exception? exception = null)
    {
        _value = default;
        _error = error ?? throw new ArgumentNullException(nameof(error));
        _exception = exception;
        IsSuccess = false;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// Factory method that provides clear semantics for creating success results.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful Result containing the specified value.</returns>
    /// <example>
    /// <code>
    /// var result = Result&lt;string&gt;.Success("Operation completed");
    /// if (result.IsSuccess)
    /// {
    ///     Console.WriteLine(result.Value);
    /// }
    /// </code>
    /// </example>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// Factory method that provides clear semantics for creating failure results.
    /// </summary>
    /// <param name="error">A descriptive error message.</param>
    /// <returns>A failed Result with the specified error message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when error is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// var result = Result&lt;string&gt;.Failure("File not found");
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine(result.Error);
    /// }
    /// </code>
    /// </example>
    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentNullException(nameof(error), "Error message cannot be null or whitespace.");

        return new Result<T>(error);
    }

    /// <summary>
    /// Creates a failed result with the specified error message and exception.
    /// Factory method for creating failure results that include exception information.
    /// </summary>
    /// <param name="error">A descriptive error message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed Result with the specified error message and exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when error is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     // Some operation
    /// }
    /// catch (IOException ex)
    /// {
    ///     return Result&lt;string&gt;.Failure("Failed to read file", ex);
    /// }
    /// </code>
    /// </example>
    public static Result<T> Failure(string error, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentNullException(nameof(error), "Error message cannot be null or whitespace.");

        return new Result<T>(error, exception ?? throw new ArgumentNullException(nameof(exception)));
    }

    /// <summary>
    /// Transforms the success value of this result using the specified function.
    /// If this result is a failure, the failure is propagated without calling the transform function.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="transform">A function to transform the success value.</param>
    /// <returns>A new Result with the transformed value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transform is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result&lt;int&gt;.Success(42)
    ///     .Map(x => x.ToString())
    ///     .Map(s => $"Value: {s}");
    /// </code>
    /// </example>
    public Result<TNew> Map<TNew>(Func<T, TNew> transform)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));

        return IsSuccess
            ? Result<TNew>.Success(transform(_value!))
            : _exception != null 
                ? Result<TNew>.Failure(_error, _exception)
                : Result<TNew>.Failure(_error);
    }

    /// <summary>
    /// Transforms this result using the specified function that returns a Result.
    /// This is useful for chaining operations that can fail, avoiding nested Result types.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="bind">A function that transforms the value and returns a new Result.</param>
    /// <returns>The result of the bind function, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when bind is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result&lt;string&gt;.Success("123")
    ///     .Bind(ParseInt)
    ///     .Bind(ValidatePositive);
    /// </code>
    /// </example>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> bind)
    {
        if (bind == null)
            throw new ArgumentNullException(nameof(bind));

        return IsSuccess ? bind(_value!) : 
            _exception != null 
                ? Result<TNew>.Failure(_error, _exception)
                : Result<TNew>.Failure(_error);
    }

    /// <summary>
    /// Executes the specified action if this result is successful.
    /// The action receives the success value as a parameter.
    /// </summary>
    /// <param name="action">The action to execute on success.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <example>
    /// <code>
    /// result.OnSuccess(value => Console.WriteLine($"Success: {value}"))
    ///       .OnFailure(error => Console.WriteLine($"Error: {error}"));
    /// </code>
    /// </example>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (IsSuccess)
            action(_value!);

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
    /// result.OnSuccess(value => Console.WriteLine($"Success: {value}"))
    ///       .OnFailure(error => Console.WriteLine($"Error: {error}"));
    /// </code>
    /// </example>
    public Result<T> OnFailure(Action<string> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (IsFailure)
            action(_error);

        return this;
    }

    /// <summary>
    /// Returns the success value if this result is successful, otherwise returns the specified fallback value.
    /// </summary>
    /// <param name="fallback">The value to return if this result is a failure.</param>
    /// <returns>The success value or the fallback value.</returns>
    /// <example>
    /// <code>
    /// var value = result.GetValueOrDefault("default");
    /// </code>
    /// </example>
    public T GetValueOrDefault(T fallback) => IsSuccess ? _value! : fallback;

    /// <summary>
    /// Returns the success value if this result is successful, otherwise returns the default value for type T.
    /// </summary>
    /// <returns>The success value or the default value for type T.</returns>
    /// <example>
    /// <code>
    /// var value = result.GetValueOrDefault();
    /// </code>
    /// </example>
    public T? GetValueOrDefault() => IsSuccess ? _value : default;

    /// <summary>
    /// Returns a string representation of this result.
    /// </summary>
    /// <returns>A string describing the result state and value/error.</returns>
    public override string ToString()
    {
        return IsSuccess
            ? $"Success: {_value}"
            : $"Failure: {_error}";
    }
}

/// <summary>
/// Provides extension methods for working with Result types in a fluent manner.
/// These methods enable functional composition and chaining of Result operations.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Combines multiple results into a single result containing a collection of values.
    /// If any result is a failure, returns the first failure encountered.
    /// </summary>
    /// <typeparam name="T">The type of the success values.</typeparam>
    /// <param name="results">The collection of results to combine.</param>
    /// <returns>A result containing all success values, or the first failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when results is null.</exception>
    /// <example>
    /// <code>
    /// var results = new[]
    /// {
    ///     Result&lt;int&gt;.Success(1),
    ///     Result&lt;int&gt;.Success(2),
    ///     Result&lt;int&gt;.Success(3)
    /// };
    /// var combined = results.Combine(); // Result&lt;IEnumerable&lt;int&gt;&gt;
    /// </code>
    /// </example>
    public static Result<IEnumerable<T>> Combine<T>(this IEnumerable<Result<T>> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        var resultList = results.ToList();
        var failure = resultList.FirstOrDefault(r => r.IsFailure);

        return failure != null
            ? failure.Exception != null
                ? Result<IEnumerable<T>>.Failure(failure.Error, failure.Exception)
                : Result<IEnumerable<T>>.Failure(failure.Error)
            : Result<IEnumerable<T>>.Success(resultList.Select(r => r.Value));
    }

    /// <summary>
    /// Executes the specified function and wraps any exceptions in a Result.
    /// Provides a safe way to execute potentially failing operations.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to execute safely.</param>
    /// <param name="errorMessage">Optional custom error message for exceptions.</param>
    /// <returns>A Result containing the function result or the exception wrapped as a failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
    /// <example>
    /// <code>
    /// var result = ResultExtensions.Try(() => int.Parse("123"));
    /// var fileResult = ResultExtensions.Try(() => File.ReadAllText("file.txt"), "Failed to read file");
    /// </code>
    /// </example>
    public static Result<T> Try<T>(Func<T> func, string? errorMessage = null)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            var message = errorMessage ?? ex.Message;
            return Result<T>.Failure(message, ex);
        }
    }
}