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
/// Comprehensive memory usage validation tests for ML pipeline processing.
/// Focuses on ARM32 deployment constraints with <300MB target memory footprint.
/// Follows "Simple Made Easy" principles with clear memory measurement boundaries.
/// </summary>
public class MemoryValidationTests : IntegrationTestBase
{
    public MemoryValidationTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MLServices_InitialLoad_ShouldStayWithinMemoryBudget()
    {
        // Given - Clean baseline measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baselineMemory = GC.GetTotalMemory(false);
        
        // When - Initialize all ML services
        using var scope = CreateScope();
        var tokenizerService = scope.ServiceProvider.GetRequiredService<ITokenizerService>();
        var featureService = scope.ServiceProvider.GetRequiredService<IFeatureEngineeringService>();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();
        var modelService = scope.ServiceProvider.GetRequiredService<IMLModelService>();
        
        // Trigger service initialization through simple operations
        var tokenizeResult = tokenizerService.TokenizeFilename("Test.Series.S01E01.mkv");
        tokenizeResult.IsSuccess.Should().BeTrue();
        var modelArchResult = await modelService.GetRecommendedArchitectureAsync();
        modelArchResult.IsSuccess.Should().BeTrue();
        
        GC.Collect();
        var afterInitializationMemory = GC.GetTotalMemory(false);

        // Then - Verify initialization memory footprint
        var initializationMemory = afterInitializationMemory - baselineMemory;
        
        // ML services initialization should be lightweight
        initializationMemory.Should().BeLessThan(100 * 1024 * 1024, 
            "ML services initialization should use less than 100MB");
        
        // Model loading should be reasonable for ARM32
        initializationMemory.Should().BeLessThan(50 * 1024 * 1024, 
            "Initial ML model loading should be under 50MB for ARM32 efficiency");
    }

    [Fact]
    public async Task TokenizerService_HighVolumeProcessing_ShouldNotLeakMemory()
    {
        // Given - Large volume of tokenization operations
        var testFilenames = Enumerable.Range(1, 200)
            .SelectMany(i => new[]
            {
                $"Series.A.{i}.S01E{i:00}.1080p.WEB-DL.H264.mkv",
                $"Anime.B.{i}.6x{i:00}.Sub.ITA.1080p.WEB-DLMux.H265.mkv",
                $"Show.C.{i}.Season.{i % 5 + 1}.Episode.{i:00}.2160p.HDR.mkv"
            })
            .Take(200)
            .ToArray();
        
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var tokenizerService = scope.ServiceProvider.GetRequiredService<ITokenizerService>();
        
        // When - Process high volume of tokenization
        var memoryCheckpoints = new List<(int iteration, long memoryBytes)>();
        
        for (int i = 0; i < testFilenames.Length; i++)
        {
            var result = tokenizerService.TokenizeFilename(testFilenames[i]);
            result.IsSuccess.Should().BeTrue();
            
            // Memory checkpoint every 50 operations
            if (i % 50 == 0)
            {
                GC.Collect();
                var currentMemory = GC.GetTotalMemory(false);
                memoryCheckpoints.Add((i, currentMemory));
            }
        }
        
        // Final memory measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        // Then - Verify no memory leaks in tokenization
        var totalMemoryIncrease = finalMemory - initialMemory;
        
        // Should not accumulate significant memory
        totalMemoryIncrease.Should().BeLessThan(20 * 1024 * 1024, 
            "Tokenizer should not accumulate more than 20MB over 200 operations");
        
        // Memory growth should be stable, not linear
        if (memoryCheckpoints.Count >= 3)
        {
            var growthRates = memoryCheckpoints
                .Zip(memoryCheckpoints.Skip(1), (first, second) => second.memoryBytes - first.memoryBytes)
                .ToList();
            
            var averageGrowth = growthRates.Average();
            var maxGrowth = growthRates.Max();
            
            Math.Abs(averageGrowth).Should().BeLessThan(10 * 1024 * 1024, 
                "Average memory growth per checkpoint should be minimal");
            maxGrowth.Should().BeLessThan(25 * 1024 * 1024, 
                "Maximum memory spike should be under 25MB");
        }
    }

