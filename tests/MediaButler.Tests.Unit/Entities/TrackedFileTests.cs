using System;
using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Tests.Unit.Infrastructure;
using Xunit;

namespace MediaButler.Tests.Unit.Entities;

/// <summary>
/// Unit tests for TrackedFile domain entity.
/// Tests domain behavior, state transitions, and business rules.
/// Follows "Simple Made Easy" principles - testing behavior, not implementation.
/// </summary>
public class TrackedFileTests : TestBase
{
    [Fact]
    public void TrackedFile_WithRequiredProperties_ShouldCreateSuccessfully()
    {
        // Arrange
        var hash = TestHash("001");
        var fileName = "Breaking.Bad.S01E01.mkv";
        var originalPath = TestFilePath(fileName);
        
        // Act
        var file = new TrackedFile
        {
            Hash = hash,
            FileName = fileName,
            OriginalPath = originalPath,
            FileSize = 734003200
        };
        
        // Assert
        file.Hash.Should().Be(hash);
        file.FileName.Should().Be(fileName);
        file.OriginalPath.Should().Be(originalPath);
        file.FileSize.Should().Be(734003200);
        file.Status.Should().Be(FileStatus.New); // Default status
        file.RetryCount.Should().Be(0); // Default retry count
        file.Confidence.Should().Be(0m); // Default confidence
    }

    [Fact]
    public void MarkAsClassified_WithValidParameters_ShouldUpdateClassificationFields()
    {
        // Arrange
        var file = CreateTestFile();
        var suggestedCategory = "BREAKING BAD";
        var confidence = 0.85m;
        var beforeClassification = DateTime.UtcNow.AddSeconds(-1);
        
        // Act
        file.MarkAsClassified(suggestedCategory, confidence);
        
        // Assert
        file.SuggestedCategory.Should().Be(suggestedCategory);
        file.Confidence.Should().Be(confidence);
        file.Status.Should().Be(FileStatus.Classified);
        file.ClassifiedAt.Should().BeAfter(beforeClassification);
        file.ClassifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void MarkAsClassified_WithInvalidConfidence_ShouldThrowArgumentException(decimal invalidConfidence)
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            file.MarkAsClassified("VALID CATEGORY", invalidConfidence));
        
