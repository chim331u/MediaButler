using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Data.Repositories;
using MediaButler.Tests.Unit.Infrastructure;
using MediaButler.Tests.Unit.ObjectMothers;
using Moq;
using Xunit;

namespace MediaButler.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for IRepository interface contracts and behavior.
/// Tests repository interface expectations without database complexity.
/// Follows "Simple Made Easy" principles - testing interfaces as contracts.
/// </summary>
public class IRepositoryTests : TestBase
{
    private readonly Mock<IRepository<TrackedFile>> _mockRepository;

    public IRepositoryTests()
    {
        _mockRepository = new Mock<IRepository<TrackedFile>>();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldCallRepository()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        var keyValues = new object[] { testFile.Hash };

        _mockRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        // Act
        var result = await _mockRepository.Object.GetByIdAsync(keyValues);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(testFile);
        _mockRepository.Verify(repo => repo.GetByIdAsync(keyValues, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var keyValues = new object[] { TestHash("999") };

        _mockRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedFile?)null);

        // Act
        var result = await _mockRepository.Object.GetByIdAsync(keyValues);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(repo => repo.GetByIdAsync(keyValues, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdIncludeDeletedAsync_ShouldAllowAccessToSoftDeletedEntities()
    {
        // Arrange
        var softDeletedFile = TrackedFileObjectMother.NewVideoFile();
        var keyValues = new object[] { softDeletedFile.Hash };

        _mockRepository
            .Setup(repo => repo.GetByIdIncludeDeletedAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(softDeletedFile);

        // Act
        var result = await _mockRepository.Object.GetByIdIncludeDeletedAsync(keyValues);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(softDeletedFile);
        _mockRepository.Verify(repo => repo.GetByIdIncludeDeletedAsync(keyValues, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllActiveEntities()
    {
        // Arrange
        var activeFiles = TrackedFileObjectMother.SeriesFiles("TEST SERIES", 3);

        _mockRepository
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeFiles);

        // Act
        var result = await _mockRepository.Object.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        _mockRepository.Verify(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindAsync_WithPredicate_ShouldReturnMatchingEntities()
    {
        // Arrange
        var matchingFiles = TrackedFileObjectMother.SeriesFiles("BREAKING BAD", 2);
        Expression<Func<TrackedFile, bool>> predicate = f => f.FileName.Contains("Breaking.Bad");

        _mockRepository
            .Setup(repo => repo.FindAsync(It.IsAny<Expression<Func<TrackedFile, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchingFiles);

        // Act
        var result = await _mockRepository.Object.FindAsync(predicate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        _mockRepository.Verify(repo => repo.FindAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithMatchingPredicate_ShouldReturnSingleEntity()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        Expression<Func<TrackedFile, bool>> predicate = f => f.Hash == testFile.Hash;

        _mockRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<TrackedFile, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFile);

        // Act
        var result = await _mockRepository.Object.FirstOrDefaultAsync(predicate);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(testFile);
        _mockRepository.Verify(repo => repo.FirstOrDefaultAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        var pagedFiles = TrackedFileObjectMother.SeriesFiles("TEST SERIES", 10).Skip(5).Take(5);
        var skip = 5;
        var take = 5;

        _mockRepository
            .Setup(repo => repo.GetPagedAsync(
                It.IsAny<int>(), 
                It.IsAny<int>(), 
                It.IsAny<Expression<Func<TrackedFile, bool>>>(), 
                It.IsAny<Expression<Func<TrackedFile, object>>>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedFiles);

        // Act
        var result = await _mockRepository.Object.GetPagedAsync(skip, take);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        _mockRepository.Verify(repo => repo.GetPagedAsync(skip, take, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CountAsync_WithPredicate_ShouldReturnMatchingCount()
    {
        // Arrange
        Expression<Func<TrackedFile, bool>> predicate = f => f.Status == Core.Enums.FileStatus.New;
        var expectedCount = 5;

        _mockRepository
            .Setup(repo => repo.CountAsync(It.IsAny<Expression<Func<TrackedFile, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _mockRepository.Object.CountAsync(predicate);

        // Assert
        result.Should().Be(expectedCount);
        _mockRepository.Verify(repo => repo.CountAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_WithMatchingPredicate_ShouldReturnTrue()
    {
        // Arrange
        Expression<Func<TrackedFile, bool>> predicate = f => f.FileName == "Breaking.Bad.S01E01.mkv";

        _mockRepository
            .Setup(repo => repo.ExistsAsync(It.IsAny<Expression<Func<TrackedFile, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockRepository.Object.ExistsAsync(predicate);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(repo => repo.ExistsAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_WithNonMatchingPredicate_ShouldReturnFalse()
    {
        // Arrange
        Expression<Func<TrackedFile, bool>> predicate = f => f.FileName == "NonExistent.File.mkv";

        _mockRepository
            .Setup(repo => repo.ExistsAsync(It.IsAny<Expression<Func<TrackedFile, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _mockRepository.Object.ExistsAsync(predicate);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(repo => repo.ExistsAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Add_WithValidEntity_ShouldReturnEntity()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();

        _mockRepository
            .Setup(repo => repo.Add(It.IsAny<TrackedFile>()))
            .Returns(testFile);

        // Act
        var result = _mockRepository.Object.Add(testFile);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(testFile);
        _mockRepository.Verify(repo => repo.Add(testFile), Times.Once);
    }

    [Fact]
    public void AddRange_WithMultipleEntities_ShouldCallRepository()
    {
        // Arrange
        var testFiles = TrackedFileObjectMother.SeriesFiles("TEST SERIES", 3).ToList();

        // Act
        _mockRepository.Object.AddRange(testFiles);

        // Assert
        _mockRepository.Verify(repo => repo.AddRange(testFiles), Times.Once);
    }

    [Fact]
    public void Update_WithValidEntity_ShouldReturnEntity()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.ClassifiedFile("BREAKING BAD", 0.8m);

        _mockRepository
            .Setup(repo => repo.Update(It.IsAny<TrackedFile>()))
            .Returns(testFile);

        // Act
        var result = _mockRepository.Object.Update(testFile);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(testFile);
        _mockRepository.Verify(repo => repo.Update(testFile), Times.Once);
    }

    [Fact]
    public void SoftDelete_WithEntity_ShouldCallRepository()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        var reason = "User requested deletion";

        // Act
        _mockRepository.Object.SoftDelete(testFile, reason);

        // Assert
        _mockRepository.Verify(repo => repo.SoftDelete(testFile, reason), Times.Once);
    }

    [Fact]
    public void HardDelete_WithEntity_ShouldCallRepository()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();

        // Act
        _mockRepository.Object.HardDelete(testFile);

        // Assert
        _mockRepository.Verify(repo => repo.HardDelete(testFile), Times.Once);
    }

    [Fact]
    public void Restore_WithEntity_ShouldCallRepository()
    {
        // Arrange
        var testFile = TrackedFileObjectMother.NewVideoFile();
        var reason = "Restore after accidental deletion";

        // Act
        _mockRepository.Object.Restore(testFile, reason);

        // Assert
        _mockRepository.Verify(repo => repo.Restore(testFile, reason), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnAffectedRowCount()
    {
        // Arrange
        var expectedAffectedRows = 3;

        _mockRepository
            .Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAffectedRows);

        // Act
        var result = await _mockRepository.Object.SaveChangesAsync();

        // Assert
        result.Should().Be(expectedAffectedRows);
        _mockRepository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var expectedAffectedRows = 1;

        _mockRepository
            .Setup(repo => repo.SaveChangesAsync(cancellationToken))
            .ReturnsAsync(expectedAffectedRows);

        // Act
        var result = await _mockRepository.Object.SaveChangesAsync(cancellationToken);

        // Assert
        result.Should().Be(expectedAffectedRows);
        _mockRepository.Verify(repo => repo.SaveChangesAsync(cancellationToken), Times.Once);
    }
}