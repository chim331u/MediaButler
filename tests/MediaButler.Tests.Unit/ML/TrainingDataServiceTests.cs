using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MediaButler.ML.Services;
using MediaButler.ML.Models;
using Xunit;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for TrainingDataService covering comprehensive training data management.
/// Tests focus on Italian content optimization and data quality validation.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" testing principles:
/// - Test behavior, not implementation
/// - Clear Given-When-Then structure  
/// - Values over state testing
/// - Independent, deterministic tests
/// </remarks>
public class TrainingDataServiceTests
{
    private readonly Mock<ILogger<TrainingDataService>> _mockLogger;
    private readonly TrainingDataService _service;

    public TrainingDataServiceTests()
    {
        _mockLogger = new Mock<ILogger<TrainingDataService>>();
        _service = new TrainingDataService(_mockLogger.Object);
    }

    [Theory]
    [InlineData("Breaking.Bad.S01E01.1080p.BluRay.ITA-NovaRip.mkv", "BREAKING BAD")]
    [InlineData("The.Walking.Dead.S05E16.720p.HDTV.ITA.ENG-DarkSideMux.mkv", "THE WALKING DEAD")]
    [InlineData("Game.of.Thrones.S08E06.4K.2160p.WEB-DL.SUB.ITA-Pir8.mkv", "GAME OF THRONES")]
    public async Task AddTrainingSampleAsync_WithValidItalianContent_ReturnsSuccess(string filename, string expectedCategory)
    {
        // Given - Italian filename with standard TV episode pattern
        
        // When - Adding training sample
        var result = await _service.AddTrainingSampleAsync(filename, expectedCategory);
        
        // Then - Should succeed
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "BREAKING BAD")]
    [InlineData("Breaking.Bad.S01E01.mkv", "")]
    [InlineData(null, "BREAKING BAD")]
    [InlineData("Breaking.Bad.S01E01.mkv", null)]
    public async Task AddTrainingSampleAsync_WithInvalidInput_ReturnsFailure(string filename, string category)
    {
        // Given - Invalid input parameters
        
        // When - Adding training sample with invalid data
        var result = await _service.AddTrainingSampleAsync(filename, category);
        
        // Then - Should fail with appropriate error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetTrainingDataAsync_WithDefaultRatios_ReturnsBalancedSplit()
    {
        // Given - Default split ratios (0.7 training, 0.2 validation)
        
        // When - Generating training data split
        var result = await _service.GetTrainingDataAsync();
        
        // Then - Should return balanced stratified split
        result.IsSuccess.Should().BeTrue();
        
        var split = result.Value;
        split.Should().NotBeNull();
        split.TotalSamples.Should().BeGreaterThan(0);
        
        // Verify split ratios are approximately correct
        var totalSamples = split.TotalSamples;
        var trainRatio = (double)split.TrainingSet.Count / totalSamples;
        var validationRatio = (double)split.ValidationSet.Count / totalSamples;
        var testRatio = (double)split.TestSet.Count / totalSamples;
        
        trainRatio.Should().BeApproximately(0.7, 0.1);
        validationRatio.Should().BeApproximately(0.2, 0.1);
        testRatio.Should().BeApproximately(0.1, 0.1);
        
        // Verify all splits contain samples
        split.TrainingSet.Should().NotBeEmpty();
        split.ValidationSet.Should().NotBeEmpty();
        split.TestSet.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(0.8, 0.15)]
    [InlineData(0.6, 0.3)]
    [InlineData(0.9, 0.05)]
    public async Task GetTrainingDataAsync_WithCustomRatios_ReturnsCorrectSplit(double trainRatio, double validationRatio)
    {
        // Given - Custom split ratios
        
        // When - Generating training data with custom ratios
        var result = await _service.GetTrainingDataAsync(trainRatio, validationRatio);
        
        // Then - Should return split with specified ratios
        result.IsSuccess.Should().BeTrue();
        
        var split = result.Value;
        var totalSamples = split.TotalSamples;
        var actualTrainRatio = (double)split.TrainingSet.Count / totalSamples;
        var actualValidationRatio = (double)split.ValidationSet.Count / totalSamples;
        
        actualTrainRatio.Should().BeApproximately(trainRatio, 0.1);
        actualValidationRatio.Should().BeApproximately(validationRatio, 0.1);
        
        split.SplitRatios.Train.Should().Be(trainRatio);
        split.SplitRatios.Validation.Should().Be(validationRatio);
    }

    [Theory]
    [InlineData(-0.1, 0.5)] // Negative training ratio
    [InlineData(0.5, -0.1)] // Negative validation ratio
    [InlineData(0.8, 0.3)]  // Ratios sum > 1.0
    [InlineData(0.0, 0.5)]  // Zero training ratio
    [InlineData(0.5, 0.0)]  // Zero validation ratio
    public async Task GetTrainingDataAsync_WithInvalidRatios_ReturnsFailure(double trainRatio, double validationRatio)
    {
        // Given - Invalid split ratios
        
        // When - Attempting to generate split with invalid ratios
        var result = await _service.GetTrainingDataAsync(trainRatio, validationRatio);
        
        // Then - Should fail with validation error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ratio");
    }

    [Fact]
    public async Task ValidateTrainingDataAsync_WithGeneratedData_ReturnsValidationReport()
    {
        // Given - Service with comprehensive training data
        
        // When - Validating training data quality
        var result = await _service.ValidateTrainingDataAsync();
        
        // Then - Should return comprehensive validation report
        result.IsSuccess.Should().BeTrue();
        
        var validation = result.Value;
        validation.Should().NotBeNull();
        validation.QualityScore.Should().BeGreaterThan(0);
        validation.Issues.Should().NotBeNull();
        validation.Recommendations.Should().NotBeNull();
        
        // With well-generated data, should pass validation
        validation.IsValid.Should().BeTrue();
        
        // Should have high quality score for generated data
        validation.QualityScore.Should().BeGreaterOrEqualTo(0.7f);
    }

    [Fact]
    public async Task GetDatasetStatisticsAsync_WithGeneratedData_ReturnsComprehensiveStats()
    {
        // Given - Service with training data
        
        // When - Getting dataset statistics
        var result = await _service.GetDatasetStatisticsAsync();
        
        // Then - Should return detailed statistics
        result.IsSuccess.Should().BeTrue();
        
        var stats = result.Value;
        stats.Should().NotBeNull();
        stats.TotalSamples.Should().BeGreaterThan(0);
        stats.UniqueCategories.Should().BeGreaterThan(0);
        stats.SamplesPerCategory.Should().NotBeEmpty();
        
        // Should have reasonable category distribution
        stats.AverageSamplesPerCategory.Should().BeGreaterThan(0);
        
        // Should cover Italian content categories
        stats.SamplesPerCategory.Keys.Should().Contain(category => 
            category.Contains("BREAKING") || category.Contains("WALKING") || category.Contains("GAME"));
        
        // Generated data should be reasonably balanced
        stats.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task ExportTrainingDataAsync_WithValidPath_ExportsSuccessfully()
    {
        // Given - Valid export file path
        var tempPath = Path.GetTempFileName();
        
        try
        {
            // When - Exporting training data
            var result = await _service.ExportTrainingDataAsync(tempPath);
            
            // Then - Should export successfully
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
            
            // Verify file was created and has content
            File.Exists(tempPath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(tempPath);
            fileContent.Should().NotBeNullOrEmpty();
            fileContent.Should().Contain("filename");
            fileContent.Should().Contain("category");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ExportTrainingDataAsync_WithInvalidPath_ReturnsFailure(string filePath)
    {
        // Given - Invalid file path
        
        // When - Attempting to export with invalid path
        var result = await _service.ExportTrainingDataAsync(filePath);
        
        // Then - Should fail with appropriate error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    [Fact]
    public async Task ImportTrainingDataAsync_WithNonexistentFile_ReturnsFailure()
    {
        // Given - Path to nonexistent file
        var nonexistentPath = "/path/to/nonexistent/file.json";
        
        // When - Attempting to import from nonexistent file
        var result = await _service.ImportTrainingDataAsync(nonexistentPath);
        
        // Then - Should fail with file not found error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ImportTrainingDataAsync_WithValidJsonFile_ReturnsImportResult()
    {
        // Given - Valid JSON training data file
        var tempPath = Path.GetTempFileName();
        var sampleData = new[]
        {
            new { filename = "Test.Series.S01E01.mkv", category = "TEST SERIES", confidence = 0.9 },
            new { filename = "Another.Show.S02E05.mkv", category = "ANOTHER SHOW", confidence = 0.8 }
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(sampleData);
        await File.WriteAllTextAsync(tempPath, json);
        
        try
        {
            // When - Importing training data
            var result = await _service.ImportTrainingDataAsync(tempPath);
            
            // Then - Should return import results
            result.IsSuccess.Should().BeTrue();
            
            var importResult = result.Value;
            importResult.Should().NotBeNull();
            importResult.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
            importResult.Warnings.Should().NotBeNull();
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ImportTrainingDataAsync_WithInvalidPath_ReturnsFailure(string filePath)
    {
        // Given - Invalid file path
        
        // When - Attempting to import with invalid path
        var result = await _service.ImportTrainingDataAsync(filePath);
        
        // Then - Should fail with appropriate error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    [Fact]
    public async Task GeneratedTrainingSamples_ShouldIncludeItalianContent()
    {
        // Given - Training data service
        
        // When - Getting training data
        var result = await _service.GetTrainingDataAsync();
        
        // Then - Should include Italian content categories
        result.IsSuccess.Should().BeTrue();
        
        var allSamples = result.Value.TrainingSet
            .Concat(result.Value.ValidationSet)
            .Concat(result.Value.TestSet)
            .ToList();
        
        // Should include popular Italian series
        allSamples.Should().Contain(s => s.Category.Contains("BREAKING BAD"));
        allSamples.Should().Contain(s => s.Category.Contains("WALKING DEAD"));
        allSamples.Should().Contain(s => s.Category.Contains("GAME OF THRONES"));
        
        // Should have realistic filename patterns
        allSamples.Should().Contain(s => s.Filename.Contains("S0") && s.Filename.Contains("E0"));
        allSamples.Should().Contain(s => s.Filename.Contains("1080p") || s.Filename.Contains("720p"));
        allSamples.Should().Contain(s => s.Filename.Contains("ITA"));
        
        // Should have confidence scores
        allSamples.Should().OnlyContain(s => s.Confidence >= 0.1 && s.Confidence <= 1.0);
        
        // Should have creation dates
        allSamples.Should().OnlyContain(s => s.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task TrainingSamples_ShouldHaveConsistentCategoryNaming()
    {
        // Given - Training data service
        
        // When - Getting dataset statistics
        var result = await _service.GetDatasetStatisticsAsync();
        
        // Then - All categories should follow UPPERCASE naming convention
        result.IsSuccess.Should().BeTrue();
        
        var categories = result.Value.SamplesPerCategory.Keys;
        categories.Should().OnlyContain(category => 
            category.ToUpperInvariant() == category, 
            "All categories should be UPPERCASE for consistency");
        
        // Should not have duplicate categories with different casing
        var distinctCategories = categories.Distinct(StringComparer.OrdinalIgnoreCase);
        categories.Count().Should().Be(distinctCategories.Count(), 
            "Should not have duplicate categories with different casing");
    }

    [Fact]
    public async Task ValidateTrainingDataAsync_WithImbalancedData_IdentifiesIssues()
    {
        // Given - Service that will generate data (some imbalance expected due to randomization)
        
        // When - Validating training data
        var result = await _service.ValidateTrainingDataAsync();
        
        // Then - Should identify any data quality issues
        result.IsSuccess.Should().BeTrue();
        
        var validation = result.Value;
        
        // Should provide recommendations if any issues found
        if (validation.Issues.Any())
        {
            validation.Recommendations.Should().NotBeEmpty();
            validation.Issues.Should().OnlyContain(issue => 
                Enum.IsDefined(typeof(IssueSeverity), issue.Severity));
        }
        
        // Should not have critical issues with generated data
        validation.Issues.Should().NotContain(issue => issue.Severity == IssueSeverity.Critical);
        
        // Quality score should be reasonable
        validation.QualityScore.Should().BeInRange(0.0f, 1.0f);
    }

    [Fact]
    public async Task TrainingDataSplit_ShouldMaintainCategoryBalance()
    {
        // Given - Standard split ratios
        
        // When - Getting training data split
        var result = await _service.GetTrainingDataAsync(0.7, 0.2);
        
        // Then - Each split should contain samples from multiple categories
        result.IsSuccess.Should().BeTrue();
        
        var split = result.Value;
        
        // Training set should have multiple categories
        var trainCategories = split.TrainingSet.Select(s => s.Category).Distinct().Count();
        trainCategories.Should().BeGreaterThan(5, "Training set should have diverse categories");
        
        // Validation set should have multiple categories
        var validationCategories = split.ValidationSet.Select(s => s.Category).Distinct().Count();
        validationCategories.Should().BeGreaterThan(3, "Validation set should have diverse categories");
        
        // Test set should have multiple categories
        var testCategories = split.TestSet.Select(s => s.Category).Distinct().Count();
        testCategories.Should().BeGreaterThan(2, "Test set should have diverse categories");
        
        // No split should be empty
        split.TrainingSet.Should().NotBeEmpty();
        split.ValidationSet.Should().NotBeEmpty();  
        split.TestSet.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TrainingSamples_ShouldHaveRealisticConfidenceScores()
    {
        // Given - Training data service
        
        // When - Getting training samples
        var result = await _service.GetTrainingDataAsync();
        
        // Then - Confidence scores should be realistic and varied
        result.IsSuccess.Should().BeTrue();
        
        var allSamples = result.Value.TrainingSet
            .Concat(result.Value.ValidationSet)
            .Concat(result.Value.TestSet)
            .ToList();
        
        // All confidence scores should be valid
        allSamples.Should().OnlyContain(s => s.Confidence >= 0.1 && s.Confidence <= 1.0);
        
        // Should have variety in confidence scores (not all the same)
        var uniqueConfidences = allSamples.Select(s => s.Confidence).Distinct().Count();
        uniqueConfidences.Should().BeGreaterThan(3, "Should have varied confidence scores");
        
        // High-quality samples should have higher confidence
        var highQualitySamples = allSamples.Where(s => 
            s.Filename.Contains("1080p") || s.Filename.Contains("4K") || s.Filename.Contains("BluRay"));
        
        if (highQualitySamples.Any())
        {
            highQualitySamples.Average(s => s.Confidence).Should().BeGreaterThan(0.6);
        }
    }
}