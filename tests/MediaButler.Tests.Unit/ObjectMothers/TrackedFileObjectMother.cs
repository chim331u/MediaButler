using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Tests.Unit.Builders;

namespace MediaButler.Tests.Unit.ObjectMothers;

/// <summary>
/// Object Mother for creating common TrackedFile test scenarios.
/// Provides pre-configured instances for typical business scenarios.
/// Complements the TrackedFileBuilder with ready-made common cases.
/// </summary>
public static class TrackedFileObjectMother
{
    /// <summary>
    /// Creates a newly discovered video file.
    /// Scenario: File just added to the system, needs classification.
    /// </summary>
    public static TrackedFile NewVideoFile(string fileName = "Breaking.Bad.S01E01.mkv")
    {
        return new TrackedFileBuilder()
            .WithFileName(fileName)
            .WithOriginalPath($"/downloads/{fileName}")
            .WithFileSize(734003200) // ~700MB
            .WithStatus(FileStatus.New)
            .Build();
    }

    /// <summary>
    /// Creates a file pending user confirmation.
    /// Scenario: ML classified file with high confidence, needs user approval.
    /// </summary>
    public static TrackedFile ClassifiedFile(string series = "BREAKING BAD", decimal confidence = 0.85m)
    {
        return new TrackedFileBuilder()
            .WithFileName($"{series.Replace(" ", ".")}.S01E01.mkv")
            .WithOriginalPath($"/downloads/{series.Replace(" ", ".")}.S01E01.mkv")
            .AsClassified(series, confidence)
            .Build();
    }

    /// <summary>
    /// Creates a confirmed file ready for moving.
    /// Scenario: User confirmed the category, ready for physical move.
    /// </summary>
    public static TrackedFile ConfirmedFile(string series = "BREAKING BAD")
    {
        return new TrackedFileBuilder()
            .WithFileName($"{series.Replace(" ", ".")}.S01E01.mkv")
            .AsConfirmed(series)
            .Build();
    }

    /// <summary>
    /// Creates a successfully moved file.
    /// Scenario: Complete workflow success - file in final location.
    /// </summary>
    public static TrackedFile MovedFile(string series = "BREAKING BAD")
    {
        var fileName = $"{series.Replace(" ", ".")}.S01E01.mkv";
        var targetPath = $"/media/TV/{series}/{fileName}";
        
        return new TrackedFileBuilder()
            .WithFileName(fileName)
            .AsMoved(series, targetPath)
            .Build();
    }

    /// <summary>
    /// Creates a file with processing error.
    /// Scenario: Something went wrong during processing, needs retry.
    /// </summary>
    public static TrackedFile ErrorFile(string error = "File not found during move operation")
    {
        return new TrackedFileBuilder()
            .WithFileName("Problem.File.S01E01.mkv")
            .AsError(error, retryCount: 2)
            .Build();
    }

    /// <summary>
    /// Creates a low confidence classification file.
    /// Scenario: ML not confident, likely needs manual categorization.
    /// </summary>
    public static TrackedFile LowConfidenceFile(decimal confidence = 0.3m)
    {
        return new TrackedFileBuilder()
            .WithFileName("Unknown.Series.S01E01.mkv")
            .AsClassified("UNKNOWN SERIES", confidence)
            .Build();
    }

    /// <summary>
    /// Creates a large file for performance testing.
    /// Scenario: Testing with large file sizes (4GB+).
    /// </summary>
    public static TrackedFile LargeFile()
    {
        return new TrackedFileBuilder()
            .WithFileName("Large.File.4K.S01E01.mkv")
            .WithFileSize(4_000_000_000) // ~4GB
            .WithStatus(FileStatus.New)
            .Build();
    }

    /// <summary>
    /// Creates a file with special characters in name.
    /// Scenario: Testing filename sanitization and edge cases.
    /// </summary>
    public static TrackedFile SpecialCharacterFile()
    {
        return new TrackedFileBuilder()
            .WithFileName("Strange: Name [2024] & \"Quotes\" (1080p).mkv")
            .WithOriginalPath("/downloads/Strange: Name [2024] & \"Quotes\" (1080p).mkv")
            .WithStatus(FileStatus.New)
            .Build();
    }

    /// <summary>
    /// Creates a subtitle file (should not be tracked independently).
    /// Scenario: Testing that related files are handled correctly.
    /// </summary>
    public static TrackedFile SubtitleFile()
    {
        return new TrackedFileBuilder()
            .WithFileName("Breaking.Bad.S01E01.srt")
            .WithOriginalPath("/downloads/Breaking.Bad.S01E01.srt")
            .WithFileSize(50_000) // ~50KB
            .WithStatus(FileStatus.New)
            .Build();
    }

    /// <summary>
    /// Creates multiple files for the same series.
    /// Scenario: Testing batch processing and series consistency.
    /// </summary>
    public static IEnumerable<TrackedFile> SeriesFiles(string series = "BREAKING BAD", int episodeCount = 3)
    {
        for (int i = 1; i <= episodeCount; i++)
        {
            var fileName = $"{series.Replace(" ", ".")}.S01E{i:D2}.mkv";
            yield return new TrackedFileBuilder()
                .WithFileName(fileName)
                .WithOriginalPath($"/downloads/{fileName}")
                .WithHash($"hash{i:D2}" + new string('0', 56)) // Unique hash per file
                .AsClassified(series, 0.9m)
                .Build();
        }
    }

    /// <summary>
    /// Creates files in different processing states.
    /// Scenario: Testing workflow state management and transitions.
    /// </summary>
    public static IEnumerable<TrackedFile> MixedStateFiles()
    {
        yield return NewVideoFile("New.Show.S01E01.mkv");
        yield return ClassifiedFile("PENDING SHOW", 0.8m);
        yield return ConfirmedFile("CONFIRMED SHOW");
        yield return MovedFile("MOVED SHOW");
        yield return ErrorFile("Move operation failed");
    }
}