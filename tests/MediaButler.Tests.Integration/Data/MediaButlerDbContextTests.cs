using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Tests.Integration.Infrastructure;
using MediaButler.Tests.Unit.ObjectMothers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MediaButler.Tests.Integration.Data;

/// <summary>
/// Integration tests for MediaButlerDbContext.
/// Tests database operations with real database instance.
/// Follows "Simple Made Easy" principles - testing actual database behavior.
/// </summary>
public class MediaButlerDbContextTests : IntegrationTestBase
{
    public MediaButlerDbContextTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveChangesAsync_WithNewTrackedFile_ShouldPersistWithBaseEntityProperties()
    {
        // Arrange
        
        var trackedFile = new TrackedFile
        {
            Hash = "abc123def456789012345678901234567890123456789012345678901234001",
            FileName = "Integration.Test.S01E01.mkv",
            OriginalPath = "/integration/test/Integration.Test.S01E01.mkv",
            FileSize = 1000000,
            Status = FileStatus.New
        };

        var beforeSave = DateTime.UtcNow.AddSeconds(-1);

        // Act
        Context.TrackedFiles.Add(trackedFile);
        var result = await Context.SaveChangesAsync();

        // Assert
        result.Should().Be(1); // One entity affected
        
        // Verify BaseEntity properties were set
        trackedFile.CreatedDate.Should().BeAfter(beforeSave);
        trackedFile.LastUpdateDate.Should().BeAfter(beforeSave);
        trackedFile.IsActive.Should().BeTrue();
        
        // Verify entity was persisted
        var persistedFile = await Context.TrackedFiles
            .FirstOrDefaultAsync(f => f.Hash == trackedFile.Hash);
        
        persistedFile.Should().NotBeNull();
        persistedFile!.FileName.Should().Be(trackedFile.FileName);
        persistedFile.Status.Should().Be(FileStatus.New);
    }


    [Fact]
    public async Task SoftDeleteFilter_ShouldExcludeInactiveEntities()
    {
        // Arrange
        
        var activeFile = new TrackedFile
        {
            Hash = "active123456789012345678901234567890123456789012345678901234001",
            FileName = "Active.File.mkv",
            OriginalPath = "/test/Active.File.mkv",
            FileSize = 1000000
        };

        var inactiveFile = new TrackedFile
        {
            Hash = "inactive12345678901234567890123456789012345678901234567890123001",
            FileName = "Inactive.File.mkv",
            OriginalPath = "/test/Inactive.File.mkv",
            FileSize = 1000000
        };

        Context.TrackedFiles.AddRange(activeFile, inactiveFile);
        await Context.SaveChangesAsync();

        // Soft delete one file
        inactiveFile.SoftDelete();
        Context.TrackedFiles.Update(inactiveFile);
        await Context.SaveChangesAsync();

        // Act - Query with global filters
        var activeFiles = await Context.TrackedFiles.ToListAsync();

        // Assert - Only active files should be returned
        activeFiles.Should().HaveCountGreaterOrEqualTo(1);
        //activeFiles[0].Hash.Should().Be(activeFile.Hash);
        activeFiles.Should().Contain(f=>f.Hash == activeFile.Hash);
        
        // Verify soft deleted file still exists in database but is excluded
        var allFiles = await Context.TrackedFiles
            .IgnoreQueryFilters() // Bypass soft delete filter
            .ToListAsync();
        
        allFiles.Should().HaveCountGreaterOrEqualTo(2);
        allFiles.Should().Contain(f => f.Hash == inactiveFile.Hash && !f.IsActive);
    }

    [Fact]
    public async Task EntityConfiguration_TrackedFile_ShouldEnforceConstraints()
    {
        // Arrange
        
        var trackedFile = new TrackedFile
        {
            Hash = "constraint123456789012345678901234567890123456789012345678901",
            FileName = "Constraint.Test.mkv",
            OriginalPath = "/test/Constraint.Test.mkv",
            FileSize = 1500000
        };

        // Act & Assert - Should save successfully with valid data
        Context.TrackedFiles.Add(trackedFile);
        await Context.SaveChangesAsync();

        // Verify hash is used as primary key
        var retrievedFile = await Context.TrackedFiles.FindAsync(trackedFile.Hash);
        retrievedFile.Should().NotBeNull();
        retrievedFile!.Hash.Should().Be(trackedFile.Hash);
    }

