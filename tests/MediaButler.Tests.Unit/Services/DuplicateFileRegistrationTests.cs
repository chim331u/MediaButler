using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MediaButler.Tests.Unit.Services;

/// <summary>
/// Unit tests for duplicate file registration handling.
/// Validates the fix for Priority 2 (v1.0.7) - UNIQUE Constraint Violations.
/// </summary>
public class DuplicateFileRegistrationTests
{
    private readonly Mock<ITrackedFileRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<FileService>> _mockLogger;
    private readonly FileService _fileService;

    public DuplicateFileRegistrationTests()
    {
        _mockRepository = new Mock<ITrackedFileRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<FileService>>();

        _mockUnitOfWork.Setup(u => u.TrackedFiles).Returns(_mockRepository.Object);

        _fileService = new FileService(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterFileAsync_WhenHashExists_ShouldReturnExistingRecord()
    {
        // Arrange
        var testFilePath = Path.GetTempFileName();
        File.WriteAllText(testFilePath, "test content for hash");

        var existingFile = new TrackedFile
        {
            Hash = await CalculateTestFileHashAsync(testFilePath),
            FileName = "existing_file.mkv",
            OriginalPath = "/old/path/existing_file.mkv",
            FileSize = 1024,
            Status = FileStatus.Classified
        };

        _mockRepository.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);

        try
        {
            // Act
            var result = await _fileService.RegisterFileAsync(testFilePath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(existingFile);
            result.Value.Hash.Should().Be(existingFile.Hash);

            // Verify no insert was attempted (idempotent behavior)
            _mockRepository.Verify(r => r.Add(It.IsAny<TrackedFile>()), Times.Never);
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task RegisterFileAsync_WhenHashExistsWithDifferentPath_ShouldUpdatePath()
    {
        // Arrange
        var testFilePath = Path.GetTempFileName();
        File.WriteAllText(testFilePath, "test content");

        var existingFile = new TrackedFile
        {
            Hash = await CalculateTestFileHashAsync(testFilePath),
            FileName = "old_name.mkv",
            OriginalPath = "/old/different/path/file.mkv",
            FileSize = 1024,
            Status = FileStatus.Moved
        };

        _mockRepository.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);

        try
        {
            // Act
            var result = await _fileService.RegisterFileAsync(testFilePath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.OriginalPath.Should().Be(testFilePath,
                "Path should be updated to new location");
            result.Value.FileName.Should().Be(Path.GetFileName(testFilePath),
                "Filename should be updated");

            // Verify update was called
            _mockRepository.Verify(r => r.Update(existingFile), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task RegisterFileAsync_WhenHashDoesNotExist_ShouldCreateNewRecord()
    {
        // Arrange
        var testFilePath = Path.GetTempFileName();
        File.WriteAllText(testFilePath, "unique content");

        _mockRepository.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedFile?)null);

        _mockRepository.Setup(r => r.Add(It.IsAny<TrackedFile>()));

        try
        {
            // Act
            var result = await _fileService.RegisterFileAsync(testFilePath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.FileName.Should().Be(Path.GetFileName(testFilePath));
            result.Value.OriginalPath.Should().Be(testFilePath);
            result.Value.Status.Should().Be(FileStatus.New);

            // Verify new record was added
            _mockRepository.Verify(r => r.Add(It.IsAny<TrackedFile>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task RegisterFileAsync_ConcurrentDuplicates_ShouldHandleRaceCondition()
    {
        // Arrange - Simulate race condition: hash doesn't exist during check, but exists during save
        var testFilePath = Path.GetTempFileName();
        File.WriteAllText(testFilePath, "race condition test");

        var concurrentFile = new TrackedFile
        {
            Hash = await CalculateTestFileHashAsync(testFilePath),
            FileName = "concurrent_file.mkv",
            OriginalPath = "/concurrent/path/file.mkv",
            FileSize = 1024,
            Status = FileStatus.New
        };

        // First call returns null (file doesn't exist yet)
        // Second call returns the file (another process created it)
        var callCount = 0;
        _mockRepository.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? null : concurrentFile;
            });

        // Simulate DbUpdateException with UNIQUE constraint message
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("UNIQUE constraint failed: TrackedFiles.Hash",
                new Exception("UNIQUE constraint failed: TrackedFiles.Hash")));

        try
        {
            // Act
            var result = await _fileService.RegisterFileAsync(testFilePath);

            // Assert - Should handle race condition gracefully
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(concurrentFile,
                "Should return the concurrently created file");

            // Verify fallback query was executed
            _mockRepository.Verify(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.AtLeast(2), "Should query again after constraint violation");
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task RegisterFileAsync_MultipleCallsWithSameFile_ShouldBeIdempotent()
    {
        // Arrange
        var testFilePath = Path.GetTempFileName();
        File.WriteAllText(testFilePath, "idempotent test");

        var existingFile = new TrackedFile
        {
            Hash = await CalculateTestFileHashAsync(testFilePath),
            FileName = Path.GetFileName(testFilePath),
            OriginalPath = testFilePath,
            FileSize = new FileInfo(testFilePath).Length,
            Status = FileStatus.New
        };

        _mockRepository.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);

        try
        {
            // Act - Register same file multiple times
            var result1 = await _fileService.RegisterFileAsync(testFilePath);
            var result2 = await _fileService.RegisterFileAsync(testFilePath);
            var result3 = await _fileService.RegisterFileAsync(testFilePath);

            // Assert - All calls should succeed with same result (idempotent)
            result1.IsSuccess.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();
            result3.IsSuccess.Should().BeTrue();

            result1.Value.Hash.Should().Be(result2.Value.Hash).And.Be(result3.Value.Hash);

            // No inserts should occur (already exists)
            _mockRepository.Verify(r => r.Add(It.IsAny<TrackedFile>()), Times.Never);
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    private static async Task<string> CalculateTestFileHashAsync(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
