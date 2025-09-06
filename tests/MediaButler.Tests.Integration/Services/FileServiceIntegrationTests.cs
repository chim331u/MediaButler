using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MediaButler.Tests.Integration.Services;

/// <summary>
/// Integration tests for FileService with real database operations.
/// Tests the complete data flow from service layer through repositories to database.
/// Follows "Simple Made Easy" principles - testing actual end-to-end behavior.
/// </summary>
public class FileServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public FileServiceIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetFileByHashAsync_WithRealDatabase_ShouldReturnPersistedFile()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        var testFile = new TrackedFile
        {
            Hash = "service123456789012345678901234567890123456789012345678901",
            FileName = "Service.Integration.mkv",
            OriginalPath = "/integration/Service.Integration.mkv",
            FileSize = 1234567,
            Status = FileStatus.New
        };

        // Persist file directly to database
        _fixture.Context.TrackedFiles.Add(testFile);
        await _fixture.Context.SaveChangesAsync();

        // Create service with real dependencies
        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        // Act
        var result = await fileService.GetFileByHashAsync(testFile.Hash);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Hash.Should().Be(testFile.Hash);
        result.Value.FileName.Should().Be("Service.Integration.mkv");
        result.Value.FileSize.Should().Be(1234567);
        result.Value.Status.Should().Be(FileStatus.New);
    }

    [Fact]
    public async Task UpdateClassificationAsync_WithRealDatabase_ShouldPersistChanges()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        var testFile = new TrackedFile
        {
            Hash = "classification123456789012345678901234567890123456789012345678",
            FileName = "Classification.Integration.mkv",
            OriginalPath = "/integration/Classification.Integration.mkv",
            FileSize = 987654,
            Status = FileStatus.New
        };

        _fixture.Context.TrackedFiles.Add(testFile);
        await _fixture.Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        var suggestedCategory = "INTEGRATION TEST SERIES";
        var confidence = 0.92m;

        // Act
        var result = await fileService.UpdateClassificationAsync(testFile.Hash, suggestedCategory, confidence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SuggestedCategory.Should().Be(suggestedCategory);
        result.Value.Confidence.Should().Be(confidence);
        result.Value.Status.Should().Be(FileStatus.Classified);
        result.Value.ClassifiedAt.Should().NotBeNull();

        // Verify persistence by querying database directly
        var persistedFile = await _fixture.Context.TrackedFiles.FindAsync(testFile.Hash);
        persistedFile.Should().NotBeNull();
        persistedFile!.SuggestedCategory.Should().Be(suggestedCategory);
        persistedFile.Confidence.Should().Be(confidence);
        persistedFile.Status.Should().Be(FileStatus.Classified);
    }

    [Fact]
    public async Task CompleteFileWorkflow_WithRealDatabase_ShouldTransitionCorrectly()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        var testFile = new TrackedFile
        {
            Hash = "workflow123456789012345678901234567890123456789012345678901",
            FileName = "Workflow.Integration.mkv",
            OriginalPath = "/integration/Workflow.Integration.mkv",
            FileSize = 1500000,
            Status = FileStatus.New
        };

        _fixture.Context.TrackedFiles.Add(testFile);
        await _fixture.Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        // Act - Complete workflow: New → Classified → Confirmed → Moved
        
        // Step 1: Classification
        var classificationResult = await fileService.UpdateClassificationAsync(
            testFile.Hash, 
            "WORKFLOW SERIES", 
            0.88m);

        // Step 2: Confirmation
        var confirmationResult = await fileService.ConfirmCategoryAsync(
            testFile.Hash, 
            "WORKFLOW SERIES");

        // Step 3: Mark as moved
        var targetPath = "/library/WORKFLOW SERIES/Workflow.Integration.mkv";
        var moveResult = await fileService.MarkFileAsMovedAsync(
            testFile.Hash, 
            targetPath);

        // Assert each step
        classificationResult.IsSuccess.Should().BeTrue();
        classificationResult.Value.Status.Should().Be(FileStatus.Classified);

        confirmationResult.IsSuccess.Should().BeTrue();
        confirmationResult.Value.Status.Should().Be(FileStatus.ReadyToMove);
        confirmationResult.Value.Category.Should().Be("WORKFLOW SERIES");

        moveResult.IsSuccess.Should().BeTrue();
        moveResult.Value.Status.Should().Be(FileStatus.Moved);
        moveResult.Value.TargetPath.Should().Be(targetPath);
        moveResult.Value.MovedAt.Should().NotBeNull();

        // Verify final state in database
        var finalFile = await _fixture.Context.TrackedFiles.FindAsync(testFile.Hash);
        finalFile.Should().NotBeNull();
        finalFile!.Status.Should().Be(FileStatus.Moved);
        finalFile.SuggestedCategory.Should().Be("WORKFLOW SERIES");
        finalFile.Category.Should().Be("WORKFLOW SERIES");
        finalFile.TargetPath.Should().Be(targetPath);
        finalFile.ClassifiedAt.Should().NotBeNull();
        finalFile.MovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFilesByStatusAsync_WithRealDatabase_ShouldReturnCorrectFiles()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        // Create files in different states
        var newFiles = Enumerable.Range(1, 3)
            .Select(i => new TrackedFile
            {
                Hash = $"status{i:D2}34567890123456789012345678901234567890123456789012345{i:D3}",
                FileName = $"Status.{i:D2}.mkv",
                OriginalPath = $"/integration/Status.{i:D2}.mkv",
                FileSize = 1000000,
                Status = FileStatus.New
            }).ToList();

        var classifiedFiles = Enumerable.Range(4, 2)
            .Select(i => new TrackedFile
            {
                Hash = $"status{i:D2}34567890123456789012345678901234567890123456789012345{i:D3}",
                FileName = $"Status.{i:D2}.mkv",
                OriginalPath = $"/integration/Status.{i:D2}.mkv",
                FileSize = 1000000,
                Status = FileStatus.Classified,
                SuggestedCategory = "STATUS TEST",
                Confidence = 0.8m,
                ClassifiedAt = DateTime.UtcNow
            }).ToList();

        _fixture.Context.TrackedFiles.AddRange(newFiles);
        _fixture.Context.TrackedFiles.AddRange(classifiedFiles);
        await _fixture.Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        // Act
        var newFilesResult = await fileService.GetFilesByStatusAsync(FileStatus.New);
        var classifiedFilesResult = await fileService.GetFilesByStatusAsync(FileStatus.Classified);

        // Assert
        newFilesResult.IsSuccess.Should().BeTrue();
        newFilesResult.Value.Should().HaveCount(3);
        newFilesResult.Value.Should().AllSatisfy(f => f.Status.Should().Be(FileStatus.New));

        classifiedFilesResult.IsSuccess.Should().BeTrue();
        classifiedFilesResult.Value.Should().HaveCount(2);
        classifiedFilesResult.Value.Should().AllSatisfy(f =>
        {
            f.Status.Should().Be(FileStatus.Classified);
            f.SuggestedCategory.Should().Be("STATUS TEST");
            f.Confidence.Should().Be(0.8m);
        });
    }

    [Fact]
    public async Task RecordErrorAsync_WithRealDatabase_ShouldPersistErrorInformation()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        var testFile = new TrackedFile
        {
            Hash = "error123456789012345678901234567890123456789012345678901",
            FileName = "Error.Integration.mkv",
            OriginalPath = "/integration/Error.Integration.mkv",
            FileSize = 800000,
            Status = FileStatus.ReadyToMove
        };

        _fixture.Context.TrackedFiles.Add(testFile);
        await _fixture.Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        var errorMessage = "Integration test error - file not accessible";
        var exceptionDetails = "System.IO.FileNotFoundException: File not found";

        // Act
        var result = await fileService.RecordErrorAsync(testFile.Hash, errorMessage, exceptionDetails);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.LastError.Should().Be(errorMessage);
        result.Value.LastErrorAt.Should().NotBeNull();
        result.Value.RetryCount.Should().Be(1);
        result.Value.Status.Should().Be(FileStatus.Retry);

        // Verify persistence in database
        var persistedFile = await _fixture.Context.TrackedFiles.FindAsync(testFile.Hash);
        persistedFile.Should().NotBeNull();
        persistedFile!.LastError.Should().Be(errorMessage);
        persistedFile.LastErrorAt.Should().NotBeNull();
        persistedFile.RetryCount.Should().Be(1);
        persistedFile.Status.Should().Be(FileStatus.Retry);
    }

    [Fact]
    public async Task DeleteFileAsync_WithRealDatabase_ShouldSoftDelete()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        var testFile = new TrackedFile
        {
            Hash = "delete123456789012345678901234567890123456789012345678901",
            FileName = "Delete.Integration.mkv",
            OriginalPath = "/integration/Delete.Integration.mkv",
            FileSize = 600000,
            Status = FileStatus.New
        };

        _fixture.Context.TrackedFiles.Add(testFile);
        await _fixture.Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        var deletionReason = "Integration test deletion";

        // Act
        var result = await fileService.DeleteFileAsync(testFile.Hash, deletionReason);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify file is soft deleted (not returned by normal queries)
        var getFileResult = await fileService.GetFileByHashAsync(testFile.Hash);
        getFileResult.IsSuccess.Should().BeFalse();
        getFileResult.Error.Should().Contain("not found");

        // Verify file still exists in database but is inactive
        var persistedFile = await _fixture.Context.TrackedFiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Hash == testFile.Hash);

        persistedFile.Should().NotBeNull();
        persistedFile!.IsActive.Should().BeFalse();
        persistedFile.Note.Should().Contain(deletionReason);
    }

    [Fact]
    public async Task ConcurrentOperations_WithRealDatabase_ShouldMaintainDataIntegrity()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        var baseHash = "concurrent12345678901234567890123456789012345678901234567";
        var files = Enumerable.Range(1, 5)
            .Select(i => new TrackedFile
            {
                Hash = baseHash + i.ToString("D3"),
                FileName = $"Concurrent.{i:D2}.mkv",
                OriginalPath = $"/integration/Concurrent.{i:D2}.mkv",
                FileSize = 1000000,
                Status = FileStatus.New
            }).ToList();

        _fixture.Context.TrackedFiles.AddRange(files);
        await _fixture.Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        // Act - Perform concurrent classifications
        var classificationTasks = files.Select(async (file, index) =>
        {
            var category = $"CONCURRENT SERIES {index + 1}";
            var confidence = 0.7m + (index * 0.05m);
            return await fileService.UpdateClassificationAsync(file.Hash, category, confidence);
        }).ToArray();

        var results = await Task.WhenAll(classificationTasks);

        // Assert
        results.Should().AllSatisfy(result => result.IsSuccess.Should().BeTrue());

        // Verify all files were updated correctly in database
        var updatedFiles = await _fixture.Context.TrackedFiles
            .Where(f => f.Hash.StartsWith(baseHash))
            .OrderBy(f => f.Hash)
            .ToListAsync();

        updatedFiles.Should().HaveCount(5);
        updatedFiles.Should().AllSatisfy(f =>
        {
            f.Status.Should().Be(FileStatus.Classified);
            f.SuggestedCategory.Should().StartWith("CONCURRENT SERIES");
            f.Confidence.Should().BeGreaterOrEqualTo(0.7m);
            f.ClassifiedAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task TransactionRollback_OnServiceError_ShouldNotPersistPartialChanges()
    {
        // Arrange
        await _fixture.CleanupAsync();
        
        var validFile = new TrackedFile
        {
            Hash = "transaction123456789012345678901234567890123456789012345678901",
            FileName = "Transaction.Valid.mkv",
            OriginalPath = "/integration/Transaction.Valid.mkv",
            FileSize = 1000000,
            Status = FileStatus.New
        };

        _fixture.Context.TrackedFiles.Add(validFile);
        await _fixture.Context.SaveChangesAsync();

        var repository = new TrackedFileRepository(_fixture.Context);
        var unitOfWork = new MediaButler.Data.UnitOfWork.UnitOfWork(_fixture.Context);
        var logger = new Mock<ILogger<FileService>>().Object;
        var fileService = new FileService(repository, unitOfWork, logger);

        // Act - Try to update with invalid confidence (should fail)
        var invalidResult = await fileService.UpdateClassificationAsync(
            validFile.Hash, 
            "VALID CATEGORY", 
            1.5m); // Invalid confidence > 1.0

        // Assert
        invalidResult.IsFailure.Should().BeTrue();
        invalidResult.Error.Should().Contain("Confidence must be between 0.0 and 1.0");

        // Verify no changes were persisted to database
        var unchangedFile = await _fixture.Context.TrackedFiles.FindAsync(validFile.Hash);
        unchangedFile.Should().NotBeNull();
        unchangedFile!.Status.Should().Be(FileStatus.New); // Should remain unchanged
        unchangedFile.SuggestedCategory.Should().BeNull();
        unchangedFile.Confidence.Should().Be(0);
        unchangedFile.ClassifiedAt.Should().BeNull();
    }
}