namespace MediaButler.ML.Models;

/// <summary>
/// Represents a filename broken down into meaningful components for ML feature extraction.
/// This is a value object that contains no behavior, only data.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable data structure
/// - Single responsibility: Only holds tokenization results
/// - No complecting: Pure data without behavior
/// </remarks>
public record TokenizedFilename
{
    /// <summary>
    /// Gets the original filename that was tokenized.
    /// </summary>
    public string OriginalFilename { get; init; } = string.Empty;

    /// <summary>
    /// Gets the series name tokens extracted from the filename.
    /// </summary>
    /// <example>
    /// For "The.Walking.Dead.S01E01.mkv" this would be ["the", "walking", "dead"]
    /// </example>
    public IReadOnlyList<string> SeriesTokens { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets all individual tokens found in the filename.
    /// </summary>
    public IReadOnlyList<string> AllTokens { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets tokens that were filtered out (quality indicators, language codes, etc.).
    /// </summary>
    public IReadOnlyList<string> FilteredTokens { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the file extension without the dot.
    /// </summary>
    /// <example>"mkv", "mp4", "avi"</example>
    public string FileExtension { get; init; } = string.Empty;

    /// <summary>
    /// Gets the episode information if found in the filename.
    /// </summary>
    public EpisodeInfo? EpisodeInfo { get; init; }

    /// <summary>
    /// Gets the quality information if found in the filename.
    /// </summary>
    public QualityInfo? QualityInfo { get; init; }

    /// <summary>
    /// Gets the release group information if found in the filename.
    /// </summary>
    public string? ReleaseGroup { get; init; }

    /// <summary>
    /// Gets additional metadata found in the filename.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Gets the timestamp when this tokenization was performed.
    /// </summary>
    public DateTime TokenizedAt { get; init; } = DateTime.UtcNow;
}