using MediaButler.Services;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;

namespace MediaButler.Tests.Unit.Services;

/// <summary>
/// Unit tests for NotificationService following "Simple Made Easy" testing principles.
/// Tests verify notification behavior without complecting with external dependencies.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly NotificationService _notificationService;

    public NotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<NotificationService>>();
        _notificationService = new NotificationService(_mockLogger.Object);
    }

    [Fact]
    public async Task NotifyOperationStartedAsync_WithValidParameters_ReturnsSuccess()
    {
        // Given
        var fileHash = "test-hash-123";
        var operation = "Processing test file";

        // When
        var result = await _notificationService.NotifyOperationStartedAsync(fileHash, operation);

        // Then
        result.IsSuccess.Should().BeTrue();
        VerifyLoggerCalled(LogLevel.Information, "File operation started");
    }

    [Fact]
    public async Task NotifyOperationStartedAsync_WithEmptyFileHash_ReturnsFailure()
    {
        // Given
        var fileHash = "";
        var operation = "Processing test file";

        // When
        var result = await _notificationService.NotifyOperationStartedAsync(fileHash, operation);

        // Then
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("File hash cannot be empty");
    }

    [Fact]
    public async Task NotifyOperationStartedAsync_WithEmptyOperation_ReturnsFailure()
    {
        // Given
        var fileHash = "test-hash-123";
        var operation = "";

        // When
        var result = await _notificationService.NotifyOperationStartedAsync(fileHash, operation);

        // Then
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Operation description cannot be empty");
    }

    [Fact]
    public async Task NotifyOperationProgressAsync_WithValidParameters_ReturnsSuccess()
    {
        // Given
        var fileHash = "test-hash-123";
        var progress = "Classifying file";

        // When
        var result = await _notificationService.NotifyOperationProgressAsync(fileHash, progress);

        // Then
        result.IsSuccess.Should().BeTrue();
        VerifyLoggerCalled(LogLevel.Information, "File operation progress");
    }

    [Fact]
    public async Task NotifyOperationCompletedAsync_WithValidParameters_ReturnsSuccess()
    {
        // Given
        var fileHash = "test-hash-123";
        var resultDescription = "File moved successfully";

        // When
        var result = await _notificationService.NotifyOperationCompletedAsync(fileHash, resultDescription);

        // Then
        result.IsSuccess.Should().BeTrue();
        VerifyLoggerCalled(LogLevel.Information, "File operation completed successfully");
    }

    [Fact]
    public async Task NotifyOperationFailedAsync_WithRetryPossible_LogsWarning()
    {
        // Given
        var fileHash = "test-hash-123";
        var error = "Temporary network error";
        var canRetry = true;

        // When
        var result = await _notificationService.NotifyOperationFailedAsync(fileHash, error, canRetry);

        // Then
        result.IsSuccess.Should().BeTrue();
        VerifyLoggerCalled(LogLevel.Warning, "File operation failed (retry possible)");
    }

    [Fact]
    public async Task NotifyOperationFailedAsync_WithoutRetryPossible_LogsError()
    {
        // Given
        var fileHash = "test-hash-123";
        var error = "Critical file corruption";
        var canRetry = false;

        // When
        var result = await _notificationService.NotifyOperationFailedAsync(fileHash, error, canRetry);

        // Then
        result.IsSuccess.Should().BeTrue();
        VerifyLoggerCalled(LogLevel.Error, "File operation failed (manual intervention required)");
    }

    [Theory]
    [InlineData(MediaButler.Services.Interfaces.NotificationSeverity.Info, LogLevel.Information)]
    [InlineData(MediaButler.Services.Interfaces.NotificationSeverity.Warning, LogLevel.Warning)]
    [InlineData(MediaButler.Services.Interfaces.NotificationSeverity.Error, LogLevel.Error)]
    public async Task NotifySystemStatusAsync_WithDifferentSeverities_LogsCorrectLevel(
        MediaButler.Services.Interfaces.NotificationSeverity severity, LogLevel expectedLogLevel)
    {
        // Given
        var message = "Test system status message";

        // When
        var result = await _notificationService.NotifySystemStatusAsync(message, severity);

        // Then
        result.IsSuccess.Should().BeTrue();
        VerifyLoggerCalled(expectedLogLevel, "System");
    }

    [Fact]
    public async Task NotifySystemStatusAsync_WithEmptyMessage_ReturnsFailure()
    {
        // Given
        var message = "";

        // When
        var result = await _notificationService.NotifySystemStatusAsync(message);

        // Then
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Status message cannot be empty");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Given/When/Then
        Action act = () => new NotificationService(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private void VerifyLoggerCalled(LogLevel logLevel, string messageContains)
    {
        _mockLogger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}