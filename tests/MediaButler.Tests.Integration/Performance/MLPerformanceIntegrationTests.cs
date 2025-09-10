using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.ML.Interfaces;
using MediaButler.Services.Background;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace MediaButler.Tests.Integration.Performance;

/// <summary>
/// Performance integration tests for ML pipeline with real data scenarios.
/// Tests memory usage, processing speed, and resource efficiency under ARM32 constraints.
/// Follows "Simple Made Easy" principles with measurable performance criteria.
/// </summary>
public class MLPerformanceIntegrationTests : IntegrationTestBase
{
    public MLPerformanceIntegrationTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MLClassification_RealWorldDataset_ShouldMeetPerformanceTargets()
    {
        // Given - Realistic dataset of Italian and international media files
        var realWorldFilenames = new[]
        {
            // Italian anime content
            "My.Hero.Academia.6x25.The.High.Deep.Blue.Sky.Sub.ITA.1080p.WEB-DLMux.H264-UBi.mkv",
            "Attack.on.Titan.4x28.L.Alba.Dell.Umanita.Sub.ITA.1080p.WEB-DLMux.H264-UBi.mkv",
            "Demon.Slayer.2x11.Non.Permettero.Che.Nessuno.Venga.Ferito.Sub.ITA.1080p.WEB-DLMux.mkv",
            
            // International TV series
            "Breaking.Bad.S05E16.Felina.FINAL.1080p.BluRay.x264-DEMAND.mkv",
            "Game.of.Thrones.S08E06.The.Iron.Throne.1080p.AMZN.WEB-DL.DDP5.1.H264-GoT.mkv",
            "Stranger.Things.S04E09.The.Piggyback.2160p.NF.WEB-DL.x265.HDR.DV.DDP5.1.Atmos-TEPES.mkv",
            
            // Complex patterns
            "[SubsPlease] One Piece - 1089 (1080p) [A1B2C3D4].mkv",
            "The.Mandalorian.S02E08.Chapter.16.The.Rescue.2160p.DSNP.WEB-DL.DDP5.1.Atmos.HDR.HEVC-TEPES.mkv",
            "Squid.Game.S01E01.Red.Light.Green.Light.2160p.NF.WEB-DL.x265.10bit.HDR.DDP5.1.Atmos-TEPES.mkv",
            
            // Edge cases
            "...Invalid...Dots...S01E01...mkv",
            "NoSeasonEpisode.1080p.mkv",
            "Multiple.Spaces  And    Dots.S01E01.mkv"
        };

        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        // When - Process real-world dataset with performance measurement
        var stopwatch = Stopwatch.StartNew();
        var results = new List<(string filename, double processingTime, string category, double confidence)>();
        
        foreach (var filename in realWorldFilenames)
        {
            var fileStopwatch = Stopwatch.StartNew();
            var classifyResult = await classificationService.ClassifyFilenameAsync(filename);
            fileStopwatch.Stop();
            
            if (classifyResult.IsSuccess)
            {
                var result = classifyResult.Value;
                results.Add((filename, fileStopwatch.Elapsed.TotalMilliseconds, result.PredictedCategory, result.Confidence));
            }
        }
        
        stopwatch.Stop();

        // Then - Verify performance targets for ARM32 deployment
        var totalTimeMs = stopwatch.Elapsed.TotalMilliseconds;
        var averageTimePerFile = totalTimeMs / realWorldFilenames.Length;
        var filesPerSecond = realWorldFilenames.Length / stopwatch.Elapsed.TotalSeconds;
        
        // Performance targets for ARM32
        totalTimeMs.Should().BeLessThan(30000, "Total processing should complete within 30 seconds");
        averageTimePerFile.Should().BeLessThan(2500, "Average processing time should be under 2.5 seconds per file");
        filesPerSecond.Should().BeGreaterThan(0.4, "Should process at least 0.4 files per second");
        
        // Verify classification quality
        results.Should().OnlyContain(r => !string.IsNullOrEmpty(r.category));
        results.Should().OnlyContain(r => r.confidence >= 0.0 && r.confidence <= 1.0);
        
        // Valid files should have reasonable confidence
        var validFiles = results.Where(r => !r.filename.Contains("Invalid") && 
                                           r.filename.Contains("S") && 
                                           r.filename.Contains("E")).ToList();
        var averageValidConfidence = validFiles.Average(r => r.confidence);
        averageValidConfidence.Should().BeGreaterThan(0.3f, "Valid files should have decent average confidence");
    }

