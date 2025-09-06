using FluentAssertions;
using MediaButler.Tests.Unit.Builders;
using MediaButler.Tests.Unit.Infrastructure;
using MediaButler.Tests.Unit.ObjectMothers;

namespace MediaButler.Tests.Unit.Infrastructure;

/// <summary>
/// Tests to verify the test infrastructure works correctly.
/// Demonstrates the builder pattern and object mother usage.
/// </summary>
public class TestInfrastructureTests : TestBase
{
    [Fact]
    public void TrackedFileBuilder_WithDefaultValues_ShouldCreateValidFile()
    {
        // Act
        var file = new TrackedFileBuilder().Build();

        // Assert
        file.Should().NotBeNull();
        file.Hash.Should().NotBeNullOrEmpty();
        file.FileName.Should().NotBeNullOrEmpty();
        file.OriginalPath.Should().NotBeNullOrEmpty();
        file.FileSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TrackedFileBuilder_WithFluentConfiguration_ShouldApplyAllSettings()
    {
        // Arrange
        var expectedHash = TestHash("123");
        var expectedFileName = "Test.Show.S01E01.mkv";
        var expectedPath = TestFilePath(expectedFileName);
        var expectedSize = 1_000_000L;

        // Act
        var file = new TrackedFileBuilder()
            .WithHash(expectedHash)
            .WithFileName(expectedFileName)
            .WithOriginalPath(expectedPath)
            .WithFileSize(expectedSize)
            .AsClassified("TEST SHOW", 0.9m)
            .Build();

        // Assert
        file.Hash.Should().Be(expectedHash);
        file.FileName.Should().Be(expectedFileName);
        file.OriginalPath.Should().Be(expectedPath);
        file.FileSize.Should().Be(expectedSize);
        file.SuggestedCategory.Should().Be("TEST SHOW");
        file.Confidence.Should().Be(0.9m);
    }

    [Fact]
    public void TrackedFileObjectMother_NewVideoFile_ShouldCreateExpectedState()
    {
        // Act
        var file = TrackedFileObjectMother.NewVideoFile("Custom.Show.S01E01.mkv");

        // Assert
        file.FileName.Should().Be("Custom.Show.S01E01.mkv");
        file.Status.Should().Be(MediaButler.Core.Enums.FileStatus.New);
        file.OriginalPath.Should().Contain("Custom.Show.S01E01.mkv");
    }

    [Fact]
    public void TrackedFileObjectMother_ClassifiedFile_ShouldHaveCorrectClassification()
    {
        // Act
        var file = TrackedFileObjectMother.ClassifiedFile("CUSTOM SERIES", 0.75m);

        // Assert
        file.Status.Should().Be(MediaButler.Core.Enums.FileStatus.Classified);
        file.SuggestedCategory.Should().Be("CUSTOM SERIES");
        file.Confidence.Should().Be(0.75m);
    }

    [Fact]
    public void ConfigurationSettingBuilder_AsPath_ShouldCreatePathSetting()
    {
        // Act
        var setting = new ConfigurationSettingBuilder()
            .AsPath("TestPath", "/test/path")
            .Build();

        // Assert
        setting.Key.Should().Be("MediaButler.Paths.TestPath");
        setting.Value.Should().Be("/test/path");
        setting.Description.Should().Contain("path");
        setting.RequiresRestart.Should().BeTrue();
    }

    [Fact]
    public void ConfigurationObjectMother_DefaultPathSettings_ShouldReturnAllRequiredPaths()
    {
        // Act
        var settings = ConfigurationObjectMother.DefaultPathSettings().ToList();

        // Assert
        settings.Should().HaveCountGreaterThan(3);
        settings.Should().Contain(s => s.Key.Contains("WatchFolder"));
        settings.Should().Contain(s => s.Key.Contains("MediaLibrary"));
        settings.Should().Contain(s => s.Key.Contains("PendingReview"));
    }

    [Fact]
    public void TestBase_TestDateTime_ShouldBeDeterministic()
    {
        // Act
        var dateTime1 = TestDateTime();
        var dateTime2 = TestDateTime();

        // Assert
        dateTime1.Should().Be(dateTime2);
        dateTime1.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void TestBase_TestHash_ShouldGenerateValidHash()
    {
        // Act
        var hash = TestHash("999");

        // Assert
        hash.Should().HaveLength(64); // SHA256 length
        hash.Should().EndWith("999");
        hash.Should().MatchRegex("^[a-f0-9]+$"); // Hexadecimal only
        
        // Verify the default case too
        var defaultHash = TestHash();
        defaultHash.Should().HaveLength(64);
        defaultHash.Should().EndWith("001");
    }
}