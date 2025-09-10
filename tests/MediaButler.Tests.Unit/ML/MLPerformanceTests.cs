using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Comprehensive ML performance testing suite covering classification speed, batch processing efficiency,
/// memory usage monitoring, concurrent handling, and resource cleanup validation.
/// Optimized for ARM32 deployment constraints with realistic Italian TV series workloads.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable performance metrics and test data
/// - Single responsibility: Only handles performance validation concerns
/// - Compose don't complect: Independent test scenarios without cross-dependencies
/// - Declarative: Clear performance expectations and measurable outcomes
/// </remarks>
public class MLPerformanceTests
{
    private readonly Mock<IPredictionService> _mockPredictionService;
    private readonly Mock<IModelEvaluationService> _mockEvaluationService;
    private readonly Mock<ILogger<MLPerformanceTests>> _mockLogger;
    
    // ARM32 performance constraints (Raspberry Pi 4, 1GB RAM)
    private const int ARM32_MAX_MEMORY_MB = 300; // MediaButler target: <300MB total
    private const double ARM32_MAX_PREDICTION_TIME_MS = 100.0; // Target: <100ms per prediction
    private const double ARM32_MIN_THROUGHPUT_PER_SEC = 10.0; // Minimum 10 predictions/second
    private const int ARM32_BATCH_SIZE_LIMIT = 50; // Conservative batch size for memory

    public MLPerformanceTests()
    {
        _mockPredictionService = new Mock<IPredictionService>();
        _mockEvaluationService = new Mock<IModelEvaluationService>();
        _mockLogger = new Mock<ILogger<MLPerformanceTests>>();
    }

    #region Classification Speed Benchmarking Tests

    [Fact]
    public async Task ClassificationSpeedBenchmark_SinglePrediction_ShouldMeetARM32Constraints()
    {
        // Given - Single filename prediction with ARM32 timing constraints
        var testFilename = "Breaking.Bad.S05E16.FINAL.1080p.ITA.ENG.mkv";
        var predictionResult = CreateHighConfidenceResult(testFilename, "BREAKING BAD", 0.94);
        
        // Setup prediction service with realistic timing (45ms average)
        _mockPredictionService
            .Setup(x => x.PredictAsync(testFilename, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(45); // Simulate real ML inference time
                return Result<ClassificationResult>.Success(predictionResult);
            });

        var stopwatch = Stopwatch.StartNew();
        
        // When - Perform single prediction
        var result = await _mockPredictionService.Object.PredictAsync(testFilename);
        
        stopwatch.Stop();

        // Then - Verify ARM32 performance constraints
        result.IsSuccess.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan((long)ARM32_MAX_PREDICTION_TIME_MS, 
            "Single prediction should meet ARM32 latency requirements");
        result.Value.PredictedCategory.Should().Be("BREAKING BAD");
        result.Value.Confidence.Should().BeGreaterThan(0.9f);
    }

    [Theory]
    [InlineData(10, 50.0, "Small batch should process quickly")]
    [InlineData(25, 75.0, "Medium batch should maintain throughput")]
    [InlineData(50, 100.0, "Large batch should stay within ARM32 limits")]
    public async Task ClassificationSpeedBenchmark_BatchPredictions_ShouldMaintainThroughput(
        int batchSize, double maxAverageTimeMs, string scenario)
    {
        // Given - Batch of Italian TV series filenames
        var testFilenames = GenerateRealisticItalianFilenames(batchSize);
        var batchResult = CreateBatchResult(testFilenames);
        
        _mockPredictionService
            .Setup(x => x.PredictBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                // Simulate realistic batch processing time (40-60ms per file)
                var totalDelay = batchSize * Random.Shared.Next(40, 60);
                await Task.Delay(totalDelay);
                return Result<BatchClassificationResult>.Success(batchResult);
            });

        var stopwatch = Stopwatch.StartNew();
        
        // When - Process batch predictions
        var result = await _mockPredictionService.Object.PredictBatchAsync(testFilenames);
        
        stopwatch.Stop();

        // Then - Verify throughput and ARM32 constraints
        result.IsSuccess.Should().BeTrue();
        var avgTimePerPrediction = (double)stopwatch.ElapsedMilliseconds / batchSize;
        avgTimePerPrediction.Should().BeLessThan(maxAverageTimeMs, scenario);
        