    [Fact]
    public async Task MLPipeline_MemoryConstraints_ShouldStayWithinARM32Limits()
    {
        // Given - Memory monitoring setup for ARM32 constraints (target <300MB total)
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        // Large batch simulating continuous processing
        var batchFilenames = Enumerable.Range(1, 100)
            .SelectMany(i => new[]
            {
                $"Popular.Anime.Series.{i % 10}.S01E{i:00}.1080p.WEB-DL.mkv",
                $"Western.TV.Show.{i % 5}.S02E{i:00}.720p.BluRay.mkv",
                $"Italian.Content.{i % 3}.6x{i:00}.Sub.ITA.1080p.WEB-DLMux.mkv"
            })
            .Take(100)
            .ToArray();

        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        // When - Process large batch with memory monitoring
        var memorySnapshots = new List<(int fileIndex, long memoryBytes)>();
        
        for (int i = 0; i < batchFilenames.Length; i++)
        {
            var classifyResult = await classificationService.ClassifyFilenameAsync(batchFilenames[i]);
            classifyResult.IsSuccess.Should().BeTrue();
            
            // Take memory snapshots every 10 files
            if (i % 10 == 0)
            {
                GC.Collect();
                var currentMemory = GC.GetTotalMemory(false);
                memorySnapshots.Add((i, currentMemory));
            }
        }
        
        // Final memory check
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Then - Verify ARM32 memory constraints
        var memoryIncrease = finalMemory - initialMemory;
        var maxMemoryDuringProcessing = memorySnapshots.Max(s => s.memoryBytes);
        
        // Memory increase should be minimal (no significant leaks)
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, 
            "Memory increase should be under 50MB for batch processing");
        
        // Peak memory should stay within ARM32 target
        maxMemoryDuringProcessing.Should().BeLessThan(200 * 1024 * 1024, 
            "Peak memory usage should stay under 200MB for sustainable ARM32 operation");
        
        // Memory usage should not grow linearly with batch size
        var firstSnapshot = memorySnapshots[0].memoryBytes;
        var lastSnapshot = memorySnapshots.Last().memoryBytes;
        var memoryGrowthRate = (lastSnapshot - firstSnapshot) / (double)batchFilenames.Length;
        
