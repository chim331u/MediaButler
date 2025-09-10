using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.Repositories;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediaButler.Tests.Integration.Data;

/// <summary>
/// Integration tests for database operations involving ML results and training data.
/// Tests persistence, retrieval, and querying of ML-related entities with real database.
/// Follows "Simple Made Easy" principles with clear data scenarios and transaction boundaries.
/// </summary>
public class MLDataIntegrationTests : IntegrationTestBase
{
    public MLDataIntegrationTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SaveMLClassificationResults_ToDatabase_ShouldPersistCorrectly()
    {
        // Given - File classified by ML pipeline
        var testFile = new TrackedFile
        {
            Hash = "ml-data-test-1",
            OriginalPath = "/test/Demon.Slayer.S01E01.Unwavering.Resolve.1080p.mkv",
            FileName = "Demon.Slayer.S01E01.Unwavering.Resolve.1080p.mkv",
            FileSize = 1024 * 1024 * 600,
            Status = FileStatus.New
        };

        // When - Classify and save ML results
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var classifyResult = await classificationService.ClassifyFilenameAsync(testFile.FileName);
        classifyResult.IsSuccess.Should().BeTrue();
        var result = classifyResult.Value;
        
        // Update file with classification
        testFile.Category = result.PredictedCategory;
        testFile.Confidence = (decimal)result.Confidence;
        testFile.Status = FileStatus.Classified;
        testFile.MarkAsModified();
        
        Context.TrackedFiles.Add(testFile);
        await Context.SaveChangesAsync();

        // Then - Verify data persistence with fresh context
        Context.ChangeTracker.Clear();
        var savedFile = await Context.TrackedFiles.FindAsync("ml-data-test-1");
        
        savedFile.Should().NotBeNull();
        savedFile!.Category.Should().Be(result.PredictedCategory);
        savedFile.Confidence.Should().Be((decimal)result.Confidence);
        savedFile.Status.Should().Be(FileStatus.Classified);
        savedFile.LastUpdateDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task QueryFilesByMLConfidence_ShouldReturnCorrectResults()
    {
        // Given - Files with various confidence levels
        var testFiles = new[]
        {
            new TrackedFile
            {
                Hash = "high-confidence-1",
                OriginalPath = "/test/Popular.Series.S01E01.mkv",
                FileName = "Popular.Series.S01E01.mkv",
                Category = "POPULAR SERIES",
                Confidence = 0.95m,
                Status = FileStatus.Classified,
                FileSize = 1024 * 1024 * 300
            },
            new TrackedFile
            {
                Hash = "medium-confidence-1",
                OriginalPath = "/test/Obscure.Show.S01E01.mkv",
                FileName = "Obscure.Show.S01E01.mkv",
                Category = "OBSCURE SHOW",
                Confidence = 0.65m,
                Status = FileStatus.Classified,
                FileSize = 1024 * 1024 * 250
            },
            new TrackedFile
            {
                Hash = "low-confidence-1",
                OriginalPath = "/test/Unknown.Series.S01E01.mkv",
                FileName = "Unknown.Series.S01E01.mkv",
                Category = "UNKNOWN",
                Confidence = 0.25m,
                Status = FileStatus.Classified,
                FileSize = 1024 * 1024 * 200
            }
        };
        
        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        // When - Query by confidence thresholds
        using var scope = CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrackedFileRepository>();
        
        // Use existing repository methods that are available
        var allFiles = Context.TrackedFiles.Where(f => f.Hash.Contains("confidence")).ToList();
        var highConfidenceFiles = allFiles.Where(f => f.Confidence >= 0.8m).ToList();
        var mediumConfidenceFiles = allFiles.Where(f => f.Confidence >= 0.5m && f.Confidence < 0.8m).ToList();
        var lowConfidenceFiles = allFiles.Where(f => f.Confidence < 0.5m).ToList();

        // Then - Verify correct filtering
        highConfidenceFiles.Should().HaveCount(1);
        highConfidenceFiles.First().Hash.Should().Be("high-confidence-1");
        
        mediumConfidenceFiles.Should().HaveCount(1);
        mediumConfidenceFiles.First().Hash.Should().Be("medium-confidence-1");
        
        lowConfidenceFiles.Should().HaveCount(1);
        lowConfidenceFiles.First().Hash.Should().Be("low-confidence-1");
    }

    [Fact]
    public async Task ProcessingLogIntegration_WithMLResults_ShouldTrackOperations()
    {
        // Given - File processing through ML pipeline
        var testFile = new TrackedFile
        {
            Hash = "processing-log-test",
            OriginalPath = "/test/Attack.on.Titan.S04E28.The.Dawn.of.Humanity.1080p.mkv",
            FileName = "Attack.on.Titan.S04E28.The.Dawn.of.Humanity.1080p.mkv",
            FileSize = 1024 * 1024 * 700,
            Status = FileStatus.New
        };
        
        Context.TrackedFiles.Add(testFile);
        await Context.SaveChangesAsync();

        // When - Process file with ML classification and log operations
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var classifyResult = await classificationService.ClassifyFilenameAsync(testFile.FileName);
        classifyResult.IsSuccess.Should().BeTrue();
        var result = classifyResult.Value;
        
        // Create processing log entry
        var processLog = ProcessingLog.Info(
            testFile.Hash,
            "ML_Classification",
            $"Classified as '{result.PredictedCategory}' with confidence {result.Confidence:F2}",
            $"Processing completed successfully",
            150
        );
        
        Context.ProcessingLogs.Add(processLog);
        
        // Update file status
        testFile.Category = result.PredictedCategory;
        testFile.Confidence = (decimal)result.Confidence;
        testFile.Status = FileStatus.Classified;
        testFile.MarkAsModified();
        
        await Context.SaveChangesAsync();

        // Then - Verify processing log persistence and relationships
        Context.ChangeTracker.Clear();
        var logs = Context.ProcessingLogs
            .Where(l => l.FileHash == testFile.Hash)
            .ToList();
        
        logs.Should().HaveCount(1);
        var log = logs.First();
        log.Category.Should().Be("ML_Classification");
        log.Level.Should().Be(LogLevel.Information);
        log.Details.Should().Contain(result.PredictedCategory);
        log.DurationMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MLCategoryStatistics_DatabaseQueries_ShouldProvideInsights()
    {
        // Given - Files classified into various categories
        var testFiles = new[]
        {
            new TrackedFile { Hash = "anime-1", OriginalPath = "/test/Naruto.S01E01.mkv", FileName = "Naruto.S01E01.mkv", Category = "NARUTO", Confidence = 0.92m, Status = FileStatus.Classified, FileSize = 1024 * 1024 * 300 },
            new TrackedFile { Hash = "anime-2", OriginalPath = "/test/Naruto.S01E02.mkv", FileName = "Naruto.S01E02.mkv", Category = "NARUTO", Confidence = 0.89m, Status = FileStatus.Classified, FileSize = 1024 * 1024 * 310 },
            new TrackedFile { Hash = "anime-3", OriginalPath = "/test/One.Piece.E1001.mkv", FileName = "One.Piece.E1001.mkv", Category = "ONE PIECE", Confidence = 0.94m, Status = FileStatus.Classified, FileSize = 1024 * 1024 * 280 },
            new TrackedFile { Hash = "western-1", OriginalPath = "/test/Breaking.Bad.S05E16.mkv", FileName = "Breaking.Bad.S05E16.mkv", Category = "BREAKING BAD", Confidence = 0.87m, Status = FileStatus.Classified, FileSize = 1024 * 1024 * 450 },
            new TrackedFile { Hash = "unknown-1", OriginalPath = "/test/Random.File.mkv", FileName = "Random.File.mkv", Category = "UNKNOWN", Confidence = 0.15m, Status = FileStatus.Classified, FileSize = 1024 * 1024 * 100 }
        };
        
        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();

        // When - Query ML statistics
        using var scope = CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrackedFileRepository>();
        
        // Category distribution using LINQ
        var allTestFiles = Context.TrackedFiles.Where(f => f.Hash.StartsWith("anime-") || f.Hash.StartsWith("western-") || f.Hash.StartsWith("unknown-")).ToList();
        var categoryStats = allTestFiles.GroupBy(f => f.Category).ToDictionary(g => g.Key!, g => g.Count());
        
        // Confidence distribution
        var highConfidenceCount = allTestFiles.Count(f => f.Confidence >= 0.8m);
        var mediumConfidenceCount = allTestFiles.Count(f => f.Confidence >= 0.5m && f.Confidence < 0.8m);
        var lowConfidenceCount = allTestFiles.Count(f => f.Confidence < 0.5m);

        // Then - Verify statistical insights
        categoryStats.Should().NotBeEmpty();
        categoryStats.Should().ContainKey("NARUTO");
        categoryStats["NARUTO"].Should().Be(2);
        categoryStats.Should().ContainKey("ONE PIECE");
        categoryStats["ONE PIECE"].Should().Be(1);
        categoryStats.Should().ContainKey("BREAKING BAD");
        categoryStats["BREAKING BAD"].Should().Be(1);
        
        highConfidenceCount.Should().Be(4); // All except unknown
        mediumConfidenceCount.Should().Be(0);
        lowConfidenceCount.Should().Be(1); // Unknown file
    }

    [Fact]
    public async Task BulkMLResults_DatabaseOperations_ShouldPerformEfficiently()
    {
        // Given - Large batch of files requiring ML classification results
        var batchSize = 50;
        var testFiles = Enumerable.Range(1, batchSize)
            .Select(i => new TrackedFile
            {
                Hash = $"bulk-ml-{i:000}",
                OriginalPath = $"/test/Series.{i % 5}.S01E{i:00}.mkv",
                FileName = $"Series.{i % 5}.S01E{i:00}.mkv",
                FileSize = 1024 * 1024 * (200 + i * 2), // Varying sizes
                Status = FileStatus.New
            })
            .ToArray();

        // When - Bulk process with ML classification
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Context.TrackedFiles.AddRange(testFiles);
        await Context.SaveChangesAsync();
        
        // Classify and update in batches
        var batchesProcessed = 0;
        for (int i = 0; i < testFiles.Length; i += 10)
        {
            var batch = testFiles.Skip(i).Take(10);
            foreach (var file in batch)
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
            batchesProcessed++;
        }
        
        stopwatch.Stop();

        // Then - Verify efficient bulk operations
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(5), 
            "Bulk ML operations should complete in reasonable time");
        
        batchesProcessed.Should().Be(5); // 50 files / 10 per batch
        
        // Verify all files were processed
        var classifiedCount = Context.TrackedFiles
            .Count(f => f.Hash.StartsWith("bulk-ml-") && f.Status == FileStatus.Classified);
        classifiedCount.Should().Be(batchSize);
        
        // Verify data integrity
        var allProcessed = Context.TrackedFiles
            .Where(f => f.Hash.StartsWith("bulk-ml-"))
            .ToList();
        
        allProcessed.Should().OnlyContain(f => !string.IsNullOrEmpty(f.Category));
        allProcessed.Should().OnlyContain(f => f.Confidence >= 0);
        allProcessed.Should().OnlyContain(f => f.LastUpdateDate > DateTime.UtcNow.AddMinutes(-10));
    }

    [Fact]
    public async Task DatabaseTransactions_MLOperations_ShouldMaintainConsistency()
    {
        // Given - File requiring transactional ML processing
        var testFile = new TrackedFile
        {
            Hash = "transaction-test",
            OriginalPath = "/test/Complex.Transaction.S01E01.mkv",
            FileName = "Complex.Transaction.S01E01.mkv",
            FileSize = 1024 * 1024 * 400,
            Status = FileStatus.New
        };

        // When - Process with transaction boundaries
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        await using var transaction = await Context.Database.BeginTransactionAsync();
        
        try
        {
            // Add file
            Context.TrackedFiles.Add(testFile);
            await Context.SaveChangesAsync();
            
            // Classify
            var classifyResult = await classificationService.ClassifyFilenameAsync(testFile.FileName);
            classifyResult.IsSuccess.Should().BeTrue();
            var result = classifyResult.Value;
            
            // Update with results
            testFile.Category = result.PredictedCategory;
            testFile.Confidence = (decimal)result.Confidence;
            testFile.Status = FileStatus.Classified;
            testFile.MarkAsModified();
            
            // Add processing log
            Context.ProcessingLogs.Add(ProcessingLog.Info(
                testFile.Hash,
                "ML_Transaction_Test",
                $"Classified with confidence {result.Confidence:F2}",
                "Transaction test completed",
                100
            ));
            
            await Context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Then - Verify transactional consistency
        Context.ChangeTracker.Clear();
        
        var savedFile = await Context.TrackedFiles.FindAsync("transaction-test");
        savedFile.Should().NotBeNull();
        savedFile!.Status.Should().Be(FileStatus.Classified);
        
        var logEntry = Context.ProcessingLogs
            .FirstOrDefault(l => l.FileHash == "transaction-test");
        logEntry.Should().NotBeNull();
        logEntry!.Level.Should().Be(LogLevel.Information);
    }
}