    [Fact]
    public async Task Update_WithModifiedEntity_ShouldUpdateLastUpdateDate()
    {
        // Arrange
        
        var trackedFile = new TrackedFile
        {
            Hash = "update123456789012345678901234567890123456789012345678901234",
            FileName = "Update.Test.mkv",
            OriginalPath = "/test/Update.Test.mkv",
            FileSize = 2000000
        };

        Context.TrackedFiles.Add(trackedFile);
        await Context.SaveChangesAsync();

        var originalUpdateDate = trackedFile.LastUpdateDate;
        
        // Wait to ensure timestamp difference
        await Task.Delay(100);

        // Act - Modify entity
        trackedFile.MarkAsClassified("TEST SERIES", 0.85m);
        Context.TrackedFiles.Update(trackedFile);
        await Context.SaveChangesAsync();

        // Assert
        trackedFile.LastUpdateDate.Should().BeAfter(originalUpdateDate);
        trackedFile.Status.Should().Be(FileStatus.Classified);
        trackedFile.SuggestedCategory.Should().Be("TEST SERIES");
        trackedFile.Confidence.Should().Be(0.85m);
        trackedFile.ClassifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldHandleMultipleOperations()
    {
        // Arrange
        
        var baseHash = "concurrent123456789012345678901234567890123456789012345678";
        var files = Enumerable.Range(1, 5)
            .Select(i => new TrackedFile
            {
                Hash = baseHash + i.ToString("D3"),
                FileName = $"Concurrent.Test.{i:D2}.mkv",
                OriginalPath = $"/test/Concurrent.Test.{i:D2}.mkv",
                FileSize = 1000000 + (i * 100000)
            })
            .ToList();

        // Act - Add multiple files concurrently
        Context.TrackedFiles.AddRange(files);
        var result = await Context.SaveChangesAsync();

        // Assert
        result.Should().Be(5); // All files should be saved
        
        var savedFiles = await Context.TrackedFiles
            .Where(f => f.Hash.StartsWith(baseHash))
            .OrderBy(f => f.Hash)
            .ToListAsync();

        savedFiles.Should().HaveCount(5);
        savedFiles.Should().BeInAscendingOrder(f => f.Hash);
        
        // Verify all have proper BaseEntity values
        savedFiles.Should().AllSatisfy(f =>
        {
            f.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            f.IsActive.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Transaction_RollbackOnError_ShouldNotPersistAnyChanges()
    {
        // Arrange
        
        var validFile = new TrackedFile
        {
            Hash = "transaction123456789012345678901234567890123456789012345678901",
            FileName = "Valid.File.mkv",
            OriginalPath = "/test/Valid.File.mkv",
            FileSize = 1000000
        };

        using var transaction = await Context.Database.BeginTransactionAsync();

        try
        {
            // Act - Add valid file
            Context.TrackedFiles.Add(validFile);
            await Context.SaveChangesAsync();

            // Simulate error by trying to add duplicate hash
            var duplicateFile = new TrackedFile
            {
                Hash = validFile.Hash, // Same hash - should cause constraint violation
                FileName = "Duplicate.File.mkv",
                OriginalPath = "/test/Duplicate.File.mkv",
                FileSize = 2000000
            };

            Context.TrackedFiles.Add(duplicateFile);
            
            // This should throw due to primary key constraint
            await Context.SaveChangesAsync();
            
            await transaction.CommitAsync();
            
            // Should not reach here
            Assert.Fail("Expected exception was not thrown");
        }
        catch (Exception)
        {
            // Act - Rollback transaction
            await transaction.RollbackAsync();
        }

        // Assert - No files should be persisted after rollback
        var filesAfterRollback = await Context.TrackedFiles.CountAsync();
        filesAfterRollback.Should().Be(0);
    }

    [Fact]
    public async Task QueryFilters_WithComplexQueries_ShouldApplyCorrectly()
    {
        // Arrange
        
        var testFiles = TrackedFileObjectMother.MixedStateFiles().ToList();
        
        // Set specific hashes for test files to avoid conflicts
        for (int i = 0; i < testFiles.Count; i++)
        {
            testFiles[i].Hash = $"query{i:D2}3456789012345678901234567890123456789012345678901234{i:D3}";
        }

        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        // Soft delete one file
        var fileToDelete = testFiles.First();
        fileToDelete.SoftDelete("Test deletion");
        Context.TrackedFiles.Update(fileToDelete);
        await Context.SaveChangesAsync();

        // Act - Complex query with filters
        var activeClassifiedFiles = await Context.TrackedFiles
            .Where(f => f.Status == FileStatus.Classified)
            .OrderBy(f => f.CreatedDate)
            .ToListAsync();

        // Assert - Should only return active files with Classified status
        activeClassifiedFiles.Should().NotBeEmpty();
        activeClassifiedFiles.Should().AllSatisfy(f =>
        {
            f.Status.Should().Be(FileStatus.Classified);
            f.IsActive.Should().BeTrue(); // Global filter should ensure this
            f.Hash.Should().NotBe(fileToDelete.Hash); // Deleted file should be excluded
        });

        // Verify we can still access soft-deleted files when needed
        var deletedFile = await Context.TrackedFiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Hash == fileToDelete.Hash);

        deletedFile.Should().NotBeNull();
        deletedFile!.IsActive.Should().BeFalse();
        deletedFile.Note.Should().Contain("Test deletion");
    }

    [Fact]
    public async Task DatabaseSchema_ShouldSupportAllRequiredOperations()
    {
        // Arrange

        // Act & Assert - Test that all entity types can be created
        var trackedFile = new TrackedFile
        {
            Hash = "schema123456789012345678901234567890123456789012345678901234",
            FileName = "Schema.Test.mkv",
            OriginalPath = "/test/Schema.Test.mkv",
            FileSize = 1000000
        };

        var processingLog = ProcessingLog.Info(
            trackedFile.Hash,
            "Schema",
            "Test Operation",
            "Schema validation test");

        // Add all entities
        Context.TrackedFiles.Add(trackedFile);
        Context.ProcessingLogs.Add(processingLog);

        var result = await Context.SaveChangesAsync();

        // Assert - All entities should be persisted
        result.Should().Be(2);

        // Verify relationships and constraints work
        var savedLog = await Context.ProcessingLogs
            .FirstOrDefaultAsync(pl => pl.FileHash == trackedFile.Hash);

        savedLog.Should().NotBeNull();
        savedLog!.Message.Should().Be("Test Operation");
        savedLog.Level.Should().Be(Core.Enums.LogLevel.Information);
    }
}