        memoryGrowthRate.Should().BeLessThan(500 * 1024, 
            "Memory growth per file should be under 500KB to prevent memory leaks");
    }

    [Fact]
    public async Task ConcurrentMLProcessing_MultipleRequests_ShouldMaintainPerformance()
    {
        // Given - Concurrent processing scenario
        var testFilenames = new[]
        {
            "Naruto.Shippuden.S01E01.Homecoming.1080p.mkv",
            "One.Piece.E1001.New.Era.Begins.1080p.mkv", 
            "Attack.on.Titan.S04E01.The.Other.Side.of.the.Sea.1080p.mkv",
            "Demon.Slayer.S01E01.Cruelty.1080p.mkv",
            "Hunter.x.Hunter.S01E01.Departure.1080p.mkv"
        };
        
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();

        // When - Process files concurrently
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = testFilenames.Select(async filename =>
        {
            var fileStopwatch = Stopwatch.StartNew();
            var classifyResult = await classificationService.ClassifyFilenameAsync(filename);
            fileStopwatch.Stop();
            if (classifyResult.IsSuccess)
            {
                return new { filename, result = classifyResult.Value, timeMs = fileStopwatch.Elapsed.TotalMilliseconds };
            }
            return null;
        });
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Then - Verify concurrent processing performance
        var totalConcurrentTime = stopwatch.Elapsed.TotalMilliseconds;
        var averageFileTime = results.Average(r => r.timeMs);
        
        // Concurrent processing should be faster than sequential
        totalConcurrentTime.Should().BeLessThan(averageFileTime * testFilenames.Length, 
            "Concurrent processing should provide some performance benefit");
        
        // All results should be valid
        var validResults = results.Where(r => r != null).ToList();
        validResults.Should().OnlyContain(r => r.result != null);
        validResults.Should().OnlyContain(r => !string.IsNullOrEmpty(r.result.PredictedCategory));
        validResults.Should().OnlyContain(r => r.result.Confidence >= 0.0f);
        
        // Processing times should be reasonable
        validResults.Should().OnlyContain(r => r.timeMs < 5000, 
            "Individual file processing should complete within 5 seconds");
    }

    [Fact]
    public async Task BackgroundProcessing_ContinuousLoad_ShouldMaintainThroughput()
    {
        // Given - Continuous processing simulation
        var continuousFiles = Enumerable.Range(1, 50)
            .Select(i => new TrackedFile
            {
                Hash = $"continuous-{i:000}",
                OriginalPath = $"/test/Continuous.Series.S{(i % 5) + 1:00}E{i:00}.1080p.mkv",
                FileName = $"Continuous.Series.S{(i % 5) + 1:00}E{i:00}.1080p.mkv",
                FileSize = 1024 * 1024 * (200 + i * 5),
                Status = FileStatus.New
            })
            .ToArray();
        
        Context.TrackedFiles.AddRange(continuousFiles);
        await Context.SaveChangesAsync();

        using var scope = CreateScope();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();
        
        // When - Simulate continuous background processing
        var stopwatch = Stopwatch.StartNew();
        var throughputMeasurements = new List<(TimeSpan elapsed, int filesProcessed)>();
        
        // Process in multiple waves to simulate continuous operation
        for (int wave = 0; wave < 5; wave++)
        {
            var remainingFiles = Context.TrackedFiles.Where(f => f.Hash.StartsWith("continuous-") && f.Status == FileStatus.New).ToList();
            if (remainingFiles.Any())
            {
                var batchResult = await processingCoordinator.ProcessBatchAsync(remainingFiles, CancellationToken.None);
                batchResult.IsSuccess.Should().BeTrue();
            }
            
            var processedCount = Context.TrackedFiles
                .Count(f => f.Hash.StartsWith("continuous-") && f.Status == FileStatus.Classified);
            
            throughputMeasurements.Add((stopwatch.Elapsed, processedCount));
            
            if (processedCount >= continuousFiles.Length)
                break;
                
            // Small delay to simulate real-world conditions
            await Task.Delay(500);
        }
        
        stopwatch.Stop();

        // Then - Verify sustained throughput
        var finalProcessedCount = throughputMeasurements.Last().filesProcessed;
        var totalThroughputTime = throughputMeasurements.Last().elapsed.TotalSeconds;
        var filesPerSecond = finalProcessedCount / totalThroughputTime;
        
        finalProcessedCount.Should().Be(continuousFiles.Length, "All files should be processed");
        filesPerSecond.Should().BeGreaterThan(0.5, "Should maintain at least 0.5 files per second throughput");
        totalThroughputTime.Should().BeLessThan(100, "Total processing should complete within 100 seconds");
        
        // Verify processing quality under load
        var processedFiles = Context.TrackedFiles
            .Where(f => f.Hash.StartsWith("continuous-") && f.Status == FileStatus.Classified)
            .ToList();
        
        processedFiles.Should().OnlyContain(f => !string.IsNullOrEmpty(f.Category));
        processedFiles.Should().OnlyContain(f => f.Confidence >= 0.0m);
        
        var averageConfidence = processedFiles.Average(f => (double)f.Confidence);
        averageConfidence.Should().BeGreaterThan(0.2, "Average confidence should remain reasonable under load");
    }

    [Fact]
    public async Task MLPipeline_ResourceRecovery_ShouldCleanupAfterProcessing()
    {
        // Given - Intensive processing scenario with resource monitoring
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        var intensiveFiles = Enumerable.Range(1, 25)
            .Select(i => $"Resource.Intensive.Processing.{i % 3}.S{(i % 5) + 1:00}E{i:00}.2160p.HDR.DV.Atmos.mkv")
            .ToArray();

        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        // When - Process intensive workload
        var resourceSnapshots = new List<(int fileIndex, long memory, int gen0, int gen1, int gen2)>();
        
        for (int i = 0; i < intensiveFiles.Length; i++)
        {
            var classifyResult = await classificationService.ClassifyFilenameAsync(intensiveFiles[i]);
            classifyResult.IsSuccess.Should().BeTrue();
            
            // Monitor GC activity
            if (i % 5 == 0)
            {
                var gen0 = GC.CollectionCount(0);
                var gen1 = GC.CollectionCount(1);
                var gen2 = GC.CollectionCount(2);
                var memory = GC.GetTotalMemory(false);
                
                resourceSnapshots.Add((i, memory, gen0, gen1, gen2));
            }
        }
        
        // Force cleanup and measure final state
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        // Then - Verify resource cleanup
        var memoryIncrease = finalMemory - initialMemory;
        var gcActivity = resourceSnapshots.Last().gen0 - resourceSnapshots.First().gen0;
        
        // Memory should return close to initial levels
        memoryIncrease.Should().BeLessThan(30 * 1024 * 1024, 
            "Memory increase after cleanup should be minimal (under 30MB)");
        
        // GC should be active but not excessive
        gcActivity.Should().BeGreaterThan(0, "Garbage collection should be active during processing");
        gcActivity.Should().BeLessThan(50, "Excessive GC activity indicates memory pressure");
        
        // Memory usage should not show linear growth pattern
        var memoryGrowthRate = resourceSnapshots
            .Zip(resourceSnapshots.Skip(1), (first, second) => second.memory - first.memory)
            .Average();
            
        Math.Abs(memoryGrowthRate).Should().BeLessThan(5 * 1024 * 1024, 
            "Memory growth rate should be stable (under 5MB per batch)");
    }
}