using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data;
using MediaButler.Services.FileOperations;
using MediaButler.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MediaButler.Tests.Integration.Services;

/// <summary>
/// Integration tests for file move operations with existing destination files.
/// Validates the fix for Priority 3 (v1.0.7) - File Already Exists During Move.
/// </summary>
public class FileMoveExistingDestinationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MediaButlerDbContext _context;
    private readonly string _testSourceDir;
    private readonly string _testTargetDir;

    public FileMoveExistingDestinationTests()
    {
        var services = new ServiceCollection();

        // Setup in-memory database for testing
        services.AddDbContext<MediaButlerDbContext>(options =>
            options.UseInMemoryDatabase($"FileMoveTest_{Guid.NewGuid()}"));

        // Register services
        services.AddScoped<IFileOperationService, FileOperationService>();
        services.AddLogging(builder => builder.AddDebug());

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<MediaButlerDbContext>();

        // Create temporary test directories
        _testSourceDir = Path.Combine(Path.GetTempPath(), $"MediaButler_Source_{Guid.NewGuid()}");
        _testTargetDir = Path.Combine(Path.GetTempPath(), $"MediaButler_Target_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSourceDir);
        Directory.CreateDirectory(_testTargetDir);
    }

    [Fact]
    public async Task MoveFile_WhenDestinationExistsWithSameHash_ShouldDeleteSourceAndUseExisting()
    {
        // Arrange - Create identical files in source and target
        var sourceFile = Path.Combine(_testSourceDir, "Breaking.Bad.S01E01.mkv");
        var targetFile = Path.Combine(_testTargetDir, "Breaking.Bad.S01E01.mkv");

        var identicalContent = "Identical video file content for hash matching test";
        await File.WriteAllTextAsync(sourceFile, identicalContent);
        await File.WriteAllTextAsync(targetFile, identicalContent);

        using var scope = _serviceProvider.CreateScope();
        var fileOperationService = scope.ServiceProvider.GetRequiredService<IFileOperationService>();

        // Act - Move file to existing destination
        var result = await fileOperationService.MoveFileAsync(sourceFile, _testTargetDir);

        // Assert - Source deleted, target preserved
        result.IsSuccess.Should().BeTrue();
        File.Exists(sourceFile).Should().BeFalse("Source file should be deleted when target is identical");
        File.Exists(targetFile).Should().BeTrue("Target file should remain when identical to source");

        var content = await File.ReadAllTextAsync(targetFile);
        content.Should().Be(identicalContent, "Target file content should be unchanged");
    }

    [Fact]
    public async Task MoveFile_WhenDestinationExistsWithDifferentHash_ShouldRenameWithSuffix()
    {
        // Arrange - Create different files in source and target
        var sourceFile = Path.Combine(_testSourceDir, "The.Office.S02E05.mkv");
        var targetFile = Path.Combine(_testTargetDir, "The.Office.S02E05.mkv");

        await File.WriteAllTextAsync(sourceFile, "Source video content - different from target");
        await File.WriteAllTextAsync(targetFile, "Target video content - different from source");

        using var scope = _serviceProvider.CreateScope();
        var fileOperationService = scope.ServiceProvider.GetRequiredService<IFileOperationService>();

        // Act - Move file to existing destination
        var result = await fileOperationService.MoveFileAsync(sourceFile, _testTargetDir);

        // Assert - Source moved with renamed target
        result.IsSuccess.Should().BeTrue();
        File.Exists(sourceFile).Should().BeFalse("Source file should be moved");
        File.Exists(targetFile).Should().BeTrue("Original target file should remain");

        var renamedFile = Path.Combine(_testTargetDir, "The.Office.S02E05_1.mkv");
        File.Exists(renamedFile).Should().BeTrue("Renamed file should exist with _1 suffix");

        var renamedContent = await File.ReadAllTextAsync(renamedFile);
        renamedContent.Should().Be("Source video content - different from target",
            "Renamed file should contain source content");
    }

    [Fact]
    public async Task MoveFile_WithMultipleSuffixCollisions_ShouldIncrementSuffix()
    {
        // Arrange - Create multiple files with same name
        var sourceFile = Path.Combine(_testSourceDir, "Game.Of.Thrones.S01E01.mkv");
        var targetFile = Path.Combine(_testTargetDir, "Game.Of.Thrones.S01E01.mkv");
        var targetFile1 = Path.Combine(_testTargetDir, "Game.Of.Thrones.S01E01_1.mkv");
        var targetFile2 = Path.Combine(_testTargetDir, "Game.Of.Thrones.S01E01_2.mkv");

        await File.WriteAllTextAsync(sourceFile, "Source content - version 4");
        await File.WriteAllTextAsync(targetFile, "Target content - version 1");
        await File.WriteAllTextAsync(targetFile1, "Target content - version 2");
        await File.WriteAllTextAsync(targetFile2, "Target content - version 3");

        using var scope = _serviceProvider.CreateScope();
        var fileOperationService = scope.ServiceProvider.GetRequiredService<IFileOperationService>();

        // Act - Move file with multiple existing versions
        var result = await fileOperationService.MoveFileAsync(sourceFile, _testTargetDir);

        // Assert - Source moved with _3 suffix
        result.IsSuccess.Should().BeTrue();
        File.Exists(sourceFile).Should().BeFalse("Source file should be moved");

        var renamedFile = Path.Combine(_testTargetDir, "Game.Of.Thrones.S01E01_3.mkv");
        File.Exists(renamedFile).Should().BeTrue("Renamed file should exist with _3 suffix");

        var renamedContent = await File.ReadAllTextAsync(renamedFile);
        renamedContent.Should().Be("Source content - version 4");
    }

    [Fact]
    public async Task MoveFile_WithRelatedFiles_ShouldMoveAllFilesCorrectly()
    {
        // Arrange - Create video file with subtitle and metadata
        var sourceVideo = Path.Combine(_testSourceDir, "Stranger.Things.S03E08.mkv");
        var sourceSubtitle = Path.Combine(_testSourceDir, "Stranger.Things.S03E08.srt");
        var sourceMetadata = Path.Combine(_testSourceDir, "Stranger.Things.S03E08.nfo");

        var targetVideo = Path.Combine(_testTargetDir, "Stranger.Things.S03E08.mkv");

        await File.WriteAllTextAsync(sourceVideo, "Video content");
        await File.WriteAllTextAsync(sourceSubtitle, "Subtitle content");
        await File.WriteAllTextAsync(sourceMetadata, "Metadata content");
        await File.WriteAllTextAsync(targetVideo, "Different video content");

        using var scope = _serviceProvider.CreateScope();
        var fileOperationService = scope.ServiceProvider.GetRequiredService<IFileOperationService>();

        // Act - Move video file (should also move related files)
        var result = await fileOperationService.MoveFileAsync(sourceVideo, _testTargetDir);

        // Assert - All files moved with correct suffix
        result.IsSuccess.Should().BeTrue();
        File.Exists(sourceVideo).Should().BeFalse("Source video should be moved");
        File.Exists(sourceSubtitle).Should().BeFalse("Source subtitle should be moved");
        File.Exists(sourceMetadata).Should().BeFalse("Source metadata should be moved");

        var renamedVideo = Path.Combine(_testTargetDir, "Stranger.Things.S03E08_1.mkv");
        var renamedSubtitle = Path.Combine(_testTargetDir, "Stranger.Things.S03E08_1.srt");
        var renamedMetadata = Path.Combine(_testTargetDir, "Stranger.Things.S03E08_1.nfo");

        File.Exists(renamedVideo).Should().BeTrue("Renamed video should exist");
        File.Exists(renamedSubtitle).Should().BeTrue("Renamed subtitle should exist");
        File.Exists(renamedMetadata).Should().BeTrue("Renamed metadata should exist");
    }

    [Fact]
    public async Task MoveFile_ConcurrentMovesToSameDestination_ShouldHandleRaceCondition()
    {
        // Arrange - Create multiple source files to move concurrently
        var sourcePaths = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var sourceFile = Path.Combine(_testSourceDir, $"source_{i}", "The.Walking.Dead.S11E24.mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, $"Content variation {i}");
            sourcePaths.Add(sourceFile);
        }

        // Act - Move all files concurrently to same target directory
        var moveTasks = sourcePaths.Select(async sourcePath =>
        {
            using var scope = _serviceProvider.CreateScope();
            var fileOperationService = scope.ServiceProvider.GetRequiredService<IFileOperationService>();
            return await fileOperationService.MoveFileAsync(sourcePath, _testTargetDir);
        }).ToList();

        var results = await Task.WhenAll(moveTasks);

        // Assert - All moves succeeded, files exist with different suffixes
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());

        var targetFiles = Directory.GetFiles(_testTargetDir, "The.Walking.Dead.S11E24*.mkv");
        targetFiles.Should().HaveCount(5, "All 5 files should be moved with unique names");

        // Verify all source files were moved
        foreach (var sourcePath in sourcePaths)
        {
            File.Exists(sourcePath).Should().BeFalse($"Source file {sourcePath} should be moved");
        }
    }

    [Fact]
    public async Task MoveFile_WhenSuffixLimitReached_ShouldReturnError()
    {
        // Arrange - Create 100 existing files (beyond suffix limit)
        var sourceFile = Path.Combine(_testSourceDir, "Limit.Test.S01E01.mkv");
        await File.WriteAllTextAsync(sourceFile, "Source content");

        for (int i = 0; i < 100; i++)
        {
            var targetFile = i == 0
                ? Path.Combine(_testTargetDir, "Limit.Test.S01E01.mkv")
                : Path.Combine(_testTargetDir, $"Limit.Test.S01E01_{i}.mkv");
            await File.WriteAllTextAsync(targetFile, $"Different content {i}");
        }

        using var scope = _serviceProvider.CreateScope();
        var fileOperationService = scope.ServiceProvider.GetRequiredService<IFileOperationService>();

        // Act - Try to move file when suffix limit reached
        var result = await fileOperationService.MoveFileAsync(sourceFile, _testTargetDir);

        // Assert - Should fail gracefully
        result.IsSuccess.Should().BeFalse("Should fail when suffix limit (100) is reached");
        result.Error.Should().Contain("suffix", "Error message should mention suffix limit");
        File.Exists(sourceFile).Should().BeTrue("Source file should remain when move fails");
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();

        // Cleanup test directories
        if (Directory.Exists(_testSourceDir))
        {
            Directory.Delete(_testSourceDir, recursive: true);
        }
        if (Directory.Exists(_testTargetDir))
        {
            Directory.Delete(_testTargetDir, recursive: true);
        }
    }
}
