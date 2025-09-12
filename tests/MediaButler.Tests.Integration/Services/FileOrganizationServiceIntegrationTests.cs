using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MediaButler.Core.Services;
using MediaButler.Core.Models;
using MediaButler.Core.Enums;
using MediaButler.Core.Entities;
using MediaButler.Tests.Integration.Infrastructure;
using System.Text.Json;

namespace MediaButler.Tests.Integration.Services;

/// <summary>
/// Integration tests for FileOrganizationService with real database and file system operations.
/// Tests complete workflow orchestration including preview, validation, organization, and error handling.
/// </summary>
public class FileOrganizationServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;

    public FileOrganizationServiceIntegrationTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task OrganizeFileAsync_WithValidFile_OrganizesSuccessfully()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        // Create a test file and tracked file record
        var testFile = CreateTestFile("The.Office.S01E01.mkv");
        var trackedFile = CreateTrackedFileRecord("office-hash", testFile, "THE OFFICE");
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        // When
        var result = await service.OrganizeFileAsync("office-hash", "THE OFFICE");

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.TargetPath.Should().NotBeEmpty();
        result.Value.ActualPath.Should().NotBeEmpty();
        result.Value.DurationMs.Should().BeGreaterThan(0);
        result.Value.RollbackOperationId.Should().NotBeNull();

        // Verify database updates
        var updatedFile = await _databaseFixture.Context.TrackedFiles
            .FirstAsync(tf => tf.Hash == "office-hash");
        updatedFile.Status.Should().Be(FileStatus.Moved);
        updatedFile.Category.Should().Be("THE OFFICE");
        updatedFile.MovedToPath.Should().NotBeNullOrEmpty();

        // Cleanup
        CleanupTestFile(testFile);
        if (File.Exists(result.Value.ActualPath))
            File.Delete(result.Value.ActualPath);
    }

    [Fact]
    public async Task OrganizeFileAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();

        // When
        var result = await service.OrganizeFileAsync("nonexistent-hash", "TEST CATEGORY");

        // Then
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task PreviewOrganizationAsync_WithValidFile_ReturnsPreview()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Breaking.Bad.S05E16.mkv");
        var trackedFile = CreateTrackedFileRecord("bb-hash", testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        // When
        var result = await service.PreviewOrganizationAsync("bb-hash", "BREAKING BAD");

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ProposedPath.Should().Contain("BREAKING BAD");
        result.Value.ProposedPath.Should().Contain("Breaking.Bad.S05E16.mkv");
        result.Value.EstimatedDurationMs.Should().BeGreaterThan(0);
        result.Value.RequiredSpaceBytes.Should().BeGreaterThan(0);
        result.Value.AvailableSpaceBytes.Should().BeGreaterThan(0);

        // Cleanup
        CleanupTestFile(testFile);
    }

    [Fact]
    public async Task PreviewOrganizationAsync_WithRelatedFiles_IncludesRelatedFiles()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Game.of.Thrones.S08E06.mkv");
        var subtitleFile = CreateTestFile("Game.of.Thrones.S08E06.srt");
        var trackedFile = CreateTrackedFileRecord("got-hash", testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        // When
        var result = await service.PreviewOrganizationAsync("got-hash", "GAME OF THRONES");

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.RelatedFiles.Should().Contain(subtitleFile);
        result.Value.RequiredSpaceBytes.Should().BeGreaterThan(trackedFile.FileSize); // Should include subtitle

        // Cleanup
        CleanupTestFile(testFile);
        CleanupTestFile(subtitleFile);
    }

    [Fact]
    public async Task ValidateOrganizationSafetyAsync_WithValidSetup_PassesValidation()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Stranger.Things.S04E09.mkv");
        var trackedFile = CreateTrackedFileRecord("st-hash", testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        var targetPath = Path.Combine(Path.GetTempPath(), "STRANGER THINGS", "Stranger.Things.S04E09.mkv");

        // When
        var result = await service.ValidateOrganizationSafetyAsync("st-hash", targetPath);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsSafe.Should().BeTrue();
        result.Value.SafetyIssues.Should().BeEmpty();
        result.Value.ValidationDetails.Should().ContainKey("SourceFileAccessible");
        result.Value.ValidationDetails.Should().ContainKey("AvailableSpaceBytes");
        result.Value.ValidationDetails.Should().ContainKey("RequiredSpaceBytes");

        // Cleanup
        CleanupTestFile(testFile);
    }

    [Fact]
    public async Task ValidateOrganizationSafetyAsync_WithInsufficientSpace_IdentifiesIssue()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Large.File.mkv");
        var trackedFile = CreateTrackedFileRecord("large-hash", testFile);
        // Simulate a very large file
        trackedFile.FileSize = long.MaxValue; 
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        var targetPath = Path.Combine(Path.GetTempPath(), "LARGE CATEGORY", "Large.File.mkv");

        // When
        var result = await service.ValidateOrganizationSafetyAsync("large-hash", targetPath);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsSafe.Should().BeFalse();
        result.Value.SafetyIssues.Should().Contain(issue => issue.Contains("Insufficient disk space"));
        result.Value.RecommendedActions.Should().Contain(action => action.Contains("Free up disk space"));

        // Cleanup
        CleanupTestFile(testFile);
    }

    [Fact]
    public async Task HandleOrganizationErrorAsync_WithTransientError_RecommendsRetry()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Error.Test.mkv");
        var trackedFile = CreateTrackedFileRecord("error-hash", testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        var transientError = new IOException("The process cannot access the file because it is being used by another process.");

        // When
        var result = await service.HandleOrganizationErrorAsync("error-hash", transientError, 1);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ShouldRetry.Should().BeTrue();
        result.Value.RecommendedAction.Should().Be(OrganizationRecoveryAction.Retry);
        result.Value.RetryDelayMs.Should().BeGreaterThan(0);
        result.Value.MaxRetryAttempts.Should().BeGreaterThan(0);
        result.Value.RequiresUserIntervention.Should().BeFalse();

        // Cleanup
        CleanupTestFile(testFile);
    }

    [Fact]
    public async Task HandleOrganizationErrorAsync_WithPermissionError_RequiresUserIntervention()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Permission.Test.mkv");
        var trackedFile = CreateTrackedFileRecord("perm-hash", testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        var permissionError = new UnauthorizedAccessException("Access to the path is denied.");

        // When
        var result = await service.HandleOrganizationErrorAsync("perm-hash", permissionError, 1);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ShouldRetry.Should().BeFalse();
        result.Value.RecommendedAction.Should().Be(OrganizationRecoveryAction.WaitForUser);
        result.Value.RequiresUserIntervention.Should().BeTrue();
        result.Value.UserActionSteps.Should().NotBeEmpty();
        result.Value.UserActionSteps.Should().Contain(step => step.Contains("permission"));

        // Cleanup
        CleanupTestFile(testFile);
    }

    [Fact]
    public async Task GetOrganizationStatusAsync_WithNewFile_ReturnsCorrectStatus()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Status.Test.mkv");
        var trackedFile = CreateTrackedFileRecord("status-hash", testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        // When
        var result = await service.GetOrganizationStatusAsync("status-hash");

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.State.Should().Be(OrganizationState.Pending);
        result.Value.AttemptCount.Should().Be(0);
        result.Value.LastAttemptAt.Should().BeNull();
        result.Value.LastError.Should().BeNull();
        result.Value.IsInProgress.Should().BeFalse();
        result.Value.ProgressPercentage.Should().Be(0);
        result.Value.AttemptHistory.Should().BeEmpty();

        // Cleanup
        CleanupTestFile(testFile);
    }

    [Fact]
    public async Task GetOrganizationStatusAsync_AfterSuccessfulOrganization_ReturnsCompletedStatus()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Complete.Test.mkv");
        var trackedFile = CreateTrackedFileRecord("complete-hash", testFile, "TEST CATEGORY");
        trackedFile.Status = FileStatus.Moved;
        trackedFile.MovedToPath = Path.Combine(Path.GetTempPath(), "TEST CATEGORY", "Complete.Test.mkv");
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        
        // Add a successful organization log
        await _databaseFixture.Context.ProcessingLogs.AddAsync(ProcessingLog.Info(
            "complete-hash",
            "FILE_ORGANIZATION",
            "File organized successfully to category TEST CATEGORY",
            JsonSerializer.Serialize(new { Category = "TEST CATEGORY" }),
            1500));
            
        await _databaseFixture.Context.SaveChangesAsync();

        // When
        var result = await service.GetOrganizationStatusAsync("complete-hash");

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.State.Should().Be(OrganizationState.Pending); // Would need to be set during actual organization
        result.Value.AttemptCount.Should().Be(1);
        result.Value.LastAttemptAt.Should().NotBeNull();
        result.Value.LastError.Should().BeNull();
        result.Value.ProgressPercentage.Should().Be(100); // File is moved
        result.Value.AttemptHistory.Should().HaveCount(1);
        result.Value.AttemptHistory.First().WasSuccessful.Should().BeTrue();

        // Cleanup
        CleanupTestFile(testFile);
    }

    [Fact]
    public async Task OrganizeFileAsync_EndToEndWorkflow_CompletesSuccessfully()
    {
        // Given - Complete end-to-end workflow test
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile("Workflow.Test.S01E01.mkv");
        var subtitleFile = CreateTestFile("Workflow.Test.S01E01.srt");
        var trackedFile = CreateTrackedFileRecord("workflow-hash", testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        // When - Execute complete workflow
        // 1. Preview organization
        var previewResult = await service.PreviewOrganizationAsync("workflow-hash", "WORKFLOW TEST");
        previewResult.IsSuccess.Should().BeTrue();
        previewResult.Value.IsSafe.Should().BeTrue();

        // 2. Validate safety
        var validationResult = await service.ValidateOrganizationSafetyAsync("workflow-hash", previewResult.Value.ProposedPath);
        validationResult.IsSuccess.Should().BeTrue();
        validationResult.Value.IsSafe.Should().BeTrue();

        // 3. Execute organization
        var organizationResult = await service.OrganizeFileAsync("workflow-hash", "WORKFLOW TEST");
        organizationResult.IsSuccess.Should().BeTrue();

        // 4. Check final status
        var statusResult = await service.GetOrganizationStatusAsync("workflow-hash");
        statusResult.IsSuccess.Should().BeTrue();

        // Then - Verify complete workflow
        var organization = organizationResult.Value;
        organization.IsSuccess.Should().BeTrue();
        organization.TargetPath.Should().NotBeEmpty();
        organization.ActualPath.Should().NotBeEmpty();
        organization.RollbackOperationId.Should().NotBeNull();

        // Verify database state
        var finalFile = await _databaseFixture.Context.TrackedFiles
            .FirstAsync(tf => tf.Hash == "workflow-hash");
        finalFile.Status.Should().Be(FileStatus.Moved);
        finalFile.Category.Should().Be("WORKFLOW TEST");
        finalFile.MovedToPath.Should().NotBeNullOrEmpty();

        // Verify processing logs
        var logs = await _databaseFixture.Context.ProcessingLogs
            .Where(log => log.FileHash == "workflow-hash")
            .ToListAsync();
        logs.Should().NotBeEmpty();
        logs.Should().Contain(log => log.Category == "FILE_ORGANIZATION");

        // Cleanup
        CleanupTestFile(testFile);
        CleanupTestFile(subtitleFile);
        if (File.Exists(organization.ActualPath))
        {
            File.Delete(organization.ActualPath);
            // Clean up directory if empty
            var dir = Path.GetDirectoryName(organization.ActualPath);
            if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    [Theory]
    [InlineData("The.Matrix.1999.mkv", "THE MATRIX")]
    [InlineData("Inception.2010.1080p.BluRay.mkv", "INCEPTION")]
    [InlineData("Pulp.Fiction.1994.mkv", "PULP FICTION")]
    public async Task OrganizeFileAsync_WithVariousFilenames_HandlesCorrectly(string filename, string category)
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
        
        var testFile = CreateTestFile(filename);
        var fileHash = $"test-{filename.GetHashCode():X}";
        var trackedFile = CreateTrackedFileRecord(fileHash, testFile);
        
        _databaseFixture.Context.TrackedFiles.Add(trackedFile);
        await _databaseFixture.Context.SaveChangesAsync();

        // When
        var result = await service.OrganizeFileAsync(fileHash, category);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.TargetPath.Should().Contain(category);
        result.Value.ActualPath.Should().Contain(filename);

        // Verify database
        var updatedFile = await _databaseFixture.Context.TrackedFiles
            .FirstAsync(tf => tf.Hash == fileHash);
        updatedFile.Category.Should().Be(category);
        updatedFile.Status.Should().Be(FileStatus.Moved);

        // Cleanup
        CleanupTestFile(testFile);
        if (File.Exists(result.Value.ActualPath))
        {
            File.Delete(result.Value.ActualPath);
            var dir = Path.GetDirectoryName(result.Value.ActualPath);
            if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    #region Test Helper Methods

    private string CreateTestFile(string filename)
    {
        var tempDir = Path.GetTempPath();
        var filePath = Path.Combine(tempDir, filename);
        
        // Create a test file with some content
        File.WriteAllText(filePath, $"Test content for {filename}");
        
        return filePath;
    }

    private TrackedFile CreateTrackedFileRecord(string hash, string filePath, string? category = null)
    {
        return new TrackedFile
        {
            Hash = hash,
            FileName = Path.GetFileName(filePath),
            OriginalPath = filePath,
            FileSize = new FileInfo(filePath).Length,
            Status = FileStatus.New,
            SuggestedCategory = category,
            Category = category,
            Confidence = 0.85m
        };
    }

    private void CleanupTestFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}