        exception.ParamName.Should().Be("confidence");
        exception.Message.Should().Contain("Confidence must be between 0.0 and 1.0");
    }

    [Fact]
    public void MarkAsClassified_WithNullCategory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            file.MarkAsClassified(null!, 0.8m));
        
        exception.ParamName.Should().Be("suggestedCategory");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void MarkAsClassified_WithValidConfidenceRange_ShouldSucceed(decimal validConfidence)
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act
        file.MarkAsClassified("TEST SERIES", validConfidence);
        
        // Assert
        file.Confidence.Should().Be(validConfidence);
        file.Status.Should().Be(FileStatus.Classified);
    }

    [Fact]
    public void ConfirmCategory_WithValidParameters_ShouldUpdateCategoryFields()
    {
        // Arrange
        var file = CreateTestFile();
        file.MarkAsClassified("SUGGESTED SERIES", 0.8m);
        
        var confirmedCategory = "CONFIRMED SERIES";
        var targetPath = TestLibraryPath(confirmedCategory, file.FileName);
        
        // Act
        file.ConfirmCategory(confirmedCategory, targetPath);
        
        // Assert
        file.Category.Should().Be(confirmedCategory);
        file.TargetPath.Should().Be(targetPath);
        file.Status.Should().Be(FileStatus.ReadyToMove);
    }

    [Fact]
    public void ConfirmCategory_WithNullCategory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            file.ConfirmCategory(null!, "/valid/path"));
        
        exception.ParamName.Should().Be("confirmedCategory");
    }

    [Fact]
    public void ConfirmCategory_WithNullTargetPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            file.ConfirmCategory("VALID CATEGORY", null!));
        
        exception.ParamName.Should().Be("targetPath");
    }

    [Fact]
    public void MarkAsMoved_WithValidPath_ShouldUpdateMovedFields()
    {
        // Arrange
        var file = CreateTestFile();
        file.ConfirmCategory("TEST SERIES", "/temp/target/path");
        
        var finalPath = "/final/moved/path/Breaking.Bad.S01E01.mkv";
        var beforeMove = DateTime.UtcNow.AddSeconds(-1);
        
        // Act
        file.MarkAsMoved(finalPath);
        
        // Assert
        file.MovedToPath.Should().Be(finalPath);
        file.TargetPath.Should().Be("/temp/target/path"); // TargetPath should remain unchanged
        file.MovedAt.Should().BeAfter(beforeMove);
        file.MovedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        file.Status.Should().Be(FileStatus.Moved);
    }

    [Fact]
    public void MarkAsMoved_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            file.MarkAsMoved(null!));
        
        exception.ParamName.Should().Be("finalPath");
    }

    [Fact]
    public void RecordError_WithValidError_ShouldUpdateErrorFields()
    {
        // Arrange
        var file = CreateTestFile();
        var errorMessage = "File not found during move operation";
        var beforeError = DateTime.UtcNow.AddSeconds(-1);
        var initialRetryCount = file.RetryCount;
        
        // Act
        file.RecordError(errorMessage);
        
        // Assert
        file.LastError.Should().Be(errorMessage);
        file.LastErrorAt.Should().BeAfter(beforeError);
        file.LastErrorAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        file.RetryCount.Should().Be(initialRetryCount + 1);
        file.Status.Should().Be(FileStatus.Retry);
    }

    [Fact]
    public void RecordError_WithShouldRetryFalse_ShouldSetErrorStatus()
    {
        // Arrange
        var file = CreateTestFile();
        var errorMessage = "Permanent error - do not retry";
        
        // Act
        file.RecordError(errorMessage, shouldRetry: false);
        
        // Assert
        file.LastError.Should().Be(errorMessage);
        file.Status.Should().Be(FileStatus.Error);
        file.RetryCount.Should().Be(1); // Still increments retry count for tracking
    }

    [Fact]
    public void RecordError_WithNullErrorMessage_ShouldThrowArgumentNullException()
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            file.RecordError(null!));
        
        exception.ParamName.Should().Be("errorMessage");
    }

    [Fact]
    public void RecordError_MultipleCalls_ShouldIncrementRetryCount()
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act
        file.RecordError("First error");
        file.RecordError("Second error");
        file.RecordError("Third error");
        
        // Assert
        file.RetryCount.Should().Be(3);
        file.LastError.Should().Be("Third error"); // Should show latest error
    }

    [Fact]
    public void FileWorkflow_CompleteSuccessPath_ShouldTransitionCorrectly()
    {
        // Arrange
        var file = CreateTestFile();
        
        // Act & Assert - New file
        file.Status.Should().Be(FileStatus.New);
        
        // Act & Assert - Classify
        file.MarkAsClassified("BREAKING BAD", 0.9m);
        file.Status.Should().Be(FileStatus.Classified);
        file.ClassifiedAt.Should().NotBeNull();
        
        // Act & Assert - Confirm
        file.ConfirmCategory("BREAKING BAD", "/library/BREAKING BAD/Breaking.Bad.S01E01.mkv");
        file.Status.Should().Be(FileStatus.ReadyToMove);
        file.Category.Should().Be("BREAKING BAD");
        
        // Act & Assert - Move
        file.MarkAsMoved("/library/BREAKING BAD/Breaking.Bad.S01E01.mkv");
        file.Status.Should().Be(FileStatus.Moved);
        file.MovedAt.Should().NotBeNull();
    }

    [Fact]
    public void FileWorkflow_WithErrorRecovery_ShouldHandleRetries()
    {
        // Arrange
        var file = CreateTestFile();
        file.MarkAsClassified("TEST SERIES", 0.8m);
        file.ConfirmCategory("TEST SERIES", "/target/path");
        
        // Act & Assert - Error during move
        file.RecordError("Network timeout during move");
        file.Status.Should().Be(FileStatus.Retry);
        file.RetryCount.Should().Be(1);
        
        // Act & Assert - Retry success
        file.MarkAsMoved("/final/path");
        file.Status.Should().Be(FileStatus.Moved);
        // Error fields remain for audit trail
        file.LastError.Should().NotBeNull();
        file.RetryCount.Should().Be(1);
    }

    /// <summary>
    /// Creates a test TrackedFile with required properties set.
    /// </summary>
    private static TrackedFile CreateTestFile(string suffix = "001")
    {
        return new TrackedFile
        {
            Hash = TestHash(suffix),
            FileName = "Breaking.Bad.S01E01.mkv",
            OriginalPath = TestFilePath("Breaking.Bad.S01E01.mkv"),
            FileSize = 734003200
        };
    }
}