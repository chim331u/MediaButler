using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Services.Interfaces;
using MediaButler.ML.Interfaces;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace MediaButler.Tests.Integration.Performance;

/// <summary>
/// ARM32 deployment validation tests for Task 3.4.3: Performance & ARM32 Validation.
/// Focuses on resource constraint compliance and performance under ARM32 conditions.
/// Follows "Simple Made Easy" principles with clear resource measurement boundaries.
/// </summary>
public class ARM32ValidationTests : IntegrationTestBase
{
    public ARM32ValidationTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task FileService_LargeFileOperations_ShouldStayWithinMemoryConstraints()
    {
        // Given - ARM32 memory constraint validation (target: <300MB)
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baselineMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        
        // Create multiple test files for batch processing
        var testFiles = new List<string>();
        try
        {
            for (int i = 0; i < 50; i++)
            {
                var filename = $"Large.File.Test.Series.S{i % 5 + 1:00}E{i:00}.1080p.mkv";
                var testFile = CreateTestFile(filename, GenerateLargeContent(1024 * 20)); // 20KB each
                testFiles.Add(testFile);
            }
            
            // When - Process all files and monitor memory usage
            var memoryReadings = new List<(int filesProcessed, long memoryUsage)>();
            
            for (int i = 0; i < testFiles.Count; i++)
            {
                var result = await fileService.RegisterFileAsync(testFiles[i]);
                result.IsSuccess.Should().BeTrue($"File registration should succeed for file {i}");
                
                // Memory checkpoint every 10 files
                if (i % 10 == 0)
                {
                    GC.Collect();
                    var currentMemory = GC.GetTotalMemory(false);
                    memoryReadings.Add((i + 1, currentMemory));
                }
            }
            
            // Final memory measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);
            
            // Then - Verify ARM32 memory constraints are met
            var totalMemoryIncrease = finalMemory - baselineMemory;
            var peakMemoryUsage = memoryReadings.Max(r => r.memoryUsage);
            
            // ARM32 constraint: Total process memory should stay under 300MB
            peakMemoryUsage.Should().BeLessThan(300 * 1024 * 1024, 
                "Peak memory usage must stay under 300MB for ARM32 deployment");
                
            // Memory increase should be reasonable for the workload
            totalMemoryIncrease.Should().BeLessThan(50 * 1024 * 1024, 
                "Memory increase should be under 50MB for 50 file operations");
            
            // Memory should not grow linearly with file count
            if (memoryReadings.Count >= 3)
            {
                var growthRates = memoryReadings
                    .Zip(memoryReadings.Skip(1), (first, second) => second.memoryUsage - first.memoryUsage)
                    .ToList();
                    
                var averageGrowth = growthRates.Average();
                averageGrowth.Should().BeLessThan(10 * 1024 * 1024, 
                    "Average memory growth should be under 10MB per checkpoint");
            }
        }
        finally
        {
            // Cleanup test files
            foreach (var testFile in testFiles)
            {
                CleanupTestFile(testFile);
            }
        }
    }
    
