using FluentAssertions;
using MediaButler.Core.Common;
using MediaButler.ML.Configuration;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.ML.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for PredictionService validating prediction functionality.
/// These tests ensure predictions work correctly with Italian content optimization.
/// </summary>
public class PredictionServiceTests
{
    private readonly Mock<ILogger<PredictionService>> _mockLogger;
    private readonly Mock<ITokenizerService> _mockTokenizerService;
    private readonly Mock<IFeatureEngineeringService> _mockFeatureService;
    private readonly Mock<IModelTrainingService> _mockTrainingService;
    private readonly IOptions<MLConfiguration> _config;
    private readonly PredictionService _service;

    public PredictionServiceTests()
    {
        _mockLogger = new Mock<ILogger<PredictionService>>();
        _mockTokenizerService = new Mock<ITokenizerService>();
        _mockFeatureService = new Mock<IFeatureEngineeringService>();
        _mockTrainingService = new Mock<IModelTrainingService>();
        
        _config = Options.Create(new MLConfiguration
        {
            ModelPath = "/tmp/test.bin",
            AutoClassifyThreshold = 0.8f,
            SuggestionThreshold = 0.5f
        });

        _service = new PredictionService(
            _mockLogger.Object,
            _mockTokenizerService.Object,
            _mockFeatureService.Object,
            _mockTrainingService.Object,
            _config);
    }

