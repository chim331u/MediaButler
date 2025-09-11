using FluentAssertions;
using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.ML.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for CategoryService validating category management functionality.
/// These tests ensure category registry, normalization, and suggestion features work correctly.
/// </summary>
public class CategoryServiceTests
{
    private readonly Mock<ILogger<CategoryService>> _mockLogger;
    private readonly Mock<ITokenizerService> _mockTokenizerService;
    private readonly CategoryService _service;
    
    public CategoryServiceTests()
    {
        _mockLogger = new Mock<ILogger<CategoryService>>();
        _mockTokenizerService = new Mock<ITokenizerService>();
        
        _service = new CategoryService(_mockLogger.Object, _mockTokenizerService.Object);
    }
    
    [Fact]
    public async Task GetCategoryRegistryAsync_ReturnsSuccessWithDefaultCategories()
    {
        // Act
        var result = await _service.GetCategoryRegistryAsync();
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Categories.Should().NotBeEmpty();
        result.Value.Categories.Should().ContainKey("IL TRONO DI SPADE");
        result.Value.Categories.Should().ContainKey("ONE PIECE");
        result.Value.Categories.Should().ContainKey("MY HERO ACADEMIA");
        result.Value.TotalCategories.Should().BeGreaterThan(0);
    }
    
    [Theory]
    [InlineData("il trono di spade", "IL TRONO DI SPADE")]
    [InlineData("Game.of.Thrones", "GAME OF THRONES")]
    [InlineData("The Breaking Bad", "BREAKING BAD")]
    [InlineData("my-hero_academia", "MY HERO ACADEMIA")]
    public void NormalizeCategory_WithValidNames_ReturnsNormalizedName(string input, string expected)
    {
        // Act
        var result = _service.NormalizeCategory(input);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeCategory_WithInvalidNames_ReturnsFailure(string input)
    {
        // Act
        var result = _service.NormalizeCategory(input);
    
        // Assert
        result.IsSuccess.Should().BeFalse();
    }
    
    [Fact]
    public void GetCategoryThreshold_WithValidCategory_ReturnsThreshold()
    {
        // Act
        var result = _service.GetCategoryThreshold("IL TRONO DI SPADE");
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0.0);
        result.Value.Should().BeLessThanOrEqualTo(1.0);
    }
    
