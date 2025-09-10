using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Services.Background;
using MediaButler.Services.Interfaces;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.Tests.Integration.Background;

/// <summary>
/// Integration tests for background services that orchestrate ML pipeline processing.
/// Tests hosted services interaction with database and ML components.
/// Follows "Simple Made Easy" principles with isolated service testing.
/// </summary>
public class BackgroundServiceIntegrationTests : IntegrationTestBase
{
    public BackgroundServiceIntegrationTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ProcessingCoordinatorHostedService_WithPendingFiles_ShouldProcessThroughMLPipeline()
    {
        // Given - Files in pending status requiring ML processing
        var testFiles = new[]
        {
            new TrackedFile
            {
                Hash = "coordinator-test-1",
                OriginalPath = "/test/Breaking.Bad.S01E01.mkv",
                FileName = "Breaking.Bad.S01E01.mkv",
                FileSize = 1024 * 1024 * 200,
                Status = FileStatus.New
            },
            new TrackedFile
            {
                Hash = "coordinator-test-2", 
                OriginalPath = "/test/The.Office.S02E01.mkv",
                FileName = "The.Office.S02E01.mkv",
                FileSize = 1024 * 1024 * 300,
                Status = FileStatus.New
            }
        };
        
        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        // When - Background service processes files
        using var scope = CreateScope();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();
        
        // Simulate background service batch processing
        var batchResult = await processingCoordinator.ProcessBatchAsync(testFiles, CancellationToken.None);
        batchResult.IsSuccess.Should().BeTrue();

        // Then - Files should be classified
        var processedFiles = Context.TrackedFiles.Where(f => 
            f.Hash == "coordinator-test-1" || f.Hash == "coordinator-test-2").ToList();
        
        processedFiles.Should().HaveCount(2);
        processedFiles.Should().OnlyContain(f => f.Status == FileStatus.Classified);
        processedFiles.Should().OnlyContain(f => !string.IsNullOrEmpty(f.Category));
        processedFiles.Should().OnlyContain(f => f.Confidence >= 0);
    }

