using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services;
using MediaButler.Tests.Unit.Infrastructure;
using MediaButler.Tests.Unit.ObjectMothers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MediaButler.Tests.Unit.Services;

/// <summary>
/// Unit tests for FileService business logic.
/// Tests service operations using mocked repositories to isolate business logic.
/// Follows "Simple Made Easy" principles - testing behavior without database complexity.
/// </summary>
public class FileServiceTests : TestBase
{
    private readonly Mock<ITrackedFileRepository> _mockFileRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<FileService>> _mockLogger;
    private readonly FileService _fileService;

    public FileServiceTests()
    {
        _mockFileRepository = new Mock<ITrackedFileRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<FileService>>();
        
        // Setup unit of work to return our mocked repository
        _mockUnitOfWork.Setup(uow => uow.TrackedFiles).Returns(_mockFileRepository.Object);
        
        _fileService = new FileService(_mockFileRepository.Object, _mockUnitOfWork.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetFileByHashAsync_WithValidHash_ShouldReturnFile()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        // Act
        var result = await _fileService.GetFileByHashAsync(testFile.Hash);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Hash.Should().Be(testFile.Hash);
        result.Value.FileName.Should().Be(testFile.FileName);
    }

    [Fact]
    public async Task GetFileByHashAsync_WithNonExistentHash_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentHash = TestHash("999");
        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(nonExistentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedFile?)null);

