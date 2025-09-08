using FluentAssertions;
using MediaButler.ML.Models;
using MediaButler.ML.Services;
using MediaButler.ML.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for FeatureEngineeringService validating feature extraction functionality.
/// These tests ensure feature vectors are generated correctly from tokenized filenames.
/// </summary>
public class FeatureEngineeringServiceTests
{
    private readonly Mock<ILogger<FeatureEngineeringService>> _mockLogger;
    private readonly IOptions<MLConfiguration> _config;
    private readonly FeatureEngineeringService _service;

    public FeatureEngineeringServiceTests()
    {
        _mockLogger = new Mock<ILogger<FeatureEngineeringService>>();
        _config = Options.Create(new MLConfiguration
        {
            ModelPath = "/tmp/test.bin",
            AutoClassifyThreshold = 0.7f,
            SuggestionThreshold = 0.5f
        });
        _service = new FeatureEngineeringService(_mockLogger.Object, _config);
    }

    [Fact]
    public void ExtractFeatures_WithValidTokenizedFilename_ReturnsSuccessResult()
    {
        // Arrange
        var tokenizedFilename = CreateSampleTokenizedFilename();

        // Act
        var result = _service.ExtractFeatures(tokenizedFilename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OriginalFilename.Should().Be(tokenizedFilename.OriginalFilename);
        result.Value.FeatureCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExtractFeatures_WithNullInput_ReturnsFailureResult()
    {
        // Act
        var result = _service.ExtractFeatures(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be null");
    }

    [Theory]
    [InlineData("Il.Trono.Di.Spade.8x04.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv")]
    [InlineData("One.Piece.1089.Sub.ITA.720p.WEB-DLMux.x264-UBi.mkv")]
    [InlineData("My.Hero.Academia.6x25.ITA.1080p.WEB-DLMux-Pir8.mkv")]
    public void ExtractFeatures_WithItalianContentFilenames_ExtractsRelevantFeatures(string filename)
    {
        // Arrange
        var tokenizedFilename = CreateTokenizedFilenameFromFilename(filename);

        // Act
        var result = _service.ExtractFeatures(tokenizedFilename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var features = result.Value;
        
        // Should detect Italian language features
        features.TokenFeatures.LanguageIndicators.Should().Contain(lang => 
            lang.Contains("ita", StringComparison.OrdinalIgnoreCase));
            
        // Should have quality features
        features.QualityFeatures.Should().NotBeNull();
        features.QualityFeatures.QualityScore.Should().BeGreaterThan(0);
        
        // Should have pattern features
        features.PatternFeatures.Should().NotBeNull();
        features.PatternFeatures.ContainsQualityPattern.Should().BeTrue();
        features.PatternFeatures.ContainsLanguagePattern.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeTokenFrequency_WithValidTokens_ReturnsAnalysisResult()
    {
        // Arrange
        var tokens = new[] { "il", "trono", "di", "spade", "trono", "spade" }.ToList().AsReadOnly();

        // Act
        var result = _service.AnalyzeTokenFrequency(tokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTokens.Should().Be(6);
        result.Value.FrequentTokens.Should().NotBeEmpty();
        result.Value.FrequentTokens.First().Token.Should().BeOneOf("trono", "spade"); // Most frequent
        result.Value.AverageTokenLength.Should().BeApproximately(4.5, 0.1); // Average of token lengths
        result.Value.DiversityScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnalyzeTokenFrequency_WithEmptyTokens_ReturnsFailureResult()
    {
        // Arrange
        var emptyTokens = new List<string>().AsReadOnly();

        // Act
        var result = _service.AnalyzeTokenFrequency(emptyTokens);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GenerateNGrams_WithValidTokensAndN_ReturnsNGramFeatures(int n)
    {
        // Arrange
        var tokens = new[] { "breaking", "bad", "season", "five", "episode", "one" }.ToList().AsReadOnly();

        // Act
        var result = _service.GenerateNGrams(tokens, n);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.First().N.Should().Be(n);
        result.Value.First().Tokens.Should().HaveCount(n);
        result.Value.All(ng => ng.DiscriminativePower > 0).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void GenerateNGrams_WithInvalidN_ReturnsFailureResult(int invalidN)
    {
        // Arrange
        var tokens = new[] { "test", "tokens" }.ToList().AsReadOnly();

        // Act
        var result = _service.GenerateNGrams(tokens, invalidN);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("must be between 1 and 5");
    }

    [Fact]
    public void ExtractQualityFeatures_WithHighQualityInfo_ReturnsHighQualityFeatures()
    {
        // Arrange
        var qualityInfo = new QualityInfo
        {
            Resolution = "1080p",
            Source = "BluRay",
            Codec = "x265",
            Tier = QualityTier.UltraHigh
        };

        // Act
        var result = _service.ExtractQualityFeatures(qualityInfo);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResolutionTier.Should().Be(QualityTier.UltraHigh);
        result.Value.IsHighQuality.Should().BeTrue();
        result.Value.QualityScore.Should().BeGreaterThan(75);
    }

    [Fact]
    public void ExtractQualityFeatures_WithLowQualityInfo_ReturnsLowQualityFeatures()
    {
        // Arrange
        var qualityInfo = new QualityInfo
        {
            Resolution = "480p",
            Source = "DVDRip", 
            Codec = null,
            Tier = QualityTier.Low
        };

        // Act
        var result = _service.ExtractQualityFeatures(qualityInfo);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResolutionTier.Should().Be(QualityTier.Low);
        result.Value.IsLowQuality.Should().BeTrue();
        result.Value.QualityScore.Should().BeLessThan(50);
    }

    [Theory]
    [InlineData("Breaking.Bad.S05E16.Final.1080p.BluRay.x264-KILLERS.mkv")]
    [InlineData("Game.of.Thrones.2011.1080p.BluRay.x265-MIXED.mkv")]
    [InlineData("Simple.filename.mkv")]
    public void ExtractPatternFeatures_WithVariousFilenames_DetectsCorrectPatterns(string filename)
    {
        // Act
        var result = _service.ExtractPatternFeatures(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PatternConfidence.Should().BeGreaterThan(0);
        result.Value.StructureComplexity.Should().BeInRange(0, 10);
        result.Value.SeparatorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExtractPatternFeatures_WithComplexFilename_ReturnsHighComplexity()
    {
        // Arrange
        var complexFilename = "Game.of.Thrones.S08E06.The.Iron.Throne.2019.1080p.AMZN.WEB-DL.DDP5.1.H.264-GoT.mkv";

        // Act
        var result = _service.ExtractPatternFeatures(complexFilename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StructureComplexity.Should().BeGreaterThan(5);
        result.Value.ContainsYear.Should().BeTrue();
        result.Value.ContainsEpisodePattern.Should().BeTrue();
        result.Value.ContainsQualityPattern.Should().BeTrue();
    }

    [Fact]
    public void ExtractFeatures_WithEpisodeInfo_IncludesEpisodeFeatures()
    {
        // Arrange
        var tokenizedFilename = CreateSampleTokenizedFilename();
        tokenizedFilename = tokenizedFilename with 
        { 
            EpisodeInfo = new EpisodeInfo
            {
                Season = 5,
                Episode = 16,
                PatternType = EpisodePatternType.SeasonEpisode,
                RawPattern = "S05E16",
                AdditionalInfo = "Final"
            }
        };

        // Act
        var result = _service.ExtractFeatures(tokenizedFilename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpisodeFeatures.Should().NotBeNull();
        result.Value.EpisodeFeatures!.SeasonNumber.Should().Be(5);
        result.Value.EpisodeFeatures!.EpisodeNumber.Should().Be(16);
        result.Value.EpisodeFeatures!.IsSpecialEpisode.Should().BeTrue(); // "Final" detected
    }

    [Fact]
    public void ExtractFeatures_WithReleaseGroup_IncludesReleaseGroupFeatures()
    {
        // Arrange
        var tokenizedFilename = CreateSampleTokenizedFilename();
        tokenizedFilename = tokenizedFilename with { ReleaseGroup = "NovaRip" };

        // Act
        var result = _service.ExtractFeatures(tokenizedFilename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReleaseGroupFeatures.Should().NotBeNull();
        result.Value.ReleaseGroupFeatures!.ReleaseGroup.Should().Be("NovaRip");
        result.Value.ReleaseGroupFeatures!.IsWellKnown.Should().BeTrue();
        result.Value.ReleaseGroupFeatures!.Region.Should().Be(ReleaseGroupRegion.Italian);
    }

    [Fact]
    public void FeatureVector_ToFeatureArray_ReturnsValidFloatArray()
    {
        // Arrange
        var tokenizedFilename = CreateCompleteTokenizedFilename();

        // Act
        var result = _service.ExtractFeatures(tokenizedFilename);
        var featureArray = result.Value.ToFeatureArray();

        // Assert
        featureArray.Should().NotBeEmpty();
        featureArray.Should().AllSatisfy(f => float.IsFinite(f));
        featureArray.Length.Should().Be(result.Value.FeatureCount);
    }

    [Fact]
    public void FeatureVector_GetFeatureNames_ReturnsCorrectCount()
    {
        // Arrange
        var tokenizedFilename = CreateCompleteTokenizedFilename();

        // Act
        var result = _service.ExtractFeatures(tokenizedFilename);
        var featureNames = result.Value.GetFeatureNames();

        // Assert
        featureNames.Should().HaveCount(result.Value.FeatureCount);
        featureNames.Should().AllSatisfy(name => !string.IsNullOrEmpty(name));
    }

    // Helper methods for creating test data

    private TokenizedFilename CreateSampleTokenizedFilename()
    {
        return new TokenizedFilename
        {
            OriginalFilename = "Il.Trono.Di.Spade.8x04.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv",
            SeriesTokens = new[] { "il", "trono", "di", "spade" }.ToList().AsReadOnly(),
            AllTokens = new[] { "il", "trono", "di", "spade", "8x04", "ita", "1080p", "web", "dlmux", "x264", "novarip" }.ToList().AsReadOnly(),
            FilteredTokens = new[] { "ita", "1080p", "web", "dlmux", "x264" }.ToList().AsReadOnly(),
            FileExtension = "mkv",
            QualityInfo = new QualityInfo
            {
                Resolution = "1080p",
                Source = "WEB-DLMux",
                Codec = "x264",
                Tier = QualityTier.High
            },
            EpisodeInfo = null,
            ReleaseGroup = "NovaRip",
            Metadata = new Dictionary<string, object>().AsReadOnly()
        };
    }

    private TokenizedFilename CreateCompleteTokenizedFilename()
    {
        var sample = CreateSampleTokenizedFilename();
        return sample with
        {
            EpisodeInfo = new EpisodeInfo
            {
                Season = 8,
                Episode = 4,
                PatternType = EpisodePatternType.SeasonEpisodeExtended,
                RawPattern = "8x04",
                AdditionalInfo = "L Ultimo Degli Stark"
            }
        };
    }

    private TokenizedFilename CreateTokenizedFilenameFromFilename(string filename)
    {
        // Simplified tokenization for testing
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
        var tokens = nameWithoutExt.Split('.', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.ToLowerInvariant())
                                  .ToList();

        return new TokenizedFilename
        {
            OriginalFilename = filename,
            SeriesTokens = tokens.Take(4).ToList().AsReadOnly(), // First few as series
            AllTokens = tokens.AsReadOnly(),
            FilteredTokens = tokens.Where(t => t.Contains("ita") || t.Contains("1080") || t.Contains("web")).ToList().AsReadOnly(),
            FileExtension = Path.GetExtension(filename).TrimStart('.'),
            QualityInfo = new QualityInfo
            {
                Resolution = tokens.FirstOrDefault(t => t.Contains("1080") || t.Contains("720")),
                Source = tokens.FirstOrDefault(t => t.Contains("web") || t.Contains("hdtv")),
                Codec = tokens.FirstOrDefault(t => t.Contains("x264") || t.Contains("x265")),
                Tier = tokens.Any(t => t.Contains("1080")) ? QualityTier.High : QualityTier.Standard
            },
            EpisodeInfo = null,
            ReleaseGroup = tokens.LastOrDefault(),
            Metadata = new Dictionary<string, object>().AsReadOnly()
        };
    }
}