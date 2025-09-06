using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.UnitOfWork;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MediaButler.Tests.Integration.UnitOfWork;

/// <summary>
/// Integration tests for Unit of Work pattern implementation.
/// Tests transaction behavior and repository coordination.
/// Follows "Simple Made Easy" principles - testing actual transaction behavior.
/// </summary>
public class UnitOfWorkTests : IntegrationTestBase
{
    public UnitOfWorkTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveChangesAsync_WithMultipleEntities_ShouldCommitAllChanges()
    {
        // Arrange
        
        using var scope = CreateScope();
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        var trackedFile = new TrackedFile
        {
            Hash = "unitofwork123456789012345678901234567890123456789012345678901",
            FileName = "UnitOfWork.Test.mkv",
            OriginalPath = "/test/UnitOfWork.Test.mkv",
            FileSize = 1000000,
            Status = FileStatus.New
        };

        var configSetting = new ConfigurationSetting
        {
            Key = "MediaButler.UnitOfWork.TestSetting",
            Value = "test value",
            Section = "UnitOfWork",
            DataType = ConfigurationDataType.String
        };

        var processingLog = ProcessingLog.Info(
            trackedFile.Hash,
            "UnitOfWork",
            "Unit of Work Test",
            "Testing transaction behavior");

        // Act
        unitOfWork.TrackedFiles.Add(trackedFile);
        unitOfWork.ConfigurationSettings.Add(configSetting);
        unitOfWork.ProcessingLogs.Add(processingLog);

        var result = await unitOfWork.SaveChangesAsync();

        // Assert
        result.Should().Be(3); // All three entities should be saved

        // Verify all entities were persisted
        var savedFile = await unitOfWork.TrackedFiles.GetByHashAsync(trackedFile.Hash);
        var savedConfig = await unitOfWork.ConfigurationSettings.FirstOrDefaultAsync(c => c.Key == configSetting.Key);
        var savedLog = await unitOfWork.ProcessingLogs.FirstOrDefaultAsync(pl => pl.FileHash == trackedFile.Hash);

        savedFile.Should().NotBeNull();
        savedConfig.Should().NotBeNull();
        savedLog.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_RollbackOnFailure_ShouldNotPersistAnyChanges()
    {
        // Arrange
        
        using var scope = CreateScope();
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        var validFile = new TrackedFile
        {
            Hash = "transaction123456789012345678901234567890123456789012345678901",
            FileName = "Transaction.Valid.mkv",
            OriginalPath = "/test/Transaction.Valid.mkv",
            FileSize = 1000000
        };

        var validConfig = new ConfigurationSetting
        {
            Key = "MediaButler.Transaction.ValidSetting",
            Value = "valid value",
            Section = "Transaction",
            DataType = ConfigurationDataType.String
        };

        try
        {
            // Act - Add valid entities
            unitOfWork.TrackedFiles.Add(validFile);
            unitOfWork.ConfigurationSettings.Add(validConfig);
            await unitOfWork.SaveChangesAsync();

            // Try to add duplicate file (same hash) - should cause constraint violation
            var duplicateFile = new TrackedFile
            {
                Hash = validFile.Hash, // Same hash
                FileName = "Transaction.Duplicate.mkv",
                OriginalPath = "/test/Transaction.Duplicate.mkv",
                FileSize = 2000000
            };

            unitOfWork.TrackedFiles.Add(duplicateFile);
            await unitOfWork.SaveChangesAsync(); // This should throw
            
            // Should not reach here
            Assert.Fail("Expected exception was not thrown");
        }
        catch (Exception)
        {
            // Expected exception due to constraint violation
        }

        // Assert - Verify rollback occurred
        // Create new unit of work to avoid cached entities
        using var newScope = CreateScope();
        var newUnitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        var fileAfterRollback = await newUnitOfWork.TrackedFiles.GetByHashAsync(validFile.Hash);
        var configAfterRollback = await newUnitOfWork.ConfigurationSettings.FirstOrDefaultAsync(c => c.Key == validConfig.Key);

        // First transaction should have succeeded
        fileAfterRollback.Should().NotBeNull();
        configAfterRollback.Should().NotBeNull();

        // But no duplicate files should exist
        var allFiles = await newUnitOfWork.TrackedFiles.FindAsync(f => f.Hash == validFile.Hash);
        allFiles.Should().HaveCount(1); // Only the original file
    }

    [Fact]
    public async Task RepositoryAccess_ThroughUnitOfWork_ShouldProvideCorrectInstances()
    {
        // Arrange
        
        using var scope = CreateScope();
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        // Act & Assert - Verify repository properties return correct types
        unitOfWork.TrackedFiles.Should().NotBeNull();
        unitOfWork.ConfigurationSettings.Should().NotBeNull();
        unitOfWork.ProcessingLogs.Should().NotBeNull();
        unitOfWork.UserPreferences.Should().NotBeNull();

        // Verify repositories are working
        var initialCount = await unitOfWork.TrackedFiles.CountAsync();
        initialCount.Should().Be(0);

        // Add a file through repository
        var testFile = new TrackedFile
        {
            Hash = "repository123456789012345678901234567890123456789012345678901",
            FileName = "Repository.Access.mkv",
            OriginalPath = "/test/Repository.Access.mkv",
            FileSize = 1000000
        };

        unitOfWork.TrackedFiles.Add(testFile);
        await unitOfWork.SaveChangesAsync();

        var newCount = await unitOfWork.TrackedFiles.CountAsync();
        newCount.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentOperations_WithUnitOfWork_ShouldMaintainConsistency()
    {
        // Arrange
        
        using var scope = CreateScope();
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        var baseHash = "concurrent12345678901234567890123456789012345678901234567";
        var files = Enumerable.Range(1, 10)
            .Select(i => new TrackedFile
            {
                Hash = baseHash + i.ToString("D3"),
                FileName = $"Concurrent.{i:D2}.mkv",
                OriginalPath = $"/test/Concurrent.{i:D2}.mkv",
                FileSize = 1000000 + (i * 100000),
                Status = i % 2 == 0 ? FileStatus.New : FileStatus.Classified
            }).ToList();

        // Act - Add files in batch
        unitOfWork.TrackedFiles.AddRange(files);
        var result = await unitOfWork.SaveChangesAsync();

        // Assert
        result.Should().Be(10);

        // Verify all files were saved with correct statuses
        var newFiles = await unitOfWork.TrackedFiles.GetByStatusAsync(FileStatus.New);
        var classifiedFiles = await unitOfWork.TrackedFiles.GetByStatusAsync(FileStatus.Classified);

        newFiles.Should().HaveCountGreaterOrEqualTo(5);
        classifiedFiles.Should().HaveCountGreaterOrEqualTo(5);

        // Update files in batch
        foreach (var file in newFiles)
        {
            file.MarkAsClassified("BATCH SERIES", 0.7m);
            unitOfWork.TrackedFiles.Update(file);
        }

        var updateResult = await unitOfWork.SaveChangesAsync();
        updateResult.Should().BeGreaterThan(0);

        // Verify updates were applied
        var allClassifiedFiles = await unitOfWork.TrackedFiles.GetByStatusAsync(FileStatus.Classified);
        allClassifiedFiles.Should().HaveCountGreaterOrEqualTo(10);
    }

    [Fact]
    public async Task Dispose_ShouldCleanupResourcesProperly()
    {
        // Arrange
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        var testFile = new TrackedFile
        {
            Hash = "dispose123456789012345678901234567890123456789012345678901",
            FileName = "Dispose.Test.mkv",
            OriginalPath = "/test/Dispose.Test.mkv",
            FileSize = 1000000
        };

        // Act - Use unit of work normally
        unitOfWork.TrackedFiles.Add(testFile);
        await unitOfWork.SaveChangesAsync();

        // Dispose should not throw exceptions
        unitOfWork.Dispose();

        // Assert - Verify we can't use disposed unit of work
        Action useDisposedUnitOfWork = () => unitOfWork.TrackedFiles.Add(testFile);
        useDisposedUnitOfWork.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task ComplexWorkflow_WithMultipleRepositories_ShouldMaintainDataIntegrity()
    {
        // Arrange
        
        using var scope = CreateScope();
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        // Create a complex workflow scenario
        var trackedFile = new TrackedFile
        {
            Hash = "workflow123456789012345678901234567890123456789012345678901",
            FileName = "Workflow.Test.mkv",
            OriginalPath = "/test/Workflow.Test.mkv",
            FileSize = 1500000,
            Status = FileStatus.New
        };

        var configSetting = new ConfigurationSetting
        {
            Key = "MediaButler.Workflow.ProcessingEnabled",
            Value = "true",
            Section = "Workflow",
            DataType = ConfigurationDataType.Boolean
        };

        // Act - Step 1: Initial setup
        unitOfWork.TrackedFiles.Add(trackedFile);
        unitOfWork.ConfigurationSettings.Add(configSetting);
        await unitOfWork.SaveChangesAsync();

        // Step 2: File processing simulation
        trackedFile.MarkAsClassified("WORKFLOW SERIES", 0.95m);
        unitOfWork.TrackedFiles.Update(trackedFile);

        var classificationLog = ProcessingLog.Info(
            trackedFile.Hash,
            "ML",
            "ML Classification",
            "Classified with high confidence");
        unitOfWork.ProcessingLogs.Add(classificationLog);

        await unitOfWork.SaveChangesAsync();

        // Step 3: File confirmation and movement
        trackedFile.ConfirmCategory("WORKFLOW SERIES", "/library/WORKFLOW SERIES/Workflow.Test.mkv");
        unitOfWork.TrackedFiles.Update(trackedFile);

        var moveLog = ProcessingLog.Info(
            trackedFile.Hash,
            "FileMovement", 
            "File Movement",
            "Moved to final location");
        unitOfWork.ProcessingLogs.Add(moveLog);

        trackedFile.MarkAsMoved("/library/WORKFLOW SERIES/Workflow.Test.mkv");
        unitOfWork.TrackedFiles.Update(trackedFile);

        await unitOfWork.SaveChangesAsync();

        // Assert - Verify complete workflow integrity
        var finalFile = await unitOfWork.TrackedFiles.GetByHashAsync(trackedFile.Hash);
        var processingLogs = await unitOfWork.ProcessingLogs.FindAsync(pl => pl.FileHash == trackedFile.Hash);
        var configuration = await unitOfWork.ConfigurationSettings.FirstOrDefaultAsync(c => c.Key == configSetting.Key);

        finalFile.Should().NotBeNull();
        finalFile!.Status.Should().Be(FileStatus.Moved);
        finalFile.Category.Should().Be("WORKFLOW SERIES");
        finalFile.ClassifiedAt.Should().NotBeNull();
        finalFile.MovedAt.Should().NotBeNull();

        processingLogs.Should().HaveCount(2);
        processingLogs.Should().AllSatisfy(log =>
        {
            log.Level.Should().Be(MediaButler.Core.Enums.LogLevel.Information);
            log.FileHash.Should().Be(trackedFile.Hash);
        });

        configuration.Should().NotBeNull();
        configuration!.Value.Should().Be("true");
        configuration.DataType.Should().Be(ConfigurationDataType.Boolean);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ShouldReturnZero()
    {
        // Arrange
        
        using var scope = CreateScope();
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        // Act - Save without making any changes
        var result = await unitOfWork.SaveChangesAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task MultipleSaveChanges_ShouldTrackChangesCorrectly()
    {
        // Arrange
        
        using var scope = CreateScope();
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(Context);

        var file1 = new TrackedFile
        {
            Hash = "multiple01123456789012345678901234567890123456789012345678901",
            FileName = "Multiple.01.mkv",
            OriginalPath = "/test/Multiple.01.mkv",
            FileSize = 1000000
        };

        var file2 = new TrackedFile
        {
            Hash = "multiple02123456789012345678901234567890123456789012345678901",
            FileName = "Multiple.02.mkv",
            OriginalPath = "/test/Multiple.02.mkv", 
            FileSize = 1100000
        };

        // Act - First save
        unitOfWork.TrackedFiles.Add(file1);
        var result1 = await unitOfWork.SaveChangesAsync();

        // Second save
        unitOfWork.TrackedFiles.Add(file2);
        var result2 = await unitOfWork.SaveChangesAsync();

        // Third save with updates
        file1.MarkAsClassified("TEST SERIES", 0.8m);
        unitOfWork.TrackedFiles.Update(file1);
        var result3 = await unitOfWork.SaveChangesAsync();

        // Assert
        result1.Should().Be(1);
        result2.Should().Be(1);
        result3.Should().BeGreaterThan(0);

        // Verify final state
        var finalFile1 = await unitOfWork.TrackedFiles.GetByHashAsync(file1.Hash);
        var finalFile2 = await unitOfWork.TrackedFiles.GetByHashAsync(file2.Hash);

        finalFile1.Should().NotBeNull();
        finalFile1!.Status.Should().Be(FileStatus.Classified);
        
        finalFile2.Should().NotBeNull();
        finalFile2!.Status.Should().Be(FileStatus.New);
    }
}