    [Fact]
    public async Task MLClassification_BatchProcessing_ShouldManageMemoryEfficiently()
    {
        // Given - Large batch of files for classification
        var batchFiles = Enumerable.Range(1, 75)
            .Select(i => new TrackedFile
            {
                Hash = $"memory-batch-{i:000}",
                OriginalPath = $"/test/Memory.Test.Series.{i % 8}.S{(i % 5) + 1:00}E{i:00}.1080p.mkv",
                FileName = $"Memory.Test.Series.{i % 8}.S{(i % 5) + 1:00}E{i:00}.1080p.mkv",
                FileSize = 1024 * 1024 * (100 + i * 3), // Varying sizes
                Status = FileStatus.New
            })
            .ToArray();
        
        Context.TrackedFiles.AddRange(batchFiles);
        await Context.SaveChangesAsync();
        
        GC.Collect();
        var preProcessingMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();

        // When - Process large batch with memory monitoring
        var memoryProfile = new List<(int filesProcessed, long memoryUsage, int gen0Collections, int gen1Collections)>();
        var stopwatch = Stopwatch.StartNew();
        
        // Process in smaller sub-batches to monitor memory behavior
        for (int batchStart = 0; batchStart < batchFiles.Length; batchStart += 15)
        {
            var remainingFiles = Context.TrackedFiles.Where(f => f.Hash.StartsWith("memory-batch-") && f.Status == FileStatus.New).Take(15).ToList();
            if (remainingFiles.Any())
            {
                var batchResult = await processingCoordinator.ProcessBatchAsync(remainingFiles, CancellationToken.None);
                batchResult.IsSuccess.Should().BeTrue();
            }
            
            var processedCount = Context.TrackedFiles
                .Count(f => f.Hash.StartsWith("memory-batch-") && f.Status == FileStatus.Classified);
            
            GC.Collect();
            var currentMemory = GC.GetTotalMemory(false);
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            
            memoryProfile.Add((processedCount, currentMemory, gen0, gen1));
            
            if (processedCount >= batchFiles.Length)
                break;
        }
        
        stopwatch.Stop();
        
        // Final cleanup and measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var postProcessingMemory = GC.GetTotalMemory(true);

        // Then - Validate batch processing memory efficiency
        var totalMemoryIncrease = postProcessingMemory - preProcessingMemory;
        var peakMemoryUsage = memoryProfile.Max(p => p.memoryUsage);
        var memoryEfficiencyRatio = (double)batchFiles.Length / (totalMemoryIncrease / (1024.0 * 1024.0));
        
        // Memory increase should be reasonable for batch size
        totalMemoryIncrease.Should().BeLessThan(80 * 1024 * 1024, 
            "Total memory increase should be under 80MB for 75-file batch");
        
        // Peak memory should stay within ARM32 constraints
        peakMemoryUsage.Should().BeLessThan(250 * 1024 * 1024, 
            "Peak memory usage should stay under 250MB for ARM32 sustainability");
        
        // Memory efficiency: should process multiple files per MB of memory increase
        memoryEfficiencyRatio.Should().BeGreaterThan(1.0, 
            "Should process at least 1 file per MB of memory increase");
        
        // GC activity should be reasonable
        var finalGen0 = memoryProfile.Last().gen0Collections;
        var finalGen1 = memoryProfile.Last().gen1Collections;
        
        finalGen0.Should().BeLessThan(100, "Gen0 collections should be reasonable");
        finalGen1.Should().BeLessThan(20, "Gen1 collections should be minimal");
    }

    [Fact]
    public async Task DatabaseOperations_WithMLData_ShouldNotAccumulateMemory()
    {
        // Given - Large number of database operations with ML data
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        // When - Perform many database write/read cycles with ML results
        var memoryMeasurements = new List<(int cycle, long memory)>();
        
        for (int cycle = 0; cycle < 20; cycle++)
        {
            // Create batch of files
            var cycleFiles = Enumerable.Range(1, 10)
                .Select(i => new TrackedFile
                {
                    Hash = $"db-memory-cycle-{cycle}-file-{i}",
                    OriginalPath = $"/test/DB.Memory.Cycle.{cycle}.File.{i}.S01E01.mkv",
                    FileName = $"DB.Memory.Cycle.{cycle}.File.{i}.S01E01.mkv",
                    FileSize = 1024 * 1024 * 200,
                    Status = FileStatus.New
                })
                .ToArray();
            
            // Add, classify, and update files
            Context.TrackedFiles.AddRange(cycleFiles);
            await Context.SaveChangesAsync();
            
            foreach (var file in cycleFiles)
            {
                var classifyResult = await classificationService.ClassifyFilenameAsync(file.FileName);
                if (classifyResult.IsSuccess)
                {
                    var result = classifyResult.Value;
                    file.Category = result.PredictedCategory;
                    file.Confidence = (decimal)result.Confidence;
                    file.Status = FileStatus.Classified;
                    file.MarkAsModified();
                }
            }
            
            await Context.SaveChangesAsync();
            
            // Query back the data
            var retrievedFiles = Context.TrackedFiles
                .Where(f => f.Hash.StartsWith($"db-memory-cycle-{cycle}"))
                .ToList();
            
            retrievedFiles.Should().HaveCount(10);
            
            // Memory measurement
            if (cycle % 5 == 0)
            {
                Context.ChangeTracker.Clear(); // Clear tracking to prevent accumulation
                GC.Collect();
                var cycleMemory = GC.GetTotalMemory(false);
                memoryMeasurements.Add((cycle, cycleMemory));
            }
        }
        
        // Final memory check
        Context.ChangeTracker.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        // Then - Verify database operations don't accumulate memory
        var totalMemoryIncrease = finalMemory - initialMemory;
        
        totalMemoryIncrease.Should().BeLessThan(50 * 1024 * 1024, 
            "Database operations should not accumulate significant memory");
        
        // Memory should remain relatively stable across cycles
        if (memoryMeasurements.Count >= 3)
        {
            var memoryVariance = memoryMeasurements
                .Select(m => m.memory)
                .Aggregate(0.0, (acc, mem) => acc + Math.Pow(mem - memoryMeasurements.Average(m2 => m2.memory), 2)) / memoryMeasurements.Count;
            
            var standardDeviation = Math.Sqrt(memoryVariance);
            
            standardDeviation.Should().BeLessThan(30 * 1024 * 1024, 
                "Memory usage should be stable across database operation cycles");
        }
    }