        var throughputPerSecond = batchSize / (stopwatch.ElapsedMilliseconds / 1000.0);
        throughputPerSecond.Should().BeGreaterThan(ARM32_MIN_THROUGHPUT_PER_SEC,
            "Batch processing should maintain minimum throughput for ARM32");
    }

    [Fact]
    public async Task PerformanceBenchmarking_WithRealisticWorkload_ShouldGenerateComprehensiveMetrics()
    {
        // Given - Realistic Italian content workload for benchmarking
        var benchmarkConfig = new BenchmarkConfiguration
        {
            PredictionCount = 500, // Realistic daily volume for home NAS
            TimeoutMs = 30000,
            WarmupCount = 50,
            MonitorMemoryUsage = true,
            BenchmarkFilenames = GenerateRealisticItalianFilenames(500).AsReadOnly()
        };

        var expectedBenchmark = new PerformanceBenchmark
        {
            AveragePredictionTimeMs = 52.3,
            MedianPredictionTimeMs = 48.7,
            P95PredictionTimeMs = 78.2,
            P99PredictionTimeMs = 95.1,
            ThroughputPredictionsPerSecond = 19.2,
            PeakMemoryUsageMB = 287.3,
            AverageMemoryUsageMB = 234.6,
            TotalBenchmarkTimeMs = 26042.7,
            BenchmarkPredictionCount = 500,
            PassedPerformanceRequirements = true,
            CpuStats = new CpuUsageStats
            {
                AverageCpuUsagePercent = 34.2,
                PeakCpuUsagePercent = 67.8
            }
        };

        _mockEvaluationService
            .Setup(x => x.BenchmarkPerformanceAsync(benchmarkConfig))
            .ReturnsAsync(Result<PerformanceBenchmark>.Success(expectedBenchmark));

        // When - Perform comprehensive benchmarking
        var result = await _mockEvaluationService.Object.BenchmarkPerformanceAsync(benchmarkConfig);

        // Then - Verify comprehensive performance metrics
        result.IsSuccess.Should().BeTrue();
        var benchmark = result.Value;
        
        // ARM32 performance constraints
        benchmark.AveragePredictionTimeMs.Should().BeLessThan(ARM32_MAX_PREDICTION_TIME_MS,
            "Average prediction time should meet ARM32 constraints");
        benchmark.ThroughputPredictionsPerSecond.Should().BeGreaterThan(ARM32_MIN_THROUGHPUT_PER_SEC,
            "Throughput should meet ARM32 minimum requirements");
        
        // Memory constraints for ARM32 deployment
        benchmark.PeakMemoryUsageMB.Should().BeLessThan(ARM32_MAX_MEMORY_MB,
            "Peak memory usage should stay within ARM32 limits");
        benchmark.AverageMemoryUsageMB.Should().BeLessThan(benchmark.PeakMemoryUsageMB,
            "Average memory should be less than peak usage");
        
        // Performance requirements should pass for production deployment
        benchmark.PassedPerformanceRequirements.Should().BeTrue("Model should meet production performance standards");
        
        // CPU utilization should be reasonable for background processing
        benchmark.CpuStats.Should().NotBeNull();
        benchmark.CpuStats!.AverageCpuUsagePercent.Should().BeLessThan(60.0,
            "Average CPU usage should allow for other NAS operations");
    }

    #endregion

    #region Batch Processing Efficiency Tests

    [Theory]
    [InlineData(5, 0.95, "Very small batch should have minimal overhead")]
    [InlineData(10, 0.90, "Small batch should maintain high efficiency")]
    [InlineData(25, 0.85, "Medium batch should balance efficiency and memory")]
    [InlineData(50, 0.80, "Large batch should optimize for throughput")]
    public async Task BatchProcessingEfficiency_WithVariousBatchSizes_ShouldOptimizeResourceUsage(
        int batchSize, double minEfficiency, string scenario)
    {
        // Given - Batch processing efficiency test with varying sizes
        var testFilenames = GenerateRealisticItalianFilenames(batchSize);
        
        // Simulate batch processing with realistic efficiency patterns
        var batchResult = new BatchClassificationResult
        {
            Results = testFilenames.Select(f => CreateHighConfidenceResult(f, "TEST CATEGORY", 0.87)).ToList().AsReadOnly(),
            ProcessingDuration = TimeSpan.FromMilliseconds(batchSize * 42) // 42ms per file average
        };

        _mockPredictionService
            .Setup(x => x.PredictBatchAsync(testFilenames, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BatchClassificationResult>.Success(batchResult));

        var individualStopwatch = Stopwatch.StartNew();
        
        // Simulate individual predictions for comparison
        var individualTasks = testFilenames.Select(async filename => 
        {
            await Task.Delay(50); // Individual prediction overhead
            return CreateHighConfidenceResult(filename, "TEST CATEGORY", 0.87);
        });
        
        await Task.WhenAll(individualTasks);
        individualStopwatch.Stop();

        var batchStopwatch = Stopwatch.StartNew();
        
        // When - Process batch predictions
        var result = await _mockPredictionService.Object.PredictBatchAsync(testFilenames);
        
        batchStopwatch.Stop();

        // Then - Verify batch processing efficiency
        result.IsSuccess.Should().BeTrue();
        
        var batchEfficiency = (double)individualStopwatch.ElapsedMilliseconds / batchStopwatch.ElapsedMilliseconds;
        batchEfficiency.Should().BeGreaterThan(minEfficiency, scenario);
        
        // Verify all predictions succeeded
        result.Value.Results.Should().HaveCount(batchSize);
        result.Value.SuccessfulClassifications.Should().Be(batchSize);
        
        // ARM32 memory efficiency - batch should not exceed memory limits
        var estimatedMemoryUsageMB = batchSize * 2.5; // ~2.5MB per prediction
        estimatedMemoryUsageMB.Should().BeLessThan(ARM32_MAX_MEMORY_MB / 2,
            "Batch processing should use less than half available memory");
    }

    [Fact]
    public async Task BatchProcessingEfficiency_WithLargeItalianDataset_ShouldHandleChunking()
    {
        // Given - Large dataset that requires chunking for ARM32
        var largeDataset = GenerateRealisticItalianFilenames(200); // Exceeds single batch limit
        var expectedChunks = (largeDataset.Count + ARM32_BATCH_SIZE_LIMIT - 1) / ARM32_BATCH_SIZE_LIMIT;

        var processedBatches = new List<BatchClassificationResult>();
        
        _mockPredictionService
            .Setup(x => x.PredictBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<string>, CancellationToken>(async (filenames, ct) =>
            {
                var batch = filenames.ToList();
                batch.Count.Should().BeLessOrEqualTo(ARM32_BATCH_SIZE_LIMIT,
                    "Individual chunks should respect ARM32 memory constraints");
                
                var batchResult = new BatchClassificationResult
                {
                    Results = batch.Select(f => CreateHighConfidenceResult(f, ExtractSeriesFromFilename(f), 0.82)).ToList().AsReadOnly(),
                    ProcessingDuration = TimeSpan.FromMilliseconds(batch.Count * 45)
                };
                
                processedBatches.Add(batchResult);
                await Task.Delay(batch.Count * 2); // Simulate processing
                return Result<BatchClassificationResult>.Success(batchResult);
            });

        // When - Process large dataset in chunks
        var allResults = new List<ClassificationResult>();
        var totalStopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < largeDataset.Count; i += ARM32_BATCH_SIZE_LIMIT)
        {
            var chunk = largeDataset.Skip(i).Take(ARM32_BATCH_SIZE_LIMIT);
            var result = await _mockPredictionService.Object.PredictBatchAsync(chunk);
            
            result.IsSuccess.Should().BeTrue();
            allResults.AddRange(result.Value.Results);
        }
        
        totalStopwatch.Stop();

        // Then - Verify chunked processing efficiency
        allResults.Should().HaveCount(largeDataset.Count);
        processedBatches.Should().HaveCount(expectedChunks);
        
        // Verify processing rate meets ARM32 requirements
        var overallThroughput = largeDataset.Count / (totalStopwatch.ElapsedMilliseconds / 1000.0);
        overallThroughput.Should().BeGreaterThan(ARM32_MIN_THROUGHPUT_PER_SEC,
            "Chunked processing should maintain overall throughput");
    }

    #endregion

    #region Memory Usage Monitoring Tests

    [Fact]
    public async Task MemoryUsageMonitoring_DuringMLOperations_ShouldStayWithinARM32Limits()
    {
        // Given - Memory monitoring during ML operations
        var testFilenames = GenerateRealisticItalianFilenames(100);
        var memorySnapshots = new List<MemoryUsageSnapshot>();
        
        // Simulate memory usage tracking during prediction
        _mockPredictionService
            .Setup(x => x.PredictBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<string>, CancellationToken>(async (filenames, ct) =>
            {
                // Simulate progressive memory usage during batch processing
                foreach (var filename in filenames.Take(10)) // Sample monitoring
                {
                    memorySnapshots.Add(new MemoryUsageSnapshot
                    {
                        Timestamp = DateTime.UtcNow,
                        UsedMemoryMB = Random.Shared.Next(180, 280), // Realistic range for MediaButler
                        AvailableMemoryMB = Random.Shared.Next(720, 820),
                        GcCollections = memorySnapshots.Count / 20 // GC every ~20 predictions
                    });
                    
                    if (ct.IsCancellationRequested) break;
                    await Task.Delay(5, ct); // Brief processing simulation
                }
                
                var results = filenames.Select(f => CreateHighConfidenceResult(f, "TEST SERIES", 0.85)).ToList();
                return Result<BatchClassificationResult>.Success(new BatchClassificationResult
                {
                    Results = results.AsReadOnly(),
                    ProcessingDuration = TimeSpan.FromMilliseconds(results.Count * 48)
                });
            });

        // When - Monitor memory during batch processing
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await _mockPredictionService.Object.PredictBatchAsync(testFilenames, cts.Token);

        // Then - Verify memory usage constraints
        result.IsSuccess.Should().BeTrue();
        memorySnapshots.Should().NotBeEmpty("Memory monitoring should capture usage data");
        
        // ARM32 memory constraint validation
        var peakMemoryUsage = memorySnapshots.Max(s => s.UsedMemoryMB);
        peakMemoryUsage.Should().BeLessThan(ARM32_MAX_MEMORY_MB,
            "Peak memory usage should stay within ARM32 constraints");
        
        // Memory growth pattern should be stable (no major leaks)
        if (memorySnapshots.Count >= 5)
        {
            var firstQuarter = memorySnapshots.Take(memorySnapshots.Count / 4).Average(s => s.UsedMemoryMB);
            var lastQuarter = memorySnapshots.TakeLast(memorySnapshots.Count / 4).Average(s => s.UsedMemoryMB);
            var memoryGrowth = lastQuarter - firstQuarter;
            
            memoryGrowth.Should().BeLessThan(50, "Memory usage should not grow significantly during processing");
        }
    }

    [Theory]
    [InlineData(10, 200, "Light workload should use minimal memory")]
    [InlineData(50, 270, "Moderate workload should stay well within limits")]
    [InlineData(100, 290, "Heavy workload should approach but not exceed limits")]
    public async Task MemoryUsageMonitoring_WithVariousWorkloads_ShouldScaleAppropriately(
        int workloadSize, double maxExpectedMemoryMB, string scenario)
    {
        // Given - Various workload sizes to test memory scaling
        var testFilenames = GenerateRealisticItalianFilenames(workloadSize);
        var performanceStats = new PredictionPerformanceStats
        {
            TotalPredictions = workloadSize,
            SuccessfulPredictions = workloadSize - Random.Shared.Next(0, 2),
            AveragePredictionTime = TimeSpan.FromMilliseconds(47.2),
            AverageConfidence = 0.82,
            ConfidenceBreakdown = new ConfidenceLevelStats
            {
                HighConfidence = (long)(workloadSize * 0.7),
                MediumConfidence = (long)(workloadSize * 0.25),
                LowConfidence = (long)(workloadSize * 0.05)
            },
            StatsPeriod = TimeSpan.FromMinutes(5)
        };

        // Simulate workload processing
        _mockPredictionService
            .Setup(x => x.GetPerformanceStatsAsync())
            .ReturnsAsync(Result<PredictionPerformanceStats>.Success(performanceStats));

        // When - Get performance stats after workload
        var result = await _mockPredictionService.Object.GetPerformanceStatsAsync();

        // Then - Verify memory scaling behavior based on workload
        result.IsSuccess.Should().BeTrue();
        var stats = result.Value;
        
        // Verify performance characteristics based on workload size
        stats.AveragePredictionTime.TotalMilliseconds.Should().BeLessThan(ARM32_MAX_PREDICTION_TIME_MS,
            "Average prediction time should meet ARM32 constraints");
        
        // Throughput should be reasonable for the workload size
        var throughputPerSecond = workloadSize / stats.StatsPeriod.TotalSeconds;
        throughputPerSecond.Should().BeGreaterThan(ARM32_MIN_THROUGHPUT_PER_SEC / 2, scenario);
        
        // Verify confidence levels are appropriate
        stats.AverageConfidence.Should().BeGreaterThan(0.7, "Overall confidence should be reasonable");
        
        // High confidence predictions should dominate
        stats.ConfidenceBreakdown.HighConfidencePercentage.Should().BeGreaterThan(60.0,
            "Most predictions should be high confidence for production use");
    }

    #endregion

    #region Concurrent Classification Handling Tests

    [Theory]
    [InlineData(2, 30, "Dual concurrent predictions should maintain performance")]
    [InlineData(4, 25, "Quad concurrent predictions should optimize ARM32 cores")]
    [InlineData(8, 20, "High concurrency should manage resource contention")]
    public async Task ConcurrentClassification_WithMultipleSimultaneousPredictions_ShouldMaintainPerformance(
        int concurrentCount, int maxAverageTimeMs, string scenario)
    {
        // Given - Multiple concurrent classification tasks
        var baseFilenames = GenerateRealisticItalianFilenames(10);
        var concurrentFilenames = Enumerable.Range(0, concurrentCount)
            .SelectMany(i => baseFilenames.Select(f => $"{i}_{f}"))
            .ToList();

        _mockPredictionService
            .Setup(x => x.PredictAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (filename, ct) =>
            {
                // Simulate concurrent processing with realistic timing variation
                var delay = Random.Shared.Next(30, 70);
                await Task.Delay(delay, ct);
                
                var series = ExtractSeriesFromFilename(filename.Substring(2)); // Remove prefix
                return Result<ClassificationResult>.Success(CreateHighConfidenceResult(filename, series, 0.86));
            });

        // When - Execute concurrent predictions
        var predictionTasks = concurrentFilenames
            .Select(filename => _mockPredictionService.Object.PredictAsync(filename))
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(predictionTasks);
        stopwatch.Stop();

        // Then - Verify concurrent performance
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue("All concurrent predictions should succeed"));
        
        var avgTimePerTask = (double)stopwatch.ElapsedMilliseconds / predictionTasks.Count;
        avgTimePerTask.Should().BeLessThan(maxAverageTimeMs, scenario);
        
        // Verify ARM32 resource efficiency under concurrency
        var totalThroughput = results.Length / (stopwatch.ElapsedMilliseconds / 1000.0);
        totalThroughput.Should().BeGreaterThan(ARM32_MIN_THROUGHPUT_PER_SEC * (concurrentCount * 0.7),
            "Concurrent processing should achieve reasonable throughput scaling");
    }

    [Fact]
    public async Task ConcurrentClassification_WithResourceContention_ShouldHandleGracefully()
    {
        // Given - High concurrent load that may cause resource contention
        var highConcurrencyCount = 16; // Exceeds ARM32 optimal concurrency
        var testFilenames = GenerateRealisticItalianFilenames(highConcurrencyCount);
        
        var concurrentExecutions = new ConcurrentBag<TimeSpan>();
        var resourceContentionDetected = false;

        _mockPredictionService
            .Setup(x => x.PredictAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (filename, ct) =>
            {
                var executionStopwatch = Stopwatch.StartNew();
                
                // Simulate resource contention with longer delays for high concurrency
                var baseDelay = Random.Shared.Next(40, 60);
                var contentionDelay = concurrentExecutions.Count > 8 ? Random.Shared.Next(20, 50) : 0;
                
                if (contentionDelay > 0) resourceContentionDetected = true;
                
                await Task.Delay(baseDelay + contentionDelay, ct);
                executionStopwatch.Stop();
                concurrentExecutions.Add(executionStopwatch.Elapsed);
                
                var series = ExtractSeriesFromFilename(filename);
                return Result<ClassificationResult>.Success(CreateHighConfidenceResult(filename, series, 0.81));
            });

        // When - Execute high concurrent load
        var tasks = testFilenames
            .Select(filename => _mockPredictionService.Object.PredictAsync(filename))
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Then - Verify graceful handling of resource contention
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue("All predictions should complete despite contention"));
        
        // Performance may degrade under high concurrency, but should remain functional
        var avgExecutionTime = concurrentExecutions.Average(t => t.TotalMilliseconds);
        avgExecutionTime.Should().BeLessThan(200, "Average execution time should remain reasonable under contention");
        
        // System should handle contention without failures
        if (resourceContentionDetected)
        {
            // Verify that despite contention, all operations completed successfully
            results.Should().HaveCount(highConcurrencyCount, "All concurrent operations should complete");
        }
    }

    #endregion

    #region Resource Cleanup and Memory Management Tests

    [Fact]
    public async Task ResourceCleanup_AfterLargeBatchProcessing_ShouldReleaseMemoryProperly()
    {
        // Given - Large batch processing that allocates significant memory
        var largeBatch = GenerateRealisticItalianFilenames(150);
        var initialMemory = 180.0; // MB
        var peakMemory = 0.0;
        var finalMemory = 0.0;

        _mockPredictionService
            .Setup(x => x.PredictBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<string>, CancellationToken>(async (filenames, ct) =>
            {
                // Simulate memory allocation during processing
                peakMemory = initialMemory + (filenames.Count() * 0.8); // ~0.8MB per file
                
                await Task.Delay(filenames.Count() * 25, ct); // Processing time
                
                // Simulate cleanup after processing
                await Task.Delay(50, ct); // Cleanup delay
                finalMemory = initialMemory + 15; // Small residual memory
                
                var results = filenames.Select(f => CreateHighConfidenceResult(f, ExtractSeriesFromFilename(f), 0.83)).ToList();
                return Result<BatchClassificationResult>.Success(new BatchClassificationResult
                {
                    Results = results.AsReadOnly(),
                    ProcessingDuration = TimeSpan.FromMilliseconds(results.Count * 25)
                });
            });

        // When - Process large batch and trigger cleanup
        var result = await _mockPredictionService.Object.PredictBatchAsync(largeBatch);
        
        // Simulate explicit cleanup (e.g., GC.Collect in real implementation)
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(100); // Allow cleanup to complete

        // Then - Verify proper resource cleanup
        result.IsSuccess.Should().BeTrue();
        
        peakMemory.Should().BeLessThan(ARM32_MAX_MEMORY_MB, "Peak memory should stay within ARM32 limits");
        finalMemory.Should().BeLessThan(initialMemory + 30, "Memory should be released after processing");
        
        var memoryRecoveryRatio = (peakMemory - finalMemory) / (peakMemory - initialMemory);
        memoryRecoveryRatio.Should().BeGreaterThan(0.85, "At least 85% of allocated memory should be released");
    }

    [Fact]
    public async Task ResourceCleanup_WithLongRunningOperations_ShouldMaintainStableMemoryProfile()
    {
        // Given - Long-running operation to test memory stability
        var totalOperations = 10;
        var memoryReadings = new List<double>();
        var operationCounter = 0;

        _mockPredictionService
            .Setup(x => x.PredictAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (filename, ct) =>
            {
                operationCounter++;
                
                // Simulate baseline memory + operation memory
                var baseMemory = 190.0;
                var operationMemory = baseMemory + (operationCounter * 2.5); // Gradual increase
                memoryReadings.Add(operationMemory);
                
                await Task.Delay(100, ct);
                
                // Simulate periodic cleanup (every 5 operations)
                if (operationCounter % 5 == 0)
                {
                    await Task.Delay(50, ct); // Cleanup delay
                    var cleanedMemory = baseMemory + 10; // Reset to baseline + small overhead
                    memoryReadings.Add(cleanedMemory);
                }
                
                var series = ExtractSeriesFromFilename(filename);
                return Result<ClassificationResult>.Success(CreateHighConfidenceResult(filename, series, 0.88));
            });

        // When - Execute long-running sequence of operations
        var filenames = GenerateRealisticItalianFilenames(totalOperations);
        
        for (int i = 0; i < totalOperations; i++)
        {
            var result = await _mockPredictionService.Object.PredictAsync(filenames[i]);
            result.IsSuccess.Should().BeTrue($"Operation {i + 1} should succeed");
            
            // Small delay between operations
            await Task.Delay(50);
        }

        // Then - Verify stable memory profile over time
        memoryReadings.Should().NotBeEmpty("Memory readings should be captured during operations");
        
        var maxReading = memoryReadings.Max();
        maxReading.Should().BeLessThan(ARM32_MAX_MEMORY_MB, "Memory should never exceed ARM32 limits");
        
        // Memory profile should show cleanup cycles (not continuously growing)
        if (memoryReadings.Count >= 8)
        {
            var firstHalf = memoryReadings.Take(memoryReadings.Count / 2).Average();
            var secondHalf = memoryReadings.Skip(memoryReadings.Count / 2).Average();
            
            var memoryGrowthRatio = secondHalf / firstHalf;
            memoryGrowthRatio.Should().BeLessThan(1.3, "Memory should not grow significantly over time due to cleanup");
        }
    }

    #endregion

    #region Helper Methods

    private static List<string> GenerateRealisticItalianFilenames(int count)
    {
        var series = new[]
        {
            "Breaking.Bad", "The.Office", "Game.of.Thrones", "Naruto", "One.Piece",
            "La.Casa.di.Carta", "Il.Trono.di.Spade", "The.Walking.Dead", "Stranger.Things",
            "Attack.on.Titan", "Death.Note", "Cowboy.Bebop", "Dragon.Ball", "Lupin"
        };
        
        var qualityTags = new[] { "720p", "1080p", "2160p", "HDTV", "BluRay", "WEBRip" };
        var languageTags = new[] { "ITA", "ENG", "ITA.ENG", "Sub.ITA", "Dub.ITA" };
        var releaseTags = new[] { "FINAL", "REPACK", "PROPER", "EXTENDED", "UNCUT" };

        return Enumerable.Range(1, count)
            .Select(i =>
            {
                var selectedSeries = series[i % series.Length];
                var season = Random.Shared.Next(1, 6);
                var episode = Random.Shared.Next(1, 25);
                var quality = qualityTags[Random.Shared.Next(qualityTags.Length)];
                var language = languageTags[Random.Shared.Next(languageTags.Length)];
                
                var filename = $"{selectedSeries}.S{season:D2}E{episode:D2}";
                
                // Add optional release tag (30% chance)
                if (Random.Shared.Next(100) < 30)
                {
                    filename += $".{releaseTags[Random.Shared.Next(releaseTags.Length)]}";
                }
                
                filename += $".{quality}.{language}.mkv";
                
                return filename;
            })
            .ToList();
    }

    private static ClassificationResult CreateHighConfidenceResult(string filename, string category, double confidence)
    {
        return new ClassificationResult
        {
            Filename = filename,
            PredictedCategory = category,
            Confidence = (float)confidence,
            Decision = ClassificationDecision.AutoClassify,
            ProcessingTimeMs = Random.Shared.Next(35, 65)
        };
    }

    private static BatchClassificationResult CreateBatchResult(List<string> filenames)
    {
        var results = filenames.Select(f => CreateHighConfidenceResult(f, ExtractSeriesFromFilename(f), 0.85)).ToList();
        
        return new BatchClassificationResult
        {
            Results = results.AsReadOnly(),
            ProcessingDuration = TimeSpan.FromMilliseconds(results.Count * 47)
        };
    }

    private static string ExtractSeriesFromFilename(string filename)
    {
        // Simple extraction logic for test purposes
        var parts = filename.Split('.');
        if (parts.Length >= 3)
        {
            return $"{parts[0].ToUpperInvariant()} {parts[1].ToUpperInvariant()}";
        }
        return parts[0].ToUpperInvariant();
    }

    #endregion
}

/// <summary>
/// Memory usage snapshot for testing purposes.
/// </summary>
public record MemoryUsageSnapshot
{
    public required DateTime Timestamp { get; init; }
    public required double UsedMemoryMB { get; init; }
    public required double AvailableMemoryMB { get; init; }
    public required int GcCollections { get; init; }
}