        // Act
        var result = await _fileService.GetFileByHashAsync(nonExistentHash);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFileByHashAsync_WithInvalidHash_ShouldReturnFailure(string? invalidHash)
    {
        // Act
        var result = await _fileService.GetFileByHashAsync(invalidHash!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Hash cannot be null or empty");
    }

    [Fact]
    public async Task GetFilesByStatusAsync_WithValidStatus_ShouldReturnFiles()
    {
        // Arrange
        var testFiles = TrackedFileObjectMother.MixedStateFiles().ToList();
        var classifiedFiles = testFiles.Where(f => f.Status == FileStatus.Classified).ToList();
        
        _mockFileRepository
            .Setup(repo => repo.GetByStatusAsync(FileStatus.Classified, It.IsAny<CancellationToken>()))
            .ReturnsAsync(classifiedFiles);

        // Act
        var result = await _fileService.GetFilesByStatusAsync(FileStatus.Classified);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().AllSatisfy(file => file.Status.Should().Be(FileStatus.Classified));
    }

    [Fact]
    public async Task UpdateClassificationAsync_WithValidParameters_ShouldUpdateFile()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        var suggestedCategory = "BREAKING BAD";
        var confidence = 0.85m;

        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        _mockFileRepository
            .Setup(repo => repo.Update(It.IsAny<TrackedFile>()))
            .Returns((TrackedFile file) => file);

        _mockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.UpdateClassificationAsync(testFile.Hash, suggestedCategory, confidence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SuggestedCategory.Should().Be(suggestedCategory);
        result.Value.Confidence.Should().Be(confidence);
        result.Value.Status.Should().Be(FileStatus.Classified);
        result.Value.ClassifiedAt.Should().NotBeNull();
        
        // Verify repository calls
        _mockFileRepository.Verify(repo => repo.Update(It.IsAny<TrackedFile>()), Times.Once);
        _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public async Task UpdateClassificationAsync_WithInvalidConfidence_ShouldReturnFailure(decimal invalidConfidence)
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        
        // Act
        var result = await _fileService.UpdateClassificationAsync(testFile.Hash, "VALID CATEGORY", invalidConfidence);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Confidence must be between 0.0 and 1.0");
        
        // Verify no repository calls were made
        _mockFileRepository.Verify(repo => repo.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateClassificationAsync_WithInvalidCategory_ShouldReturnFailure(string? invalidCategory)
    {
        // Act
        var result = await _fileService.UpdateClassificationAsync(TestHash(), invalidCategory!, 0.8m);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Category cannot be null or empty");
    }

    [Fact]
    public async Task ConfirmCategoryAsync_WithValidParameters_ShouldUpdateFileStatus()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.ClassifiedFile("BREAKING BAD", 0.85m);
        var confirmedCategory = "BREAKING BAD";

        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        _mockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.ConfirmCategoryAsync(testFile.Hash, confirmedCategory);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Category.Should().Be(confirmedCategory);
        result.Value.Status.Should().Be(FileStatus.ReadyToMove);
        result.Value.TargetPath.Should().NotBeNull();
        result.Value.TargetPath.Should().Contain(confirmedCategory);
    }

    [Fact]
    public async Task MarkFileAsMovedAsync_WithValidParameters_ShouldUpdateFileStatus()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.ConfirmedFile("BREAKING BAD");
        var targetPath = "/media/TV/BREAKING BAD/Breaking.Bad.S01E01.mkv";

        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        _mockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.MarkFileAsMovedAsync(testFile.Hash, targetPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TargetPath.Should().Be(targetPath);
        result.Value.Status.Should().Be(FileStatus.Moved);
        result.Value.MovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordErrorAsync_WithValidParameters_ShouldUpdateErrorFields()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.ConfirmedFile("BREAKING BAD");
        var errorMessage = "File not found during move operation";
        var exception = "System.IO.FileNotFoundException: The file 'test.mkv' could not be found.";

        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        _mockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.RecordErrorAsync(testFile.Hash, errorMessage, exception);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.LastError.Should().Be(errorMessage);
        result.Value.LastErrorAt.Should().NotBeNull();
        result.Value.RetryCount.Should().BeGreaterThan(0);
        result.Value.Status.Should().Be(FileStatus.Retry);
    }

    [Fact]
    public async Task GetFilesReadyForClassificationAsync_WithValidLimit_ShouldReturnFiles()
    {
        // Arrange
        var newFiles = TrackedFileObjectMother.SeriesFiles("TEST SERIES", 5)
            .Select(f => { f.Status = FileStatus.New; return f; })
            .ToList();

        _mockFileRepository
            .Setup(repo => repo.GetFilesReadyForClassificationAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFiles);

        // Act
        var result = await _fileService.GetFilesReadyForClassificationAsync(10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(5);
        result.Value.Should().AllSatisfy(file => file.Status.Should().Be(FileStatus.New));
    }

    [Fact]
    public async Task DeleteFileAsync_WithValidHash_ShouldSoftDeleteFile()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        var reason = "User requested deletion";

        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        _mockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.DeleteFileAsync(testFile.Hash, reason);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify soft delete was called (file should be marked as inactive)
        _mockFileRepository.Verify(repo => repo.SoftDelete(It.IsAny<TrackedFile>(), reason), Times.Once);
    }

    [Fact]
    public async Task SearchFilesByNameAsync_WithPattern_ShouldReturnMatchingFiles()
    {
        // Arrange
        var pattern = "Breaking.Bad%";
        var matchingFiles = TrackedFileObjectMother.SeriesFiles("BREAKING BAD", 3).ToList();

        _mockFileRepository
            .Setup(repo => repo.SearchByFilenameAsync(pattern, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchingFiles);

        // Act
        var result = await _fileService.SearchFilesByNameAsync(pattern);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);
        result.Value.Should().AllSatisfy(file => file.FileName.Should().Contain("BREAKING.BAD"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetFilesReadyForClassificationAsync_WithInvalidLimit_ShouldReturnFailure(int invalidLimit)
    {
        // Act
        var result = await _fileService.GetFilesReadyForClassificationAsync(invalidLimit);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Limit must be greater than 0");
    }

    [Fact]
    public async Task ResetFileErrorAsync_WithValidHash_ShouldResetErrorState()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.ErrorFile("Previous error");
        testFile.RetryCount = 2;

        _mockFileRepository
            .Setup(repo => repo.GetByHashAsync(testFile.Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        _mockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.ResetFileErrorAsync(testFile.Hash);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.RetryCount.Should().Be(0);
        result.Value.LastError.Should().BeNull();
        result.Value.LastErrorAt.Should().BeNull();
        result.Value.Status.Should().Be(FileStatus.New); // Reset to initial state
    }
}