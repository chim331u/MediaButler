using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MediaButler.Core.Common;
using Xunit;

namespace MediaButler.Tests.Unit.Common;

/// <summary>
/// Unit tests for the Result&lt;T&gt; pattern implementation.
/// These tests verify the core functionality of explicit error handling
/// without exceptions, following "Simple Made Easy" principles.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_WithValue_ShouldCreateSuccessfulResult()
    {
        // Arrange
        const string expectedValue = "test value";

        // Act
        var result = Result<string>.Success(expectedValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(expectedValue);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failure_WithErrorMessage_ShouldCreateFailedResult()
    {
        // Arrange
        const string expectedError = "Something went wrong";

        // Act
        var result = Result<string>.Failure(expectedError);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(expectedError);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failure_WithErrorMessageAndException_ShouldCreateFailedResultWithException()
    {
        // Arrange
        const string expectedError = "Operation failed";
        var expectedException = new InvalidOperationException("Inner exception");

        // Act
        var result = Result<string>.Failure(expectedError, expectedException);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
        result.Exception.Should().Be(expectedException);
    }

    [Fact]
    public void Value_OnFailedResult_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var result = Result<string>.Failure("Error message");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        exception.Message.Should().Contain("Cannot access Value on a failed result");
        exception.Message.Should().Contain("Error message");
    }

    [Fact]
    public void Error_OnSuccessfulResult_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var result = Result<string>.Success("test value");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => result.Error);
        exception.Message.Should().Contain("Cannot access Error on a successful result");
    }

    [Fact]
    public void Map_OnSuccessfulResult_ShouldTransformValue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mappedResult = result.Map(x => x.ToString());

        // Assert
        mappedResult.IsSuccess.Should().BeTrue();
        mappedResult.Value.Should().Be("42");
    }

    [Fact]
    public void Map_OnFailedResult_ShouldPropagateFailure()
    {
        // Arrange
        const string expectedError = "Original error";
        var originalException = new Exception("Original exception");
        var result = Result<int>.Failure(expectedError, originalException);

        // Act
        var mappedResult = result.Map(x => x.ToString());

        // Assert
        mappedResult.IsFailure.Should().BeTrue();
        mappedResult.Error.Should().Be(expectedError);
        mappedResult.Exception.Should().Be(originalException);
    }

    [Fact]
    public void Bind_OnSuccessfulResult_ShouldExecuteBindFunction()
    {
        // Arrange
        var result = Result<string>.Success("123");

        // Act
        var boundResult = result.Bind(ParseInt);

        // Assert
        boundResult.IsSuccess.Should().BeTrue();
        boundResult.Value.Should().Be(123);
    }

    [Fact]
    public void Bind_OnFailedResult_ShouldPropagateFailure()
    {
        // Arrange
        const string expectedError = "Original error";
        var result = Result<string>.Failure(expectedError);

        // Act
        var boundResult = result.Bind(ParseInt);

        // Assert
        boundResult.IsFailure.Should().BeTrue();
        boundResult.Error.Should().Be(expectedError);
    }

    [Fact]
    public void Bind_WithFailingBindFunction_ShouldReturnFailure()
    {
        // Arrange
        var result = Result<string>.Success("not a number");

        // Act
        var boundResult = result.Bind(ParseInt);

        // Assert
        boundResult.IsFailure.Should().BeTrue();
        boundResult.Error.Should().Contain("Invalid number format");
    }

    [Fact]
    public void OnSuccess_OnSuccessfulResult_ShouldExecuteAction()
    {
        // Arrange
        var result = Result<string>.Success("test");
        var wasExecuted = false;
        string? capturedValue = null;

        // Act
        var returnedResult = result.OnSuccess(value =>
        {
            wasExecuted = true;
            capturedValue = value;
        });

        // Assert
        wasExecuted.Should().BeTrue();
        capturedValue.Should().Be("test");
        returnedResult.Should().BeSameAs(result); // Fluent interface
    }

    [Fact]
    public void OnSuccess_OnFailedResult_ShouldNotExecuteAction()
    {
        // Arrange
        var result = Result<string>.Failure("Error");
        var wasExecuted = false;

        // Act
        result.OnSuccess(_ => wasExecuted = true);

        // Assert
        wasExecuted.Should().BeFalse();
    }

    [Fact]
    public void OnFailure_OnFailedResult_ShouldExecuteAction()
    {
        // Arrange
        const string expectedError = "Test error";
        var result = Result<string>.Failure(expectedError);
        var wasExecuted = false;
        string? capturedError = null;

        // Act
        var returnedResult = result.OnFailure(error =>
        {
            wasExecuted = true;
            capturedError = error;
        });

        // Assert
        wasExecuted.Should().BeTrue();
        capturedError.Should().Be(expectedError);
        returnedResult.Should().BeSameAs(result); // Fluent interface
    }

    [Fact]
    public void OnFailure_OnSuccessfulResult_ShouldNotExecuteAction()
    {
        // Arrange
        var result = Result<string>.Success("Success");
        var wasExecuted = false;

        // Act
        result.OnFailure(_ => wasExecuted = true);

        // Assert
        wasExecuted.Should().BeFalse();
    }

    [Fact]
    public void GetValueOrDefault_OnSuccessfulResult_ShouldReturnValue()
    {
        // Arrange
        var result = Result<string>.Success("actual value");

        // Act
        var value = result.GetValueOrDefault("fallback");

        // Assert
        value.Should().Be("actual value");
    }

    [Fact]
    public void GetValueOrDefault_OnFailedResult_ShouldReturnFallback()
    {
        // Arrange
        var result = Result<string>.Failure("Error");

        // Act
        var value = result.GetValueOrDefault("fallback");

        // Assert
        value.Should().Be("fallback");
    }

    [Fact]
    public void GetValueOrDefault_WithoutParameter_OnFailedResult_ShouldReturnDefault()
    {
        // Arrange
        var result = Result<string>.Failure("Error");

        // Act
        var value = result.GetValueOrDefault();

        // Assert
        value.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Failure_WithNullOrWhitespaceError_ShouldThrowArgumentNullException(string? error)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => Result<string>.Failure(error!));
        exception.ParamName.Should().Be("error");
    }

    [Fact]
    public void ToString_OnSuccessfulResult_ShouldShowValue()
    {
        // Arrange
        var result = Result<string>.Success("test value");

        // Act
        var stringRepresentation = result.ToString();

        // Assert
        stringRepresentation.Should().Be("Success: test value");
    }

    [Fact]
    public void ToString_OnFailedResult_ShouldShowError()
    {
        // Arrange
        var result = Result<string>.Failure("test error");

        // Act
        var stringRepresentation = result.ToString();

        // Assert
        stringRepresentation.Should().Be("Failure: test error");
    }

    // Helper method for testing Bind functionality
    private static Result<int> ParseInt(string value)
    {
        return int.TryParse(value, out var result)
            ? Result<int>.Success(result)
            : Result<int>.Failure("Invalid number format");
    }
}

