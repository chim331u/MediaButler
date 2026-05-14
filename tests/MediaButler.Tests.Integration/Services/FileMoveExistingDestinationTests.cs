using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data;
using MediaButler.Services.FileOperations;
using MediaButler.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using UnitOfWorkImpl = MediaButler.Data.UnitOfWork.UnitOfWork;
using MediaButler.Core.Services;
using MediaButler.Services.Interfaces;
using MediaButler.Services;
using Xunit;

namespace MediaButler.Tests.Integration.Services;

/// <summary>
/// Integration tests for file move operations with existing destination files.
/// Validates the fix for Priority 3 (v1.0.7) - File Already Exists During Move.
/// </summary>
[Collection("Database Tests")]
public class FileMoveExistingDestinationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MediaButlerDbContext _context;
    private readonly string _testSourceDir;
    private readonly string _testTargetDir;
    private readonly IFileOperationService _fileOperationService;

    public FileMoveExistingDestinationTests()
    {
        var services = new ServiceCollection();

        // Setup in-memory database for testing
        services.AddDbContext<MediaButlerDbContext>(options =>
            options.UseInMemoryDatabase($"FileMoveTest_{Guid.NewGuid()}"));

        // Register services
        services.AddScoped<IFileOperationService, FileOperationService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IRollbackService, RollbackService>();
        services.AddScoped<IErrorClassificationService, ErrorClassificationService>();
        services.AddScoped<IOrganizationStateService, OrganizationStateService>();
        services.AddScoped<IOrganizationValidator, MediaButler.Services.Validation.OrganizationValidator>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITrackedFileRepository, TrackedFileRepository>();
        services.AddScoped<IFileOrganizationStateRepository, FileOrganizationStateRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWorkImpl>();
        
        // Add minimal configuration
        var configDict = new Dictionary<string, string?>
        {
            ["MediaButler:FileDiscovery:WatchFolders:0"] = "/tmp/test",
            ["MediaButler:ML:ActiveModelVersion"] = "1.0.0"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<MediaButler.Core.Configuration.IMediaButlerConfiguration, MediaButler.Services.Configuration.MediaButlerConfiguration>();

        services.AddLogging(builder => builder.AddDebug());

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<MediaButlerDbContext>();
        _fileOperationService = _serviceProvider.GetRequiredService<IFileOperationService>();

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

        // Calculate hash and register in database
        var hash = await CalculateHashAsync(sourceFile);
        var trackedFileEntity = new TrackedFile
        {
            Hash = hash,
            FileName = Path.GetFileName(sourceFile),
            OriginalPath = sourceFile,
            Status = FileStatus.New
        };
        _context.TrackedFiles.Add(trackedFileEntity);
        await _context.SaveChangesAsync();

        // Act - Move
        var targetFilePath = Path.Combine(_testTargetDir, Path.GetFileName(sourceFile));
        var result = await _fileOperationService.MoveFileAsync(hash, targetFilePath);

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

        // Calculate hash and register in database
        var hash = await CalculateHashAsync(sourceFile);
        var trackedFileEntity = new TrackedFile
        {
            Hash = hash,
            FileName = Path.GetFileName(sourceFile),
            OriginalPath = sourceFile,
            Status = FileStatus.New
        };
        _context.TrackedFiles.Add(trackedFileEntity);
        await _context.SaveChangesAsync();

        // Act - Move
        var targetFilePath = Path.Combine(_testTargetDir, Path.GetFileName(sourceFile));
        var result = await _fileOperationService.MoveFileAsync(hash, targetFilePath);

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

        // Calculate hash and register in database
        var hash = await CalculateHashAsync(sourceFile);
        var trackedFileEntity = new TrackedFile
        {
            Hash = hash,
            FileName = Path.GetFileName(sourceFile),
            OriginalPath = sourceFile,
            Status = FileStatus.New
        };
        _context.TrackedFiles.Add(trackedFileEntity);
        await _context.SaveChangesAsync();

        // Act - Move file with multiple existing versions
        var targetFilePath = Path.Combine(_testTargetDir, Path.GetFileName(sourceFile));
        var result = await _fileOperationService.MoveFileAsync(hash, targetFilePath);

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

        // Calculate hash and register in database
        var hash = await CalculateHashAsync(sourceVideo);
        var trackedFileEntity = new TrackedFile
        {
            Hash = hash,
            FileName = Path.GetFileName(sourceVideo),
            OriginalPath = sourceVideo,
            Status = FileStatus.New
        };
        _context.TrackedFiles.Add(trackedFileEntity);
        await _context.SaveChangesAsync();

        // Act - Move video file (should also move related files)
        var targetFilePath = Path.Combine(_testTargetDir, Path.GetFileName(sourceVideo));
        var result = await _fileOperationService.MoveFileAsync(hash, targetFilePath);

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
        var moveTasks = sourcePaths.Select(async (sourcePath, index) =>
        {
            // Stagger starts to prevent SQLite locking
            await Task.Delay(index * 30);

            using var scope = _serviceProvider.CreateScope();
            var fileOperationService = scope.ServiceProvider.GetRequiredService<IFileOperationService>();
            var context = scope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
            
            // Calculate hash and register in database
            var hash = await CalculateHashAsync(sourcePath);
            var trackedFileEntity = new TrackedFile
            {
                Hash = hash,
                FileName = Path.GetFileName(sourcePath),
                OriginalPath = sourcePath,
                Status = FileStatus.New
            };
            context.TrackedFiles.Add(trackedFileEntity);
            await context.SaveChangesAsync();

            var targetFilePath = Path.Combine(_testTargetDir, Path.GetFileName(sourcePath));
            return await fileOperationService.MoveFileAsync(hash, targetFilePath);
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

        // Calculate hash and register in database
        var hash = await CalculateHashAsync(sourceFile);
        var trackedFileEntity = new TrackedFile
        {
            Hash = hash,
            FileName = Path.GetFileName(sourceFile),
            OriginalPath = sourceFile,
            Status = FileStatus.New
        };
        _context.TrackedFiles.Add(trackedFileEntity);
        await _context.SaveChangesAsync();

        // Act - Try to move file when suffix limit reached
        var targetFilePath = Path.Combine(_testTargetDir, Path.GetFileName(sourceFile));
        var result = await _fileOperationService.MoveFileAsync(hash, targetFilePath);

        // Assert - Should fail gracefully
        result.IsSuccess.Should().BeFalse("Should fail when suffix limit (100) is reached");
        result.Error.Should().Contain("suffix", "Error message should mention suffix limit");
        File.Exists(sourceFile).Should().BeTrue("Source file should remain when move fails");
    }

    private async Task<string> CalculateHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
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
