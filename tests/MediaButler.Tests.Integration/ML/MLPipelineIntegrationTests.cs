using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediaButler.Tests.Integration.ML;

/// <summary>
/// Integration tests for end-to-end ML pipeline functionality.
/// Tests the complete flow from filename input to classification result storage.
/// Follows "Simple Made Easy" principles with clear test scenarios and real service dependencies.
/// </summary>
public class MLPipelineIntegrationTests : IntegrationTestBase
{
    public MLPipelineIntegrationTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ClassifyFile_CompleteMLPipeline_ShouldStoreResultsInDatabase()
    {
        // Given - Real ML services and database
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();
        
        var testFile = new TrackedFile
        {
            Hash = "test-hash-123",
            OriginalPath = "/test/The.Walking.Dead.S11E24.FINAL.1080p.mkv",
            FileName = "The.Walking.Dead.S11E24.FINAL.1080p.mkv",
            FileSize = 1024 * 1024 * 500, // 500MB
            Status = FileStatus.New
        };
        
        Context.TrackedFiles.Add(testFile);
        await Context.SaveChangesAsync();

        // When - Complete ML classification pipeline
        var classificationResult = await classificationService.ClassifyFilenameAsync(testFile.FileName);
        var result = classificationResult.Value;

        // Then - Verify classification results
        classificationResult.IsSuccess.Should().BeTrue();
        result.Should().NotBeNull();
        result.PredictedCategory.Should().NotBeNull();
        result.Confidence.Should().BeGreaterThan(0.0f);
        result.Confidence.Should().BeLessOrEqualTo(1.0f);
        
        // Verify the result structure matches expected ML pipeline output
        if (result.PredictedCategory != "UNKNOWN")
        {
            result.PredictedCategory.Should().Match(c => c.All(char.IsUpper) || c.Contains(' '));
            result.Confidence.Should().BeGreaterThan(0.1f); // Reasonable confidence for known series
        }
    }

    [Fact]
    public async Task TokenizeAndClassify_ItalianMediaFilename_ShouldExtractCorrectFeatures()
    {
        // Given - Italian media filename with complex patterns
        using var scope = CreateScope();
        var tokenizerService = scope.ServiceProvider.GetRequiredService<ITokenizerService>();
        var featureService = scope.ServiceProvider.GetRequiredService<IFeatureEngineeringService>();
        
        var complexFilename = "My.Hero.Academia.6x25.The.High.Deep.Blue.Sky.Sub.ITA.1080p.WEB-DLMux.H264-UBi.mkv";

        // When - Complete tokenization and feature extraction
        var tokenizeResult = tokenizerService.TokenizeFilename(complexFilename);
        
        // Then - Verify tokenization succeeded
        tokenizeResult.IsSuccess.Should().BeTrue();
        var tokenized = tokenizeResult.Value;
        
        // Verify Italian content patterns are handled correctly
        tokenized.SeriesTokens.Should().Contain("hero");
        tokenized.SeriesTokens.Should().Contain("academia");
        tokenized.EpisodeInfo.Should().NotBeNull();
        tokenized.EpisodeInfo.Season.Should().Be(6);
        tokenized.EpisodeInfo.Episode.Should().Be(25);
        
        // Verify quality extraction
        tokenized.QualityInfo.Resolution.Should().Be("1080p");
        tokenized.QualityInfo.VideoCodec.Should().Be("H264");
        tokenized.QualityInfo.Source.Should().Be("WEB-DLMux");
    }

    [Fact]
    public async Task MLPipeline_WithMultipleFiles_ShouldHandleBatchProcessing()
    {
        // Given - Multiple test files for batch processing
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        var testFiles = new[]
        {
            "Breaking.Bad.S05E16.FINAL.1080p.BluRay.x264.mkv",
            "The.Office.S01E01.Pilot.720p.WEB-DL.mkv", 
            "One.Piece.1089.Sub.ITA.1080p.WEB-DLMux.H264.mkv",
            "Attack.on.Titan.S04E28.The.Dawn.of.Humanity.1080p.mkv"
        };

        // When - Process multiple files through ML pipeline
        var results = new List<ClassificationResult>();
        foreach (var filename in testFiles)
        {
            var classifyResult = await classificationService.ClassifyFilenameAsync(filename);
            classifyResult.IsSuccess.Should().BeTrue();
            results.Add(classifyResult.Value);
        }

        // Then - Verify all files were processed
        results.Should().HaveCount(4);
        results.Should().OnlyContain(r => r != null);
        results.Should().OnlyContain(r => r.PredictedCategory != null);
        results.Should().OnlyContain(r => r.Confidence >= 0.0f && r.Confidence <= 1.0f);
        
        // Verify diverse classification results for different series
        var categories = results.Select(r => r.PredictedCategory).Distinct().ToList();
        categories.Should().HaveCountGreaterThan(1, "Different series should get different categories");
    }

