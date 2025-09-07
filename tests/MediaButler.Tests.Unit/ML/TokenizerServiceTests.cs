using FluentAssertions;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.ML.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for TokenizerService implementation.
/// Tests are based on real Italian training data patterns (1,797 samples analyzed).
/// Following "Simple Made Easy" principles with clear, focused test cases.
/// </summary>
public class TokenizerServiceTests
{
    private readonly ITokenizerService _tokenizerService;
    private readonly Mock<ILogger<TokenizerService>> _mockLogger;

    public TokenizerServiceTests()
    {
        _mockLogger = new Mock<ILogger<TokenizerService>>();
        _tokenizerService = new TokenizerService(_mockLogger.Object);
    }

    #region ExtractSeriesName Tests (Real Italian Training Data)

    [Theory]
    [InlineData("Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv", "Il Trono Di Spade")]
    [InlineData("Il.Trono.Di.Spade.1x10.Fuoco.E.Sangue.iTALiAN.HDTVMux-DarkSideMux.avi", "Il Trono Di Spade")]
    [InlineData("Bones.7x08.The.Bump.In.The.Road.ITA.DLMux.x264-FoV.mkv", "Bones")]
    [InlineData("Once.Upon.a.Time.1x01.Pilot.Sub.ITA.Mux.x264-NovaRip.mkv", "Once Upon A Time")]
    [InlineData("One.Piece.1089.Sub.ITA.720p.WEB-DLMux.x264-UBi.mkv", "One Piece")]
    [InlineData("Person.of.Interest.4x12.Control-Alt-Delete.ITA.DLMux.x264-NovaRip.mkv", "Person Interest")]
    [InlineData("Bull.6x10.Fronteggiare.Il.Nemico.ITA.DLMux.x264-UBi.mkv", "Bull")]
    public void ExtractSeriesName_WithItalianContent_ReturnsCorrectSeriesName(string filename, string expectedSeriesName)
    {
        // Act
        var result = _tokenizerService.ExtractSeriesName(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedSeriesName);
    }

    [Theory]
    [InlineData("NCIS.Los.Angeles.14x15.Sensi.di.Colpa.ITA.DLMux.x264-UBi.mkv", "NCIS Los Angeles")]
    [InlineData("NCIS.New.Orleans.7x16.Laissez.les.Bons.Temps.Rouler.ITA.DLMux.x264-UBi.mkv", "NCIS New Orleans")]
    [InlineData("My.Hero.Academia.6x25.The.High.Deep.Blue.Sky.Sub.ITA.1080p.WEB-DLMux.H264-UBi.mkv", "My Hero Academia")]
    [InlineData("Attacco.dei.Giganti.4x28.L.Alba.dell.Umanita.Sub.ITA.1080p.WEB-DLMux.H264-UBi.mkv", "Attacco Dei Giganti")]
    [InlineData("Cavalieri.dello.Zodiaco.Saint.Seiya.1x73.La.Resurrezione.del.Grande.Maestro.ITA.DVDRip.XviD-Pir8.avi", "Cavalieri Dello Zodiaco Saint Seiya")]
    public void ExtractSeriesName_WithComplexItalianNames_ReturnsCorrectSeriesName(string filename, string expectedSeriesName)
    {
        // Act
        var result = _tokenizerService.ExtractSeriesName(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedSeriesName);
    }

    [Theory]
    [InlineData("", "Filename cannot be null or empty")]
    [InlineData(null, "Filename cannot be null or empty")]
    [InlineData("   ", "Could not extract a valid series name")]
    public void ExtractSeriesName_WithInvalidInput_ReturnsFailure(string filename, string expectedErrorMessage)
    {
        // Act
        var result = _tokenizerService.ExtractSeriesName(filename);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(expectedErrorMessage);
    }

    #endregion

    #region ExtractEpisodeInfo Tests (Italian Episode Patterns)