    [Fact]
    public async Task FileService_ConcurrentOperations_ShouldMaintainPerformance()
    {
        // Given - Performance validation under concurrency
        var stopwatch = Stopwatch.StartNew();
        var testFiles = new List<string>();
        
        // Create test files
        for (int i = 0; i < 20; i++)
        {
            var filename = $"Concurrent.Performance.Test.S01E{i:00}.mkv";
            var testFile = CreateTestFile(filename);
            testFiles.Add(testFile);
        }
        
        try
        {
            // When - Process files concurrently
            var concurrentTasks = testFiles.Select(async testFile =>
            {
                using var scope = CreateScope();
                var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
                return await fileService.RegisterFileAsync(testFile);
            });
            
            var results = await Task.WhenAll(concurrentTasks);
            stopwatch.Stop();
            
            // Then - Verify all operations succeeded and performance is acceptable
            results.Should().AllSatisfy(result => result.IsSuccess.Should().BeTrue());
            
            // ARM32 performance constraint: Operations should complete within reasonable time
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, 
                "20 concurrent file registrations should complete within 10 seconds on ARM32");
            
            var avgTimePerFile = (double)stopwatch.ElapsedMilliseconds / testFiles.Count;
            avgTimePerFile.Should().BeLessThan(500, 
                "Average time per file should be under 500ms for ARM32 efficiency");
            
            // Verify database consistency
            var dbFiles = await Context.TrackedFiles.ToListAsync();
            dbFiles.Should().HaveCount(testFiles.Count);
            
            // All files should have unique hashes (no race conditions)
            var uniqueHashes = dbFiles.Select(f => f.Hash).Distinct().Count();
            uniqueHashes.Should().Be(testFiles.Count, "All files should have unique hashes");
        }
        finally
        {
            foreach (var testFile in testFiles)
            {
                CleanupTestFile(testFile);
            }
        }
    }
    
    [Fact]
    public async Task DatabaseOperations_HighVolume_ShouldPerformEfficientlyOnARM32()
    {
        // Given - Database performance validation
        var stopwatch = Stopwatch.StartNew();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        
        // When - Perform high-volume database operations
        var operationTimes = new List<long>();
        
        for (int batch = 0; batch < 5; batch++)
        {
            var batchStopwatch = Stopwatch.StartNew();
            
            // Create batch of files
            var batchFiles = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var filename = $"HighVolume.Batch{batch}.File{i}.S01E01.mkv";
                var testFile = CreateTestFile(filename);
                batchFiles.Add(testFile);
                
                var result = await fileService.RegisterFileAsync(testFile);
                result.IsSuccess.Should().BeTrue();
            }
            
            batchStopwatch.Stop();
            operationTimes.Add(batchStopwatch.ElapsedMilliseconds);
            
            // Cleanup batch files
            foreach (var file in batchFiles)
            {
                CleanupTestFile(file);
            }
        }
        
        stopwatch.Stop();
        
        // Final memory check
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        
        // Then - Verify ARM32 performance characteristics
        var totalMemoryIncrease = finalMemory - initialMemory;
        var averageBatchTime = operationTimes.Average();
        
        // Performance constraints for ARM32
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000, 
            "High-volume operations should complete within 15 seconds on ARM32");
            
        averageBatchTime.Should().BeLessThan(3000, 
            "Average batch time should be under 3 seconds for 10 files");
        
        // Memory should not accumulate significantly
        totalMemoryIncrease.Should().BeLessThan(20 * 1024 * 1024, 
            "Memory increase should be under 20MB for high-volume operations");
        
        // Consistent performance across batches
        var maxBatchTime = operationTimes.Max();
        var minBatchTime = operationTimes.Min();
        var timeVariance = maxBatchTime - minBatchTime;
        
        timeVariance.Should().BeLessThan((long)(averageBatchTime * 2), 
            "Batch processing times should be relatively consistent");
    }
    
    [Fact]
    public void MLServices_ResourceUsage_ShouldBeOptimalForARM32()
    {
        // Given - ML service resource usage validation
        GC.Collect();
        var baselineMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        
        // When - Initialize ML services (lightweight on ARM32)
        var tokenizerService = scope.ServiceProvider.GetService<ITokenizerService>();
        var classificationService = scope.ServiceProvider.GetService<IClassificationService>();
        
        // ML services might not be available in all test configurations
        if (tokenizerService != null)
        {
            // Test tokenization performance
            var testFilenames = new[]
            {
                "The.Walking.Dead.S11E24.FINAL.1080p.mkv",
                "Breaking.Bad.S05E16.Felina.720p.BluRay.mkv",
                "Game.of.Thrones.S08E06.The.Iron.Throne.2160p.mkv"
            };
            
            var stopwatch = Stopwatch.StartNew();
            foreach (var filename in testFilenames)
            {
                var result = tokenizerService.TokenizeFilename(filename);
                result.IsSuccess.Should().BeTrue();
            }
            stopwatch.Stop();
            
            // ARM32 performance: tokenization should be fast
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
                "Tokenization should be under 100ms for 3 files on ARM32");
        }
        
        GC.Collect();
        var afterInitMemory = GC.GetTotalMemory(false);
        
        // Then - Verify ML services don't consume excessive resources
        var mlMemoryFootprint = afterInitMemory - baselineMemory;
        mlMemoryFootprint.Should().BeLessThan(50 * 1024 * 1024, 
            "ML services should use less than 50MB on ARM32");
    }
    
    [Fact]
    public async Task FileOperations_IOPerformance_ShouldMeetARM32Requirements()
    {
        // Given - I/O performance validation for ARM32 constraints
        var testFiles = new List<string>();
        var fileSizes = new[] { 1024, 1024 * 10, 1024 * 50 }; // 1KB, 10KB, 50KB
        
        foreach (var size in fileSizes)
        {
            for (int i = 0; i < 3; i++)
            {
                var filename = $"IOPerf.Size{size}.File{i}.mkv";
                var testFile = CreateTestFile(filename, GenerateLargeContent(size));
                testFiles.Add(testFile);
            }
        }
        
        using var scope = CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        
        try
        {
            // When - Process files of different sizes
            var performanceMetrics = new List<(long fileSize, long processingTime)>();
            
            foreach (var testFile in testFiles)
            {
                var fileInfo = new FileInfo(testFile);
                var stopwatch = Stopwatch.StartNew();
                
                var result = await fileService.RegisterFileAsync(testFile);
                
                stopwatch.Stop();
                result.IsSuccess.Should().BeTrue();
                
                performanceMetrics.Add((fileInfo.Length, stopwatch.ElapsedMilliseconds));
            }
            
            // Then - Verify I/O performance is acceptable for ARM32
            var averageTimePerKB = performanceMetrics.Average(m => m.processingTime / (double)(m.fileSize / 1024.0));
            
            averageTimePerKB.Should().BeLessThan(10, 
                "File processing should be under 10ms per KB on ARM32");
            
            // Larger files shouldn't be disproportionately slower
            var smallFiles = performanceMetrics.Where(m => m.fileSize <= 2048).ToList();
            var largeFiles = performanceMetrics.Where(m => m.fileSize > 10240).ToList();
            
            if (smallFiles.Any() && largeFiles.Any())
            {
                var smallFileAvg = smallFiles.Average(m => m.processingTime / (double)(m.fileSize / 1024.0));
                var largeFileAvg = largeFiles.Average(m => m.processingTime / (double)(m.fileSize / 1024.0));
                
                // Performance should scale reasonably with file size
                (largeFileAvg / smallFileAvg).Should().BeLessThan(3.0, 
                    "Large file processing shouldn't be more than 3x slower per KB");
            }
        }
        finally
        {
            foreach (var testFile in testFiles)
            {
                CleanupTestFile(testFile);
            }
        }
    }

    #region Helper Methods

    private string CreateTestFile(string filename, string? content = null)
    {
        var tempDir = Path.GetTempPath();
        var filePath = Path.Combine(tempDir, $"arm32_test_{Guid.NewGuid():N}_{filename}");
        
        content ??= $"Test content for {filename}\nCreated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\nARM32 Test Data";
        File.WriteAllText(filePath, content);
        
        return filePath;
    }
    
    private string GenerateLargeContent(int sizeBytes)
    {
        var random = new Random(42); // Deterministic for tests
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789\n";
        var result = new char[sizeBytes];
        
        for (int i = 0; i < sizeBytes; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(result);
    }
    
    private void CleanupTestFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    #endregion
}