    [Fact]
    public async Task PredictAsync_WithValidItalianFilename_ReturnsSuccessResult()
    {
        // Arrange
        var filename = "Il.Trono.Di.Spade.8x04.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv";
        SetupSuccessfulPredictionMocks(filename, "IL TRONO DI SPADE", 0.95);

        // Act
        var result = await _service.PredictAsync(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedCategory.Should().Be("IL TRONO DI SPADE");
        result.Value.Confidence.Should().Be((float)0.95);
        result.Value.Decision.Should().Be(ClassificationDecision.AutoClassify);
        result.Value.Decision.Should().NotBe(ClassificationDecision.Failed);
        result.Value.ProcessingTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PredictAsync_WithNullFilename_ReturnsFailureResult()
    {
        // Act
        var result = await _service.PredictAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public async Task PredictAsync_WithEmptyFilename_ReturnsFailureResult()
    {
        // Act
        var result = await _service.PredictAsync(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public async Task PredictAsync_WhenTokenizationFails_ReturnsFailureResult()
    {
        // Arrange
        var filename = "invalid.filename";
        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Failure("Tokenization failed"));

        // Act
        var result = await _service.PredictAsync(filename);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Tokenization failed");
    }

    [Fact]
    public async Task PredictAsync_WhenFeatureExtractionFails_ReturnsFailureResult()
    {
        // Arrange
        var filename = "test.mkv";
        var tokenized = CreateMockTokenizedFilename(filename);
        
        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Success(tokenized));
        _mockFeatureService.Setup(x => x.ExtractFeatures(tokenized))
            .Returns(Result<FeatureVector>.Failure("Feature extraction failed"));

        // Act
        var result = await _service.PredictAsync(filename);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Feature extraction failed");
    }

    [Fact]
    public async Task PredictAsync_WhenModelPredictionFails_ReturnsFailureResult()
    {
        // Arrange
        var filename = "test.mkv";
        var tokenized = CreateMockTokenizedFilename(filename);
        var features = CreateMockFeatureVector(filename);
        
        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Success(tokenized));
        _mockFeatureService.Setup(x => x.ExtractFeatures(tokenized))
            .Returns(Result<FeatureVector>.Success(features));

        // Act
        var result = await _service.PredictAsync(filename);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Model prediction failed");
    }

    [Theory]
    [InlineData(0.9, ClassificationDecision.AutoClassify)]
    [InlineData(0.7, ClassificationDecision.SuggestWithAlternatives)]
    [InlineData(0.3, ClassificationDecision.RequestManualCategorization)]
    public async Task PredictAsync_WithDifferentConfidenceLevels_ReturnsCorrectDecision(
        double confidence, ClassificationDecision expectedDecision)
    {
        // Arrange
        var filename = "test.mkv";
        SetupSuccessfulPredictionMocks(filename, "TEST SERIES", confidence);

        // Act
        var result = await _service.PredictAsync(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Decision.Should().Be(expectedDecision);
        result.Value.Confidence.Should().Be((float)confidence);
    }

    [Fact]
    public async Task PredictBatchAsync_WithValidFilenames_ReturnsSuccessResults()
    {
        // Arrange
        var filenames = new[]
        {
            "Breaking.Bad.S05E16.mkv",
            "Il.Trono.Di.Spade.8x04.ITA.mkv",
            "One.Piece.1089.Sub.ITA.mkv"
        };

        foreach (var filename in filenames)
        {
            SetupSuccessfulPredictionMocks(filename, "TEST SERIES", 0.8);
        }

        // Act
        var result = await _service.PredictBatchAsync(filenames);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalFiles.Should().Be(3);
        result.Value.SuccessfulClassifications.Should().Be(3);
        result.Value.AverageConfidence.Should().Be(0.8);
        result.Value.ProcessingDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task PredictBatchAsync_WithNullFilenames_ReturnsFailureResult()
    {
        // Act
        var result = await _service.PredictBatchAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task PredictBatchAsync_WithEmptyFilenames_ReturnsFailureResult()
    {
        // Act
        var result = await _service.PredictBatchAsync(Array.Empty<string>());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task PredictBatchAsync_WithMixedResults_ReturnsPartialSuccessResults()
    {
        // Arrange
        var filenames = new[] { "success.mkv", "failure.mkv" };
        
        // Setup first filename to succeed
        SetupSuccessfulPredictionMocks("success.mkv", "SUCCESS SERIES", 0.9);
        
        // Setup second filename to fail at tokenization
        _mockTokenizerService.Setup(x => x.TokenizeFilename("failure.mkv"))
            .Returns(Result<TokenizedFilename>.Failure("Tokenization failed"));

        // Act
        var result = await _service.PredictBatchAsync(filenames);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalFiles.Should().Be(2);
        result.Value.SuccessfulClassifications.Should().Be(1);
        result.Value.Results.Should().HaveCount(2);
        result.Value.Results.First(r => r.Filename == "success.mkv").Decision.Should().NotBe(ClassificationDecision.Failed);
        result.Value.Results.First(r => r.Filename == "failure.mkv").Decision.Should().Be(ClassificationDecision.Failed);
    }

    [Fact]
    public async Task ValidateFilenameAsync_WithValidItalianFilename_ReturnsValidResult()
    {
        // Arrange
        var filename = "Il.Trono.Di.Spade.8x04.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv";

        // Act
        var result = await _service.ValidateFilenameAsync(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.ProcessingConfidence.Should().BeGreaterThan(0.8);
        result.Value.ItalianIndicators.HasItalianLanguage.Should().BeTrue();
        result.Value.ItalianIndicators.HasItalianReleaseGroup.Should().BeTrue();
        result.Value.ItalianIndicators.ItalianReleaseGroup.Should().Be("NOVARIP");
        result.Value.ItalianIndicators.HasItalianSeries.Should().BeTrue();
        result.Value.Complexity.HasSpecialPatterns.Should().BeTrue();
        result.Value.Complexity.ComplexityScore.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task ValidateFilenameAsync_WithSimpleFilename_ReturnsLowerComplexity()
    {
        // Arrange
        var filename = "simple.video.mkv";

        // Act
        var result = await _service.ValidateFilenameAsync(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.Complexity.ComplexityScore.Should().BeLessThan(5);
        result.Value.Complexity.HasSpecialPatterns.Should().BeFalse();
        result.Value.ItalianIndicators.HasItalianLanguage.Should().BeFalse();
        result.Value.ItalianIndicators.HasItalianReleaseGroup.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateFilenameAsync_WithNullFilename_ReturnsFailureResult()
    {
        // Act
        var result = await _service.ValidateFilenameAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public async Task GetPerformanceStatsAsync_AfterPredictions_ReturnsAccurateStats()
    {
        // Arrange - Make some predictions to generate stats
        var filenames = new[] { "test1.mkv", "test2.mkv", "test3.mkv" };
        
        foreach (var filename in filenames)
        {
            SetupSuccessfulPredictionMocks(filename, "TEST", 0.85);
            await _service.PredictAsync(filename);
        }

        // Act
        var result = await _service.GetPerformanceStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPredictions.Should().BeGreaterOrEqualTo(3);
        result.Value.SuccessfulPredictions.Should().BeGreaterOrEqualTo(3);
        result.Value.SuccessRate.Should().Be(1.0); // All should be successful
        result.Value.AverageConfidence.Should().BeApproximately(0.85, 0.01);
        result.Value.AveragePredictionTime.Should().BeGreaterThan(TimeSpan.Zero);
        result.Value.ConfidenceBreakdown.HighConfidence.Should().BeGreaterOrEqualTo(3);
    }

    // Helper methods for test setup

    private void SetupSuccessfulPredictionMocks(string filename, string category, double confidence)
    {
        var tokenized = CreateMockTokenizedFilename(filename);
        var features = CreateMockFeatureVector(filename);
        // Pattern-based prediction does not need mock ML prediction result

        _mockTokenizerService.Setup(x => x.TokenizeFilename(filename))
            .Returns(Result<TokenizedFilename>.Success(tokenized));
        
        _mockFeatureService.Setup(x => x.ExtractFeatures(tokenized))
            .Returns(Result<FeatureVector>.Success(features));
    }

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
            EpisodeInfo = null,
            ReleaseGroup = "TestGroup",
            Metadata = new Dictionary<string, string>().AsReadOnly()
        };
    }

    private FeatureVector CreateMockFeatureVector(string filename)
    {
        return new FeatureVector
        {
            OriginalFilename = filename,
            TokenFeatures = new TokenFrequencyAnalysis
            {
                TotalTokens = 10,
                FrequentTokens = new List<TokenFrequency>
                {
                    new TokenFrequency
                    {
                        Token = "test",
                        Count = 5,
                        RelativeFrequency = 0.5,
                        ImportanceScore = 0.8,
                        Category = "series_name",
                        IsCommonAcrossSeries = false
                    }
                }.AsReadOnly(),
                RareTokens = new List<TokenFrequency>().AsReadOnly(),
                AverageTokenLength = 5.2,
                AlphaNumericRatio = 0.7,
                DiversityScore = 0.6,
                LanguageIndicators = new[] { "ita" }.ToList().AsReadOnly()
            },
            NGramFeatures = new List<NGramFeature>
            {
                new NGramFeature
                {
                    N = 2,
                    Tokens = new[] { "test", "series" }.ToList().AsReadOnly(),
                    Frequency = 3,
                    RelativeFrequency = 0.8,
                    DiscriminativePower = 0.9,
                    Context = NGramContext.SeriesName,
                    IsCrossBoundary = false
                }
            }.AsReadOnly(),
            QualityFeatures = new QualityFeatures
            {
                QualityScore = 85,
                ResolutionTier = QualityTier.High,
                SourceTier = QualityTier.High,
                HasHDR = false,
                HasMultipleAudio = false
            },
            PatternFeatures = new PatternMatchingFeatures
            {
                PatternType = FilenamePatternType.TVShowBasic,
                DetectedPatterns = new List<DetectedPattern>
                {
                    new DetectedPattern
                    {
                        Type = PatternType.Episode,
                        Pattern = "S01E01",
                        Confidence = 0.9,
                        Position = 10
                    }
                }.AsReadOnly(),
                LengthCategory = FilenameLengthCategory.Medium,
                PatternConfidence = 0.8,
                StructureComplexity = 6,
                SeparatorCount = 8,
                AlphaNumericRatio = 0.7,
                ContainsYear = false,
                ContainsEpisodePattern = true,
                ContainsQualityPattern = true,
                ContainsLanguagePattern = true,
                ContainsReleaseGroupPattern = true
            }
        };
    }

}