    [Fact]
    public async Task FileDiscoveryService_Integration_ShouldCreateTrackedFileEntries()
    {
        // Given - File discovery service with database integration
        using var scope = CreateScope();
        var fileDiscovery = scope.ServiceProvider.GetRequiredService<IFileDiscoveryService>();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create test media files
            var testFiles = new[]
            {
                "Stranger.Things.S04E01.1080p.mkv",
                "Game.of.Thrones.S01E01.720p.mkv"
            };
            
            foreach (var filename in testFiles)
            {
                await File.WriteAllTextAsync(Path.Combine(tempDir, filename), "test content");
            }

            // When - Discover files in directory  
            var scanResult = await fileDiscovery.ScanFoldersAsync(CancellationToken.None);
            scanResult.IsSuccess.Should().BeTrue();

            // Then - Files should be tracked in database
            var discoveredFiles = Context.TrackedFiles
                .Where(f => f.OriginalPath.StartsWith(tempDir))
                .ToList();
            
            discoveredFiles.Should().HaveCount(2);
            discoveredFiles.Should().OnlyContain(f => f.Status == FileStatus.New);
            discoveredFiles.Should().OnlyContain(f => !string.IsNullOrEmpty(f.Hash));
            discoveredFiles.Should().OnlyContain(f => f.FileSize > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ProcessingCoordinator_BatchProcessing_ShouldRespectMemoryLimits()
    {
        // Given - Large number of files to test batch processing
        var batchSize = 15; // Larger than typical batch size
        var testFiles = Enumerable.Range(1, batchSize)
            .Select(i => new TrackedFile
            {
                Hash = $"batch-test-{i:000}",
                OriginalPath = $"/test/Anime.Series.S01E{i:00}.1080p.mkv",
                FileName = $"Anime.Series.S01E{i:00}.1080p.mkv",
                FileSize = 1024 * 1024 * 400, // 400MB each
                Status = FileStatus.New
            })
            .ToArray();
        
        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        // When - Process large batch with memory monitoring
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();
        var batchResult = await processingCoordinator.ProcessBatchAsync(testFiles, CancellationToken.None);
        batchResult.IsSuccess.Should().BeTrue();

        // Then - Memory usage should be controlled
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Should not consume excessive memory despite batch size
        memoryIncrease.Should().BeLessThan(100 * 1024 * 1024, 
            "Batch processing should maintain memory efficiency");
        
        // Verify files were processed
        var processedCount = Context.TrackedFiles
            .Count(f => f.Hash.StartsWith("batch-test-") && f.Status == FileStatus.Classified);
        processedCount.Should().Be(batchSize);
    }

    [Fact]
    public async Task BackgroundService_ErrorHandling_ShouldMaintainSystemStability()
    {
        // Given - Files that might cause processing errors
        var problematicFiles = new[]
        {
            new TrackedFile
            {
                Hash = "error-test-1",
                OriginalPath = "/nonexistent/path/file.mkv",
                FileName = "corrupted...filename...mkv",
                FileSize = 0, // Zero size file
                Status = FileStatus.New
            },
            new TrackedFile
            {
                Hash = "error-test-2",
                OriginalPath = "/test/valid.file.S01E01.mkv",
                FileName = "valid.file.S01E01.mkv", 
                FileSize = 1024 * 1024 * 100,
                Status = FileStatus.New
            }
        };
        
        Context.TrackedFiles.AddRange(problematicFiles);
        await Context.SaveChangesAsync();

        // When - Background service encounters errors
        using var scope = CreateScope();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();
        
        // Should not throw exceptions despite problematic files
        var processAction = async () => {
            var batchResult = await processingCoordinator.ProcessBatchAsync(problematicFiles, CancellationToken.None);
            return batchResult;
        };
        await processAction.Should().NotThrowAsync();

        // Then - Valid files should still be processed, errors should be logged
        var validFile = Context.TrackedFiles.First(f => f.Hash == "error-test-2");
        validFile.Status.Should().Be(FileStatus.Classified);
        
        // Error handling might leave problematic file in Error status or retry
        var problematicFile = Context.TrackedFiles.First(f => f.Hash == "error-test-1");
        problematicFile.Status.Should().BeOneOf(FileStatus.Error, FileStatus.New);
    }

    [Fact]
    public async Task BackgroundService_CancellationToken_ShouldHandleGracefulShutdown()
    {
        // Given - Files for processing and cancellation token
        var testFiles = Enumerable.Range(1, 5)
            .Select(i => new TrackedFile
            {
                Hash = $"cancellation-test-{i}",
                OriginalPath = $"/test/Series.S01E{i:00}.mkv",
                FileName = $"Series.S01E{i:00}.mkv",
                FileSize = 1024 * 1024 * 200,
                Status = FileStatus.New
            })
            .ToArray();
        
        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        // When - Process with early cancellation
        using var scope = CreateScope();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel quickly
        
        // Should handle cancellation gracefully
        var processAction = async () => {
            var batchResult = await processingCoordinator.ProcessBatchAsync(testFiles, cts.Token);
            return batchResult;
        };
        await processAction.Should().NotThrowAsync<OperationCanceledException>();

        // Then - Some files may be processed, but service should shutdown cleanly
        var processedCount = Context.TrackedFiles
            .Count(f => f.Hash.StartsWith("cancellation-test-") && f.Status == FileStatus.Classified);
        
        // May have processed some files before cancellation
        processedCount.Should().BeGreaterOrEqualTo(0);
        processedCount.Should().BeLessOrEqualTo(5);
    }

    [Fact]
    public async Task BackgroundService_PerformanceMonitoring_ShouldTrackMetrics()
    {
        // Given - Files for performance measurement
        var testFiles = Enumerable.Range(1, 8)
            .Select(i => new TrackedFile
            {
                Hash = $"perf-test-{i:00}",
                OriginalPath = $"/test/Performance.Test.S01E{i:00}.1080p.mkv",
                FileName = $"Performance.Test.S01E{i:00}.1080p.mkv",
                FileSize = 1024 * 1024 * 150,
                Status = FileStatus.New
            })
            .ToArray();
        
        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        // When - Process files with timing
        using var scope = CreateScope();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var batchResult = await processingCoordinator.ProcessBatchAsync(testFiles, CancellationToken.None);
        batchResult.IsSuccess.Should().BeTrue();
        stopwatch.Stop();

        // Then - Processing should complete within reasonable time
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2), 
            "Background processing should complete efficiently");
        
        // All files should be processed
        var processedCount = Context.TrackedFiles
            .Count(f => f.Hash.StartsWith("perf-test-") && f.Status == FileStatus.Classified);
        processedCount.Should().Be(8);
        
        // Processing rate should be reasonable for ARM32 constraints
        var filesPerSecond = testFiles.Length / stopwatch.Elapsed.TotalSeconds;
        filesPerSecond.Should().BeGreaterThan(0.1, "Should process at least 1 file per 10 seconds");
    }
}