/// <summary>
/// Unit tests for the non-generic Result pattern implementation.
/// Tests operations that indicate success/failure without returning values.
/// </summary>
public class NonGenericResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failure_WithErrorMessage_ShouldCreateFailedResult()
    {
        // Arrange
        const string expectedError = "Operation failed";

        // Act
        var result = Result.Failure(expectedError);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(expectedError);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failure_WithErrorMessageAndException_ShouldCreateFailedResultWithException()
    {
        // Arrange
        const string expectedError = "Operation failed";
        var expectedException = new InvalidOperationException("Inner exception");

        // Act
        var result = Result.Failure(expectedError, expectedException);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
        result.Exception.Should().Be(expectedException);
    }

    [Fact]
    public void And_WithTwoSuccessfulResults_ShouldReturnSuccess()
    {
        // Arrange
        var result1 = Result.Success();
        var result2 = Result.Success();

        // Act
        var combined = result1.And(result2);

        // Assert
        combined.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void And_WithFirstResultFailed_ShouldReturnFirstFailure()
    {
        // Arrange
        var result1 = Result.Failure("First error");
        var result2 = Result.Success();

        // Act
        var combined = result1.And(result2);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().Be("First error");
    }

    [Fact]
    public void And_WithSecondResultFailed_ShouldReturnSecondFailure()
    {
        // Arrange
        var result1 = Result.Success();
        var result2 = Result.Failure("Second error");

        // Act
        var combined = result1.And(result2);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().Be("Second error");
    }

    [Fact]
    public void Map_OnSuccessfulResult_ShouldReturnGenericSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var mappedResult = result.Map("mapped value");

        // Assert
        mappedResult.IsSuccess.Should().BeTrue();
        mappedResult.Value.Should().Be("mapped value");
    }

    [Fact]
    public void Map_OnFailedResult_ShouldPropagateFailure()
    {
        // Arrange
        const string expectedError = "Original error";
        var originalException = new Exception("Original exception");
        var result = Result.Failure(expectedError, originalException);

        // Act
        var mappedResult = result.Map("mapped value");

        // Assert
        mappedResult.IsFailure.Should().BeTrue();
        mappedResult.Error.Should().Be(expectedError);
        mappedResult.Exception.Should().Be(originalException);
    }
}

/// <summary>
/// Unit tests for Result extension methods.
/// Tests functional composition and utility methods.
/// </summary>
public class ResultExtensionsTests
{
    [Fact]
    public void Combine_WithAllSuccessfulResults_ShouldReturnCombinedSuccess()
    {
        // Arrange
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3)
        };

        // Act
        var combined = results.Combine();

        // Assert
        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Combine_WithOneFailedResult_ShouldReturnFirstFailure()
    {
        // Arrange
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure("Second failed"),
            Result<int>.Failure("Third failed")
        };

        // Act
        var combined = results.Combine();

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().Be("Second failed");
    }

    [Fact]
    public void Try_WithSuccessfulFunction_ShouldReturnSuccess()
    {
        // Act
        var result = ResultExtensions.Try(() => 42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Try_WithExceptionThrowingFunction_ShouldReturnFailure()
    {
        // Act
        var result = ResultExtensions.Try<int>(() => throw new InvalidOperationException("Test exception"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Test exception");
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Try_WithCustomErrorMessage_ShouldUseCustomMessage()
    {
        // Act
        var result = ResultExtensions.Try<int>(
            () => throw new InvalidOperationException("Original message"),
            "Custom error message");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Custom error message");
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Try_WithNullFunction_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => ResultExtensions.Try<int>(null!));
        exception.ParamName.Should().Be("func");
    }

    [Fact]
    public void Combine_WithNullResults_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            ResultExtensions.Combine<int>(null!));
        exception.ParamName.Should().Be("results");
    }
}