    [Fact]
    public void GetCategoryThreshold_WithAlias_ReturnsThreshold()
    {
        // Act
        var result = _service.GetCategoryThreshold("GAME OF THRONES");
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0.0);
    }
    
    [Fact]
    public void GetCategoryThreshold_WithUnknownCategory_ReturnsFailure()
    {
        // Act
        var result = _service.GetCategoryThreshold("UNKNOWN SERIES");
    
        // Assert
        result.IsSuccess.Should().BeFalse();
    }
    
    [Fact]
    public async Task RegisterCategoryAsync_WithValidCategory_ReturnsSuccess()
    {
        // Arrange
        var newCategory = CategoryDefinition.Create(
            "TEST SERIES", 
            "Test Series", 
            CategoryType.TVSeries, 
            0.8);
    
        // Act
        var result = await _service.RegisterCategoryAsync(newCategory);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("TEST");
        result.Value.DisplayName.Should().Be("Test Series");
        result.Value.ConfidenceThreshold.Should().Be(0.8);
    }
    
    [Fact]
    public async Task RegisterCategoryAsync_WithExistingCategory_ReturnsFailure()
    {
        // Arrange
        var existingCategory = CategoryDefinition.Create(
            "IL TRONO DI SPADE", 
            "Il Trono di Spade", 
            CategoryType.TVSeries);
    
        // Act
        var result = await _service.RegisterCategoryAsync(existingCategory);
    
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }
    
    [Fact]
    public async Task UpdateCategoryAsync_WithValidUpdates_ReturnsUpdatedCategory()
    {
        // Arrange
        var updates = new CategoryUpdate
        {
            DisplayName = "Updated Display Name",
            ConfidenceThreshold = 0.95,
            NewAliases = new[] { "NEW ALIAS" }.AsReadOnly()
        };
    
        // Act
        var result = await _service.UpdateCategoryAsync("IL TRONO DI SPADE", updates);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Updated Display Name");
        result.Value.ConfidenceThreshold.Should().Be(0.95);
        result.Value.Aliases.Should().Contain("NEW ALIAS");
    }
    
    [Fact]
    public async Task UpdateCategoryAsync_WithUnknownCategory_ReturnsFailure()
    {
        // Arrange
        var updates = new CategoryUpdate { DisplayName = "New Name" };
    
        // Act
        var result = await _service.UpdateCategoryAsync("UNKNOWN SERIES", updates);
    
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
    
    [Fact]
    public async Task GetCategorySuggestionsAsync_WithItalianContent_ReturnsItalianSuggestions()
    {
        // Arrange
        var filename = "Il.Trono.Di.Spade.8x04.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv";
        var tokenized = CreateMockTokenizedFilename(filename);
        
        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Success(tokenized));
    
        // Act
        var result = await _service.GetCategorySuggestionsAsync(filename);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().NotBeEmpty();
        result.Value.Suggestions.First().CategoryName.Should().Be("IL TRONO DI SPADE");
        result.Value.HasHighConfidenceSuggestions.Should().BeTrue();
    }
    
    [Fact]
    public async Task GetCategorySuggestionsAsync_WithAnimeContent_ReturnsAnimeSuggestions()
    {
        // Arrange
        var filename = "One.Piece.1089.Sub.ITA.720p.WEB-DLMux.x264-UBi.mkv";
        var tokenized = CreateMockTokenizedFilename(filename);
        
        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Success(tokenized));
    
        // Act
        var result = await _service.GetCategorySuggestionsAsync(filename);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().NotBeEmpty();
        result.Value.Suggestions.Should().Contain(s => s.CategoryName == "ONE PIECE");
        result.Value.BestSuggestion.Should().NotBeNull();
    }
    
    [Fact]
    public async Task GetCategorySuggestionsAsync_WithTokenizationFailure_ReturnsEmptySuggestions()
    {
        // Arrange
        var filename = "invalid.filename";
        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Failure("Tokenization failed"));
    
        // Act
        var result = await _service.GetCategorySuggestionsAsync(filename);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().BeEmpty();
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetCategorySuggestionsAsync_WithInvalidMaxSuggestions_UsesDefaultValue(int maxSuggestions)
    {
        // Arrange
        var filename = "test.mkv";
        var tokenized = CreateMockTokenizedFilename(filename);
        
        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Success(tokenized));
    
        // Act
        var result = await _service.GetCategorySuggestionsAsync(filename, maxSuggestions);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Count.Should().BeLessOrEqualTo(5);
    }
    
    [Fact]
    public async Task RecordUserFeedbackAsync_WithValidFeedback_ReturnsSuccess()
    {
        // Arrange
        var feedback = new CategoryFeedback
        {
            Filename = "test.mkv",
            PredictedCategory = "PREDICTED CATEGORY",
            PredictionConfidence = 0.7,
            ActualCategory = "ACTUAL CATEGORY",
            Source = FeedbackSource.UserCorrection
        };
    
        // Act
        var result = await _service.RecordUserFeedbackAsync(feedback);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
    }
    
    [Fact]
    public async Task RecordUserFeedbackAsync_WithNullFeedback_ReturnsFailure()
    {
        // Act
        var result = await _service.RecordUserFeedbackAsync(null!);
    
        // Assert
        result.IsSuccess.Should().BeFalse();
    }
    
    [Fact]
    public async Task GetCategoryStatisticsAsync_ReturnsValidStatistics()
    {
        // Arrange - Record some feedback first
        var feedback = new CategoryFeedback
        {
            Filename = "test.mkv",
            PredictedCategory = "IL TRONO DI SPADE",
            PredictionConfidence = 0.9,
            ActualCategory = "IL TRONO DI SPADE",
            Source = FeedbackSource.UserConfirmation
        };
        await _service.RecordUserFeedbackAsync(feedback);
    
        // Act
        var result = await _service.GetCategoryStatisticsAsync();
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCategories.Should().BeGreaterThan(0);
        result.Value.CategoryStats.Should().NotBeEmpty();
        result.Value.OverallAccuracy.Should().BeGreaterOrEqualTo(0.0);
        result.Value.AverageConfidence.Should().BeGreaterOrEqualTo(0.0);
    }
    
    [Fact]
    public async Task MergeCategoriesAsync_WithValidCategories_ReturnsMergeResult()
    {
        // Arrange - First register a category to merge
        var sourceCategory = CategoryDefinition.Create(
            "SOURCE CATEGORY", 
            "Source Category", 
            CategoryType.TVSeries) with { FileCount = 10 };
        await _service.RegisterCategoryAsync(sourceCategory);
    
        // Act
        var result = await _service.MergeCategoriesAsync("SOURCE CATEGORY", "IL TRONO DI SPADE");
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SourceCategory.Should().Be("SOURCE CATEGORY");
        result.Value.TargetCategory.Should().Be("IL TRONO DI SPADE");
        result.Value.FilesTransferred.Should().Be(10);
    }
    
    [Fact]
    public async Task MergeCategoriesAsync_WithSameCategories_ReturnsFailure()
    {
        // Act
        var result = await _service.MergeCategoriesAsync("IL TRONO DI SPADE", "IL TRONO DI SPADE");
    
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be the same");
    }
    
    [Fact]
    public async Task MergeCategoriesAsync_WithUnknownSource_ReturnsFailure()
    {
        // Act
        var result = await _service.MergeCategoriesAsync("UNKNOWN SOURCE", "IL TRONO DI SPADE");
    
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Source category");
        result.Error.Should().Contain("not found");
    }
    
    [Theory]
    [InlineData("VALID CATEGORY", true)]
    [InlineData("My Hero Academia", true)]
    [InlineData("The Office (US)", true)]
    [InlineData("", false)]
    [InlineData("A", false)]
    [InlineData("X", false)]
    [InlineData("NEW", false)] // Reserved word
    public void ValidateCategoryName_WithVariousNames_ReturnsCorrectValidation(string name, bool expectedValid)
    {
        // Act
        var result = _service.ValidateCategoryName(name);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().Be(expectedValid);
        if (expectedValid)
        {
            result.Value.NormalizedName.Should().NotBeEmpty();
            result.Value.Issues.Should().BeEmpty();
        }
        else
        {
            result.Value.Issues.Should().NotBeEmpty();
        }
    }
    
    [Fact]
    public void ValidateCategoryName_WithInvalidCharacters_ReturnsValidationIssues()
    {
        // Act
        var result = _service.ValidateCategoryName("Invalid@Category#Name");
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.Issues.Should().Contain(issue => issue.Contains("invalid characters"));
        result.Value.Suggestions.Should().NotBeEmpty();
    }
    
    [Fact]
    public void ValidateCategoryName_WithTooLongName_ReturnsValidationIssues()
    {
        // Arrange
        var longName = new string('A', 101); // 101 characters
    
        // Act
        var result = _service.ValidateCategoryName(longName);
    
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.Issues.Should().Contain(issue => issue.Contains("100 characters"));
    }
    
    // Helper methods for test setup
    
    private TokenizedFilename CreateMockTokenizedFilename(string filename)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
        var tokens = nameWithoutExt.Split('.').Select(t => t.ToLowerInvariant()).ToList();
    
        return new TokenizedFilename
        {
            OriginalFilename = filename,
            SeriesTokens = tokens.Take(3).ToList().AsReadOnly(),
            AllTokens = tokens.AsReadOnly(),
            FilteredTokens = tokens.Where(t => !string.IsNullOrEmpty(t)).ToList().AsReadOnly(),
            FileExtension = Path.GetExtension(filename).TrimStart('.'),
            QualityInfo = new QualityInfo
            {
                Resolution = "1080p",
                VideoCodec = "x264",
                QualityTier = QualityTier.High
            },
            EpisodeInfo = filename.Contains("8x04") ? new EpisodeInfo
            {
                Season = 8,
                Episode = 4,
                AdditionalInfo = "L Ultimo Degli Stark"
            } : null,
            ReleaseGroup = ExtractReleaseGroup(filename),
            Metadata = new Dictionary<string, string>().AsReadOnly()
        };
    }
    
    private string? ExtractReleaseGroup(string filename)
    {
        if (filename.Contains("NovaRip", StringComparison.OrdinalIgnoreCase))
            return "NovaRip";
        if (filename.Contains("UBi", StringComparison.OrdinalIgnoreCase))
            return "UBi";
        if (filename.Contains("PIR8", StringComparison.OrdinalIgnoreCase))
            return "PIR8";
        return null;
    }
}