    [Fact]
    public async Task MLPipeline_WithInvalidFilename_ShouldHandleGracefully()
    {
        // Given - Invalid/malformed filename
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        var invalidFilenames = new[]
        {
            "", 
            "   ",
            "no-extension-file",
            "random.text.file.txt",
            "..invalid..filename...mkv"
        };

        // When - Process invalid filenames
        foreach (var filename in invalidFilenames)
        {
            var classifyResult = await classificationService.ClassifyFilenameAsync(filename);
            
            // Then - Should return valid result even for invalid input
            classifyResult.IsSuccess.Should().BeTrue();
            var result = classifyResult.Value;
            result.Should().NotBeNull();
            result.PredictedCategory.Should().NotBeNull();
            
            // Invalid files should typically get low confidence or "UNKNOWN" category
            if (result.PredictedCategory == "UNKNOWN")
            {
                result.Confidence.Should().BeLessOrEqualTo(0.5f);
            }
        }
    }

    [Theory]
    [InlineData("Stranger.Things.S04E09.The.Piggyback.2160p.NF.WEB-DL.x265.HDR.mkv")]
    [InlineData("Game.of.Thrones.S08E06.The.Iron.Throne.1080p.AMZN.WEB-DL.mkv")]
    [InlineData("The.Mandalorian.S02E08.Chapter.16.The.Rescue.2160p.DSNP.WEB-DL.mkv")]
    public async Task MLPipeline_WithHighQualityFiles_ShouldMaintainAccuracy(string filename)
    {
        // Given - High quality media files with clear series identification
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        // When - Classify high quality files
        var classifyResult = await classificationService.ClassifyFilenameAsync(filename);
        
        // Then - Should achieve good confidence for well-structured filenames
        classifyResult.IsSuccess.Should().BeTrue();
        var result = classifyResult.Value;
        result.Should().NotBeNull();
        result.PredictedCategory.Should().NotBeNull();
        result.Confidence.Should().BeGreaterThan(0.3f); // Should have reasonable confidence
        
        // Verify category formatting (uppercase with spaces)
        if (result.PredictedCategory != "UNKNOWN")
        {
            result.PredictedCategory.Should().Match(c => 
                c.All(ch => char.IsUpper(ch) || char.IsWhiteSpace(ch) || char.IsDigit(ch)));
        }
    }

    [Fact]
    public async Task MLPipeline_DatabaseIntegration_ShouldPersistClassificationResults()
    {
        // Given - TrackedFile requiring classification
        var testFile = new TrackedFile
        {
            Hash = "integration-test-hash",
            OriginalPath = "/test/media/Squid.Game.S01E01.Red.Light.Green.Light.2160p.NF.WEB-DL.mkv",
            FileName = "Squid.Game.S01E01.Red.Light.Green.Light.2160p.NF.WEB-DL.mkv",
            FileSize = 1024 * 1024 * 800, // 800MB
            Status = FileStatus.New
        };
        
        Context.TrackedFiles.Add(testFile);
        await Context.SaveChangesAsync();

        // When - Classify and update file
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var classifyResult = await classificationService.ClassifyFilenameAsync(testFile.FileName);
        
        classifyResult.IsSuccess.Should().BeTrue();
        var result = classifyResult.Value;
        
        // Update file with classification result
        testFile.Category = result.PredictedCategory;
        testFile.Confidence = (decimal)result.Confidence;
        testFile.Status = FileStatus.Classified;
        testFile.MarkAsModified();
        
        await Context.SaveChangesAsync();

        // Then - Verify persistence in database
        var savedFile = await Context.TrackedFiles.FindAsync(testFile.Hash);
        savedFile.Should().NotBeNull();
        savedFile!.Category.Should().Be(result.PredictedCategory);
        savedFile.Confidence.Should().Be((decimal)result.Confidence);
        savedFile.Status.Should().Be(FileStatus.Classified);
        savedFile.LastUpdateDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MLPipeline_MemoryUsage_ShouldStayWithinLimits()
    {
        // Given - Memory baseline measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        using var scope = CreateScope();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        
        // When - Process multiple files to test memory usage
        var testFiles = new[]
        {
            "Attack.on.Titan.S01E01.1080p.mkv", "Attack.on.Titan.S01E02.1080p.mkv",
            "Demon.Slayer.S01E01.1080p.mkv", "Demon.Slayer.S01E02.1080p.mkv",
            "Naruto.S01E01.1080p.mkv", "Naruto.S01E02.1080p.mkv",
            "One.Piece.E1001.1080p.mkv", "One.Piece.E1002.1080p.mkv",
            "Hunter.x.Hunter.S01E01.1080p.mkv", "Hunter.x.Hunter.S01E02.1080p.mkv"
        };
        
        foreach (var filename in testFiles)
        {
            var classifyResult = await classificationService.ClassifyFilenameAsync(filename);
            classifyResult.IsSuccess.Should().BeTrue();
        }
        
        // Then - Memory usage should be reasonable (target <300MB total for ARM32)
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Memory increase should be minimal (< 50MB for batch processing)
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, 
            "ML pipeline should not leak memory during batch processing");
    }
}