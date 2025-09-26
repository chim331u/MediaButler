using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediaButler.API.Controllers;
using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Services.Interfaces;
using MediaButler.Tests.Unit.Builders;
using MediaButler.Tests.Unit.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MediaButler.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for FileActionsController.
/// Tests controller behavior using mocked services to isolate controller logic.
/// Follows "Simple Made Easy" principles - testing HTTP/response behavior without service complexity.
/// </summary>
public class FileActionsControllerTests : TestBase
{
    private readonly Mock<IFileActionsService> _mockFileActionsService;
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<ILogger<FileActionsController>> _mockLogger;
    private readonly FileActionsController _controller;

    public FileActionsControllerTests()
    {
        _mockFileActionsService = new Mock<IFileActionsService>();
        _mockFileService = new Mock<IFileService>();
        _mockLogger = new Mock<ILogger<FileActionsController>>();

        _controller = new FileActionsController(
            _mockFileActionsService.Object,
            _mockFileService.Object,
            _mockLogger.Object);
    }

    #region IgnoreFileAsync Tests

    [Fact]
    public async Task IgnoreFileAsync_WithValidHash_ShouldReturnOkResult()
    {
        // Arrange
        var testFile = new TrackedFileBuilder()
            .WithFileName("test_file.mkv")
            .WithStatus(FileStatus.Ignored)
            .Build();

        _mockFileService
            .Setup(service => service.IgnoreFileAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TrackedFile>.Success(testFile));

        // Act
        var result = await _controller.IgnoreFileAsync(testFile.Hash);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        // Verify the response structure
        var responseObj = okResult.Value;
        responseObj.Should().NotBeNull();

        // Check properties via reflection (since we're using anonymous object)
        var responseType = responseObj!.GetType();
        var messageProperty = responseType.GetProperty("message");
        var hashProperty = responseType.GetProperty("hash");
        var statusProperty = responseType.GetProperty("status");

        messageProperty!.GetValue(responseObj).Should().Be("File successfully marked as ignored");
        hashProperty!.GetValue(responseObj).Should().Be(testFile.Hash);
        statusProperty!.GetValue(responseObj).Should().Be(FileStatus.Ignored.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task IgnoreFileAsync_WithInvalidHash_ShouldReturnBadRequest(string hash)
    {
        // Act
        var result = await _controller.IgnoreFileAsync(hash);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().Be("Hash parameter cannot be empty");

        // Verify service was never called
        _mockFileService.Verify(
            service => service.IgnoreFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IgnoreFileAsync_WithNonExistentFile_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentHash = "non_existent_hash";
        var errorMessage = $"File with hash '{nonExistentHash}' not found";

        _mockFileService
            .Setup(service => service.IgnoreFileAsync(nonExistentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TrackedFile>.Failure(errorMessage));

        // Act
        var result = await _controller.IgnoreFileAsync(nonExistentHash);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult!.Value.Should().Be(errorMessage);
    }

    [Fact]
    public async Task IgnoreFileAsync_WithBusinessLogicError_ShouldReturnBadRequest()
    {
        // Arrange
        var testHash = "test_hash";
        var errorMessage = "Cannot ignore a file that has already been moved";

        _mockFileService
            .Setup(service => service.IgnoreFileAsync(testHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TrackedFile>.Failure(errorMessage));

        // Act
        var result = await _controller.IgnoreFileAsync(testHash);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().Be(errorMessage);
    }

    [Fact]
    public async Task IgnoreFileAsync_WithServiceException_ShouldReturnInternalServerError()
    {
        // Arrange
        var testHash = "test_hash";
        var exceptionMessage = "Database connection failed";

        _mockFileService
            .Setup(service => service.IgnoreFileAsync(testHash, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _controller.IgnoreFileAsync(testHash);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("An unexpected error occurred while ignoring the file");
    }

    [Fact]
    public async Task IgnoreFileAsync_ShouldLogAppropriateMessages()
    {
        // Arrange
        var testFile = new TrackedFileBuilder()
            .WithFileName("test_file.mkv")
            .WithStatus(FileStatus.Ignored)
            .Build();

        _mockFileService
            .Setup(service => service.IgnoreFileAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TrackedFile>.Success(testFile));

        // Act
        await _controller.IgnoreFileAsync(testFile.Hash);

        // Assert
        // Verify that logging occurred (checking that logger was called)
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Received request to ignore file with hash")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully marked file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IgnoreFileAsync_WithFailure_ShouldLogWarning()
    {
        // Arrange
        var testHash = "test_hash";
        var errorMessage = "File not found";

        _mockFileService
            .Setup(service => service.IgnoreFileAsync(testHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TrackedFile>.Failure(errorMessage));

        // Act
        await _controller.IgnoreFileAsync(testHash);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to ignore file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IgnoreFileAsync_WithException_ShouldLogError()
    {
        // Arrange
        var testHash = "test_hash";
        var exception = new Exception("Test exception");

        _mockFileService
            .Setup(service => service.IgnoreFileAsync(testHash, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        await _controller.IgnoreFileAsync(testHash);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error ignoring file")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}