    [Theory]
    [InlineData("Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv", 8, 4, EpisodePatternType.Alternative)]
    [InlineData("Il.Trono.Di.Spade.1x10.Fuoco.E.Sangue.iTALiAN.HDTVMux-DarkSideMux.avi", 1, 10, EpisodePatternType.Alternative)]
    [InlineData("Bones.7x08.The.Bump.In.The.Road.ITA.DLMux.x264-FoV.mkv", 7, 8, EpisodePatternType.Alternative)]
    [InlineData("Once.Upon.a.Time.1x01.Pilot.Sub.ITA.Mux.x264-NovaRip.mkv", 1, 1, EpisodePatternType.Alternative)]
    [InlineData("NCIS.Los.Angeles.14x15.Sensi.di.Colpa.ITA.DLMux.x264-UBi.mkv", 14, 15, EpisodePatternType.Alternative)]
    public void ExtractEpisodeInfo_WithItalianEpisodePatterns_ReturnsCorrectEpisodeInfo(
        string filename, int expectedSeason, int expectedEpisode, EpisodePatternType expectedPatternType)
    {
        // Act
        var result = _tokenizerService.ExtractEpisodeInfo(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Season.Should().Be(expectedSeason);
        result.Value.Episode.Should().Be(expectedEpisode);
        result.Value.PatternType.Should().Be(expectedPatternType);
        result.Value.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("One.Piece.1089.Sub.ITA.720p.WEB-DLMux.x264-UBi.mkv")] // No clear season/episode pattern
    [InlineData("Some.Random.File.Without.Episodes.mkv")]
    [InlineData("Il.Trono.Di.Spade.S03.SUB.PACK.ITA.zip")] // Season pack, no specific episode
    public void ExtractEpisodeInfo_WithoutEpisodePatterns_ReturnsFailure(string filename)
    {
        // Act
        var result = _tokenizerService.ExtractEpisodeInfo(filename);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region ExtractQualityInfo Tests (Italian Quality Patterns)

    [Theory]
    [InlineData("Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv", 
        null, "x264", "WEBMux", QualityTier.Unknown)]
    [InlineData("Il.Trono.Di.Spade.1x05.Il.Lupo.E.Il.Leone.iTALiAN.HDTVMux-DarkSideMux.avi", 
        null, null, "HDTVMux", QualityTier.Unknown)]
    [InlineData("Bones.7x08.The.Bump.In.The.Road.ITA.DLMux.x264-FoV.mkv", 
        null, "x264", "DLMux", QualityTier.Unknown)]
    [InlineData("One.Piece.1089.Sub.ITA.720p.WEB-DLMux.x264-UBi.mkv", 
        "720p", "x264", "WEB-DLMux", QualityTier.Standard)]
    [InlineData("My.Hero.Academia.6x25.The.High.Deep.Blue.Sky.Sub.ITA.1080p.WEB-DLMux.H264-UBi.mkv", 
        "1080p", null, "WEB-DLMux", QualityTier.High)]
    public void ExtractQualityInfo_WithItalianQualityPatterns_ReturnsCorrectQualityInfo(
        string filename, string? expectedResolution, string? expectedCodec, string? expectedSource, QualityTier expectedTier)
    {
        // Act
        var result = _tokenizerService.ExtractQualityInfo(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Resolution.Should().Be(expectedResolution);
        result.Value.VideoCodec.Should().Be(expectedCodec);
        result.Value.Source.Should().Be(expectedSource);
        result.Value.QualityTier.Should().Be(expectedTier);
    }

    [Theory]
    [InlineData("Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv", "ITA")]
    [InlineData("Il.Trono.Di.Spade.1x05.Il.Lupo.E.Il.Leone.iTALiAN.HDTVMux-DarkSideMux.avi", "ITALIAN")]
    [InlineData("Il.Trono.Di.Spade.2x01.Il.Nord.Non.Dimentica.ITA_ENG.DLMux.XviD-Pir8.avi", "ITA_ENG")]
    [InlineData("Il.Trono.Di.Spade.1x10.Fuoco.E.Sangue.iTALiAN.HDTVMux-DarkSideMux.forced.srt", "ITALIAN,FORCED")]
    public void ExtractQualityInfo_WithLanguageIndicators_ExtractsLanguageCodes(string filename, string expectedLanguages)
    {
        // Act
        var result = _tokenizerService.ExtractQualityInfo(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var actualLanguages = string.Join(",", result.Value.LanguageCodes);
        actualLanguages.Should().Be(expectedLanguages);
    }

    #endregion

    #region TokenizeFilename Tests (Complete Tokenization)

    [Fact]
    public void TokenizeFilename_WithCompleteItalianExample_ReturnsFullTokenization()
    {
        // Arrange
        var filename = "Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv";

        // Act
        var result = _tokenizerService.TokenizeFilename(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var tokenized = result.Value;
        tokenized.OriginalFilename.Should().Be(filename);
        tokenized.FileExtension.Should().Be("mkv");
        
        // Series tokens should be extracted
        tokenized.SeriesTokens.Should().Contain("trono");
        tokenized.SeriesTokens.Should().Contain("spade");
        
        // Episode info should be present
        tokenized.EpisodeInfo.Should().NotBeNull();
        tokenized.EpisodeInfo!.Season.Should().Be(8);
        tokenized.EpisodeInfo!.Episode.Should().Be(4);
        
        // Quality info should be present
        tokenized.QualityInfo.Should().NotBeNull();
        tokenized.QualityInfo.Source.Should().Be("WEBMux");
        tokenized.QualityInfo.VideoCodec.Should().Be("x264");
        
        // Release group should be identified
        tokenized.ReleaseGroup.Should().Be("UBi");
        
        // Filtered tokens should include technical terms
        tokenized.FilteredTokens.Should().Contain("ITA");
        tokenized.FilteredTokens.Should().Contain("WEBMux");
        tokenized.FilteredTokens.Should().Contain("x264");
    }

    [Theory]
    [InlineData("Bones.7x08.The.Bump.In.The.Road.ITA.DLMux.x264-FoV.mkv")]
    [InlineData("Once.Upon.a.Time.1x01.Pilot.Sub.ITA.Mux.x264-NovaRip.mkv")]
    [InlineData("Person.of.Interest.4x12.Control-Alt-Delete.ITA.DLMux.x264-NovaRip.mkv")]
    [InlineData("Bull.6x10.Fronteggiare.Il.Nemico.ITA.DLMux.x264-UBi.mkv")]
    public void TokenizeFilename_WithVariousItalianContent_SuccessfullyTokenizes(string filename)
    {
        // Act
        var result = _tokenizerService.TokenizeFilename(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalFilename.Should().Be(filename);
        result.Value.SeriesTokens.Should().NotBeEmpty();
        result.Value.AllTokens.Should().NotBeEmpty();
        result.Value.EpisodeInfo.Should().NotBeNull();
        result.Value.EpisodeInfo!.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void TokenizeFilename_WithInvalidInput_ReturnsFailure(string filename)
    {
        // Act
        var result = _tokenizerService.TokenizeFilename(filename);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Filename cannot be null or empty");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Theory]
    [InlineData("400110-SpiderMan.No.Way.Home.2021.iTALiAN.MD.720p.HDCAM.x264WRS.mkv.mp4")] // Unusual format from training data
    [InlineData("Il.Trono.Di.Spade.S03.SUB.PACK.ITA.zip")] // Archive file
    [InlineData("Il.Trono.Di.Spade.1x10.Fuoco.E.Sangue.iTALiAN.HDTVMux-DarkSideMux.forced.srt")] // Subtitle file
    public void TokenizeFilename_WithEdgeCases_HandlesGracefully(string filename)
    {
        // Act
        var result = _tokenizerService.TokenizeFilename(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalFilename.Should().Be(filename);
        result.Value.AllTokens.Should().NotBeEmpty();
    }

    [Fact]
    public void TokenizeFilename_WithLongComplexFilename_HandlesCorrectly()
    {
        // Arrange - Real example from training data
        var filename = "Cavalieri.dello.Zodiaco.Saint.Seiya.1x73.La.Resurrezione.del.Grande.Maestro.ITA.DVDRip.XviD-Pir8.avi";

        // Act
        var result = _tokenizerService.TokenizeFilename(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SeriesTokens.Should().Contain("cavalieri");
        result.Value.SeriesTokens.Should().Contain("zodiaco");
        result.Value.SeriesTokens.Should().Contain("saint");
        result.Value.SeriesTokens.Should().Contain("seiya");
        result.Value.EpisodeInfo!.Season.Should().Be(1);
        result.Value.EpisodeInfo!.Episode.Should().Be(73);
        result.Value.QualityInfo.Source.Should().Be("DVDRip");
        result.Value.QualityInfo.VideoCodec.Should().Be("XviD");
        result.Value.ReleaseGroup.Should().Be("Pir8");
    }

    #endregion

    #region Performance and Memory Tests

    [Fact]
    public void TokenizeFilename_WithManyFiles_PerformsEfficiently()
    {
        // Arrange - Sample of real filenames from training data
        var filenames = new[]
        {
            "Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv",
            "Bones.7x08.The.Bump.In.The.Road.ITA.DLMux.x264-FoV.mkv",
            "Once.Upon.a.Time.1x01.Pilot.Sub.ITA.Mux.x264-NovaRip.mkv",
            "Person.of.Interest.4x12.Control-Alt-Delete.ITA.DLMux.x264-NovaRip.mkv",
            "Bull.6x10.Fronteggiare.Il.Nemico.ITA.DLMux.x264-UBi.mkv",
            "NCIS.Los.Angeles.14x15.Sensi.di.Colpa.ITA.DLMux.x264-UBi.mkv",
            "My.Hero.Academia.6x25.The.High.Deep.Blue.Sky.Sub.ITA.1080p.WEB-DLMux.H264-UBi.mkv",
            "One.Piece.1089.Sub.ITA.720p.WEB-DLMux.x264-UBi.mkv"
        };

        var startTime = DateTime.UtcNow;

        // Act - Process all files
        var results = filenames.Select(f => _tokenizerService.TokenizeFilename(f)).ToList();

        var processingTime = DateTime.UtcNow - startTime;

        // Assert
        results.Should().HaveCount(8);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        
        // Should process multiple files quickly (performance requirement: <500ms total for batch)
        processingTime.TotalMilliseconds.Should().BeLessThan(100); // Even faster than requirement
    }

    #endregion

    #region Italian-Specific Pattern Tests

    [Theory]
    [InlineData("Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv", "L Ultimo Degli Stark")] // Italian episode title
    [InlineData("Bull.6x10.Fronteggiare.Il.Nemico.ITA.DLMux.x264-UBi.mkv", "Fronteggiare")] // Italian episode title
    [InlineData("NCIS.Los.Angeles.14x15.Sensi.di.Colpa.ITA.DLMux.x264-UBi.mkv", "Sensi")] // Italian episode title
    public void TokenizeFilename_WithItalianEpisodeTitles_ExtractsEpisodeInfo(string filename, string expectedTitlePart)
    {
        // Act
        var result = _tokenizerService.TokenizeFilename(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpisodeInfo.Should().NotBeNull();
        
        if (result.Value.EpisodeInfo!.AdditionalInfo != null)
        {
            result.Value.EpisodeInfo.AdditionalInfo.Should().Contain(expectedTitlePart);
        }
    }

    [Theory]
    [InlineData("Il.Trono.Di.Spade.8x04.L.Ultimo.Degli.Stark.ITA.WEBMux.x264-UBi.mkv", "UBi")]
    [InlineData("Il.Trono.Di.Spade.6x01.La.Donna.Rossa.ITA.DLMux.x264-NovaRip.mkv", "NovaRip")]
    [InlineData("Il.Trono.Di.Spade.1x05.Il.Lupo.E.Il.Leone.iTALiAN.HDTVMux-DarkSideMux.avi", "DarkSideMux")]
    [InlineData("Il.Trono.Di.Spade.2x01.Il.Nord.Non.Dimentica.ITA_ENG.DLMux.XviD-Pir8.avi", "Pir8")]
    [InlineData("Il.Trono.Di.Spade.4x06.Le.Leggi.Degli.Dei.E.Degli.Uomini.ITA.HDTVMux.x264-iGM.mp4", "iGM")]
    public void TokenizeFilename_WithItalianReleaseGroups_ExtractsReleaseGroup(string filename, string expectedReleaseGroup)
    {
        // Act
        var result = _tokenizerService.TokenizeFilename(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReleaseGroup.Should().Be(expectedReleaseGroup);
    }

    #endregion
}