    [Fact]
    public async Task LongRunningMLOperations_ExtendedSession_ShouldMaintainMemoryStability()
    {
        // Given - Simulation of long-running session (multiple hours compressed)
        GC.Collect();
        var sessionStartMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var processingCoordinator = scope.ServiceProvider.GetRequiredService<IProcessingCoordinator>();
        
        var sessionMetrics = new List<(int iteration, long memory, TimeSpan elapsed, int filesProcessed)>();
        var stopwatch = Stopwatch.StartNew();

        // When - Simulate extended processing session
        for (int iteration = 0; iteration < 10; iteration++)
        {
            // Create files for this iteration
            var iterationFiles = Enumerable.Range(1, 20)
                .Select(i => new TrackedFile
                {
                    Hash = $"longrun-{iteration:00}-{i:00}",
                    OriginalPath = $"/test/LongRun.Iter.{iteration}.File.{i}.S01E01.mkv",
                    FileName = $"LongRun.Iter.{iteration}.File.{i}.S01E01.mkv",
                    FileSize = 1024 * 1024 * (150 + i * 5),
                    Status = FileStatus.New
                })
                .ToArray();
            
            Context.TrackedFiles.AddRange(iterationFiles);
            await Context.SaveChangesAsync();
            
            // Process files
            var iterFiles = Context.TrackedFiles.Where(f => f.Hash.StartsWith($"longrun-{iteration:00}-") && f.Status == FileStatus.New).ToList();
            if (iterFiles.Any())
            {
                var batchResult = await processingCoordinator.ProcessBatchAsync(iterFiles, CancellationToken.None);
                batchResult.IsSuccess.Should().BeTrue();
            }
            
            // Simulate some direct classification calls
            for (int direct = 0; direct < 5; direct++)
            {
                var classifyResult = await classificationService.ClassifyFilenameAsync($"Direct.Call.{iteration}.{direct}.S01E01.mkv");
                classifyResult.IsSuccess.Should().BeTrue();
            }
            
            // Memory and performance metrics
            Context.ChangeTracker.Clear();
            GC.Collect();
            var iterationMemory = GC.GetTotalMemory(false);
            var totalFilesProcessed = Context.TrackedFiles.Count(f => f.Status == FileStatus.Classified);
            
            sessionMetrics.Add((iteration, iterationMemory, stopwatch.Elapsed, totalFilesProcessed));
            
            // Brief pause to simulate real-world processing gaps
            await Task.Delay(100);
        }
        
        stopwatch.Stop();
        
        // Final memory measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var sessionEndMemory = GC.GetTotalMemory(true);

        // Then - Verify long-running session memory stability
        var totalSessionMemoryIncrease = sessionEndMemory - sessionStartMemory;
        var peakSessionMemory = sessionMetrics.Max(m => m.memory);
        var totalFilesInSession = sessionMetrics.Last().filesProcessed;
        
        // Overall session memory increase should be reasonable
        totalSessionMemoryIncrease.Should().BeLessThan(100 * 1024 * 1024, 
            "Long-running session should not accumulate excessive memory");
        
        // Peak memory should stay within ARM32 operational range
        peakSessionMemory.Should().BeLessThan(280 * 1024 * 1024, 
            "Peak session memory should stay under 280MB for ARM32 stability");
        
        // Memory efficiency per file processed
        var memoryPerFile = totalSessionMemoryIncrease / (double)totalFilesInSession;
        memoryPerFile.Should().BeLessThan(500 * 1024, 
            "Memory increase per processed file should be under 500KB");
        
        // Memory should not show continuous upward trend
        var memoryTrend = sessionMetrics
            .Take(sessionMetrics.Count - 1)
            .Zip(sessionMetrics.Skip(1), (first, second) => second.memory - first.memory)
            .ToList();
        
        var positiveTrendCount = memoryTrend.Count(trend => trend > 5 * 1024 * 1024); // >5MB increases
        var trendPercentage = positiveTrendCount / (double)memoryTrend.Count;
        
        trendPercentage.Should().BeLessThan(0.7, 
            "Memory should not consistently increase across iterations (trend <70%)");
    }
}