using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using MediaButler.Tests.Integration.Infrastructure;
using MediaButler.Tests.Unit.ObjectMothers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MediaButler.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for TrackedFileRepository implementation.
/// Tests repository operations with real database interactions.
/// Follows "Simple Made Easy" principles - testing actual data access behavior.
/// </summary>
public class TrackedFileRepositoryTests : IntegrationTestBase
{
    public TrackedFileRepositoryTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetByHashAsync_WithExistingFile_ShouldReturnFile()
    {
        // Arrange
        
        var testFile = new TrackedFile
        {
            Hash = "repository123456789012345678901234567890123456789012345678901",
            FileName = "Repository.Test.mkv",
            OriginalPath = "/test/Repository.Test.mkv",
            FileSize = 1000000,
            Status = FileStatus.New
        };

        Context.TrackedFiles.Add(testFile);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var result = await repository.GetByHashAsync(testFile.Hash);

        // Assert
        result.Should().NotBeNull();
        result!.Hash.Should().Be(testFile.Hash);
        result.FileName.Should().Be("Repository.Test.mkv");
        result.Status.Should().Be(FileStatus.New);
    }

    [Fact]
    public async Task GetByHashAsync_WithNonExistentHash_ShouldReturnNull()
    {
        // Arrange
        var repository = new TrackedFileRepository(Context);
        var nonExistentHash = "nonexistent1234567890123456789012345678901234567890123456789";

        // Act
        var result = await repository.GetByHashAsync(nonExistentHash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByHashAsync_WithSoftDeletedFile_ShouldReturnNull()
    {
        // Arrange
        
        var testFile = new TrackedFile
        {
            Hash = "softdelete123456789012345678901234567890123456789012345678901",
            FileName = "SoftDelete.Test.mkv",
            OriginalPath = "/test/SoftDelete.Test.mkv",
            FileSize = 1000000
        };

        Context.TrackedFiles.Add(testFile);
        await Context.SaveChangesAsync();

        // Soft delete the file
        testFile.SoftDelete("Test deletion");
        Context.TrackedFiles.Update(testFile);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var result = await repository.GetByHashAsync(testFile.Hash);

        // Assert
        result.Should().BeNull(); // Soft deleted files should not be returned by default
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldReturnFilesWithSpecificStatus()
    {
        // Arrange
        
        var newFiles = Enumerable.Range(1, 3)
            .Select(i => new TrackedFile
            {
                Hash = $"status{i:D2}34567890123456789012345678901234567890123456789012345{i:D3}",
                FileName = $"Status.Test.{i:D2}.mkv",
                OriginalPath = $"/test/Status.Test.{i:D2}.mkv",
                FileSize = 1000000,
                Status = FileStatus.New
            }).ToList();

        var classifiedFiles = Enumerable.Range(4, 2)
            .Select(i => new TrackedFile
            {
                Hash = $"status{i:D2}34567890123456789012345678901234567890123456789012345{i:D3}",
                FileName = $"Status.Test.{i:D2}.mkv",
                OriginalPath = $"/test/Status.Test.{i:D2}.mkv",
                FileSize = 1000000,
                Status = FileStatus.Classified,
                SuggestedCategory = "TEST SERIES",
                Confidence = 0.8m
            }).ToList();

        Context.TrackedFiles.AddRange(newFiles);
        Context.TrackedFiles.AddRange(classifiedFiles);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var newFilesResult = await repository.GetByStatusAsync(FileStatus.New);
        var classifiedFilesResult = await repository.GetByStatusAsync(FileStatus.Classified);

        // Assert
        newFilesResult.Should().HaveCount(3);
        newFilesResult.Should().AllSatisfy(f => f.Status.Should().Be(FileStatus.New));

        classifiedFilesResult.Should().HaveCount(2);
        classifiedFilesResult.Should().AllSatisfy(f =>
        {
            f.Status.Should().Be(FileStatus.Classified);
            f.SuggestedCategory.Should().Be("TEST SERIES");
            f.Confidence.Should().Be(0.8m);
        });
    }

    [Fact]
    public async Task GetFilesReadyForClassificationAsync_ShouldReturnNewFilesInOrder()
    {
        // Arrange
        
        var baseTime = DateTime.UtcNow.AddHours(-1);
        var testFiles = Enumerable.Range(1, 5)
            .Select(i => new TrackedFile
            {
                Hash = $"classification{i:D2}567890123456789012345678901234567890123456789{i:D3}",
                FileName = $"Classification.Test.{i:D2}.mkv",
                OriginalPath = $"/test/Classification.Test.{i:D2}.mkv",
                FileSize = 1000000,
                Status = FileStatus.New
            }).ToList();

        // Set different created dates to test ordering
        for (int i = 0; i < testFiles.Count; i++)
        {
            testFiles[i].CreatedDate = baseTime.AddMinutes(i * 10);
        }

        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var result = await repository.GetFilesReadyForClassificationAsync(3);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(f => f.CreatedDate); // Oldest first (FIFO)
        result.Should().AllSatisfy(f => f.Status.Should().Be(FileStatus.New));
        
        // Verify correct files were returned (oldest 3)
        var resultHashes = result.Select(f => f.Hash).ToList();
        resultHashes.Should().Contain(testFiles[0].Hash);
        resultHashes.Should().Contain(testFiles[1].Hash);
        resultHashes.Should().Contain(testFiles[2].Hash);
    }

    [Fact]
    public async Task GetFilesAwaitingConfirmationAsync_ShouldReturnClassifiedFiles()
    {
        // Arrange
        
        var classifiedFiles = TrackedFileObjectMother.SeriesFiles("AWAITING CONFIRMATION", 3)
            .ToList();

        // Set specific hashes and mark as classified
        for (int i = 0; i < classifiedFiles.Count; i++)
        {
            classifiedFiles[i].Hash = $"awaiting{i:D2}5678901234567890123456789012345678901234567890123{i:D3}";
            classifiedFiles[i].MarkAsClassified("AWAITING CONFIRMATION", 0.8m);
        }

        // Add a file that's not awaiting confirmation
        var newFile = new TrackedFile
        {
            Hash = "notawaiting123456789012345678901234567890123456789012345678901",
            FileName = "NotAwaiting.Test.mkv",
            OriginalPath = "/test/NotAwaiting.Test.mkv",
            FileSize = 1000000,
            Status = FileStatus.New
        };

        Context.TrackedFiles.AddRange(classifiedFiles);
        Context.TrackedFiles.Add(newFile);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var result = await repository.GetFilesAwaitingConfirmationAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(f =>
        {
            f.Status.Should().Be(FileStatus.Classified);
            f.SuggestedCategory.Should().Be("AWAITING CONFIRMATION");
            f.ClassifiedAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ExistsByOriginalPathAsync_ShouldDetectDuplicates()
    {
        // Arrange
        
        var existingFile = new TrackedFile
        {
            Hash = "duplicate123456789012345678901234567890123456789012345678901",
            FileName = "Duplicate.Test.mkv",
            OriginalPath = "/test/path/Duplicate.Test.mkv",
            FileSize = 1000000
        };

        Context.TrackedFiles.Add(existingFile);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var existsResult = await repository.ExistsByOriginalPathAsync("/test/path/Duplicate.Test.mkv");
        var notExistsResult = await repository.ExistsByOriginalPathAsync("/different/path/NonExistent.mkv");

        // Assert
        existsResult.Should().BeTrue();
        notExistsResult.Should().BeFalse();
    }

    [Fact]
    public async Task GetProcessingStatsAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        
        // Create files in different states
        var mixedFiles = TrackedFileObjectMother.MixedStateFiles().ToList();
        
        // Set specific hashes to avoid conflicts
        for (int i = 0; i < mixedFiles.Count; i++)
        {
            mixedFiles[i].Hash = $"stats{i:D2}567890123456789012345678901234567890123456789012345{i:D3}";
        }

        Context.TrackedFiles.AddRange(mixedFiles);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var stats = await repository.GetProcessingStatsAsync();

        // Assert
        stats.Should().NotBeEmpty();
        stats.Should().ContainKeys(
            FileStatus.New,
            FileStatus.Classified,
            FileStatus.ReadyToMove,
            FileStatus.Moved,
            FileStatus.Error
        );

        // Verify counts are positive
        foreach (var stat in stats)
        {
            stat.Value.Should().BeGreaterThan(0);
        }

        // Total should match the number of files we added
        stats.Values.Sum().Should().Be(mixedFiles.Count);
    }

    [Fact]
    public async Task SearchByFilenameAsync_ShouldSupportWildcards()
    {
        // Arrange
        
        var testFiles = new[]
        {
            new TrackedFile
            {
                Hash = "search01123456789012345678901234567890123456789012345678901",
                FileName = "Breaking.Bad.S01E01.mkv",
                OriginalPath = "/test/Breaking.Bad.S01E01.mkv",
                FileSize = 1000000
            },
            new TrackedFile
            {
                Hash = "search02123456789012345678901234567890123456789012345678901",
                FileName = "Breaking.Bad.S01E02.mkv", 
                OriginalPath = "/test/Breaking.Bad.S01E02.mkv",
                FileSize = 1100000
            },
            new TrackedFile
            {
                Hash = "search03123456789012345678901234567890123456789012345678901",
                FileName = "The.Office.S01E01.mkv",
                OriginalPath = "/test/The.Office.S01E01.mkv",
                FileSize = 900000
            }
        };

        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var breakingBadFiles = await repository.SearchByFilenameAsync("Breaking.Bad%");
        var s01e01Files = await repository.SearchByFilenameAsync("%.S01E01.mkv");
        var allMkvFiles = await repository.SearchByFilenameAsync("%.mkv");

        // Assert
        breakingBadFiles.Should().HaveCount(2);
        breakingBadFiles.Should().AllSatisfy(f => f.FileName.Should().StartWith("Breaking.Bad"));

        s01e01Files.Should().HaveCount(2);
        s01e01Files.Should().AllSatisfy(f => f.FileName.Should().EndWith(".S01E01.mkv"));

        allMkvFiles.Should().HaveCount(3);
        allMkvFiles.Should().AllSatisfy(f => f.FileName.Should().EndWith(".mkv"));
    }

    [Fact]
    public async Task Add_Update_SoftDelete_Restore_ShouldWorkCorrectly()
    {
        // Arrange
        var repository = new TrackedFileRepository(Context);

        var testFile = new TrackedFile
        {
            Hash = "lifecycle123456789012345678901234567890123456789012345678901",
            FileName = "Lifecycle.Test.mkv",
            OriginalPath = "/test/Lifecycle.Test.mkv",
            FileSize = 1000000,
            Status = FileStatus.New
        };

        // Act & Assert - Add
        var addedFile = repository.Add(testFile);
        await repository.SaveChangesAsync();

        addedFile.Should().NotBeNull();
        addedFile.Hash.Should().Be(testFile.Hash);

        // Act & Assert - Update
        addedFile.MarkAsClassified("TEST SERIES", 0.9m);
        var updatedFile = repository.Update(addedFile);
        await repository.SaveChangesAsync();

        updatedFile.Status.Should().Be(FileStatus.Classified);
        updatedFile.SuggestedCategory.Should().Be("TEST SERIES");

        // Act & Assert - Soft Delete
        repository.SoftDelete(updatedFile, "Integration test deletion");
        await repository.SaveChangesAsync();

        // Verify file is soft deleted (not returned by normal queries)
        var deletedFileQuery = await repository.GetByHashAsync(testFile.Hash);
        deletedFileQuery.Should().BeNull();

        // But exists in database with IgnoreQueryFilters
        var deletedFileRaw = await repository.GetByIdIncludeDeletedAsync([testFile.Hash]);
        deletedFileRaw.Should().NotBeNull();
        deletedFileRaw!.IsActive.Should().BeFalse();
        deletedFileRaw.Note.Should().Contain("Integration test deletion");

        // Act & Assert - Restore
        repository.Restore(deletedFileRaw, "Integration test restoration");
        await repository.SaveChangesAsync();

        // Verify file is restored
        var restoredFile = await repository.GetByHashAsync(testFile.Hash);
        restoredFile.Should().NotBeNull();
        restoredFile!.IsActive.Should().BeTrue();
        restoredFile.Note.Should().Contain("Integration test restoration");
    }

    [Fact]
    public async Task GetFilesExceedingRetryLimitAsync_ShouldReturnHighRetryCountFiles()
    {
        // Arrange
        
        var lowRetryFile = new TrackedFile
        {
            Hash = "lowretry123456789012345678901234567890123456789012345678901",
            FileName = "LowRetry.Test.mkv",
            OriginalPath = "/test/LowRetry.Test.mkv",
            FileSize = 1000000,
            RetryCount = 2
        };

        var highRetryFile = new TrackedFile
        {
            Hash = "highretry12345678901234567890123456789012345678901234567890",
            FileName = "HighRetry.Test.mkv",
            OriginalPath = "/test/HighRetry.Test.mkv",
            FileSize = 1000000,
            RetryCount = 5
        };

        Context.TrackedFiles.AddRange(lowRetryFile, highRetryFile);
        await Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(Context);

        // Act
        var result = await repository.GetFilesExceedingRetryLimitAsync(3);

        // Assert
        result.Should().HaveCount(1);
        result.First().Hash.Should().Be(highRetryFile.Hash);
        result.First().RetryCount.Should().Be(5);
    }
}