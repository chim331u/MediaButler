namespace MediaButler.ML.Models;

/// <summary>
/// Represents episode information extracted from a filename.
/// This is a value object containing season/episode identification data.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable data structure
/// - Single responsibility: Only holds episode identification data
/// - No complecting: Pure data without behavior
/// </remarks>
public record EpisodeInfo
{
    /// <summary>
    /// Gets the season number if identified.
    /// </summary>
    public int? Season { get; init; }

    /// <summary>
    /// Gets the episode number if identified.
    /// </summary>
    public int? Episode { get; init; }

    /// <summary>
    /// Gets the raw episode pattern that was matched.
    /// </summary>
    /// <example>"S01E01", "Season 1 Episode 1", "1x01"</example>
    public string RawPattern { get; init; } = string.Empty;

    /// <summary>
    /// Gets the pattern type that was used to identify the episode.
    /// </summary>
    public EpisodePatternType PatternType { get; init; }

    /// <summary>
    /// Gets additional episode information if available.
    /// </summary>
    /// <example>Episode title, part numbers, etc.</example>
    public string? AdditionalInfo { get; init; }

    /// <summary>
    /// Indicates whether this represents a valid episode identification.
    /// </summary>
    public bool IsValid => Season.HasValue && Episode.HasValue && Season > 0 && Episode > 0;

    /// <summary>
    /// Gets a normalized string representation of the episode.
    /// </summary>
    /// <returns>Normalized episode string like "S01E01" or empty if invalid</returns>
    public string ToNormalizedString()
    {
        if (!IsValid) return string.Empty;
        return $"S{Season:D2}E{Episode:D2}";
    }
}

/// <summary>
/// Represents the type of pattern used to identify episode information.
/// </summary>
public enum EpisodePatternType
{
    /// <summary>
    /// No pattern was identified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Standard pattern: S##E## (e.g., S01E01)
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Alternative pattern: ##x## (e.g., 1x01)
    /// </summary>
    Alternative = 2,

    /// <summary>
    /// Verbose pattern: Season # Episode # (e.g., Season 1 Episode 1)
    /// </summary>
    Verbose = 3,

    /// <summary>
    /// Episode only pattern: E## or Ep## (e.g., E01, Ep01)
    /// </summary>
    EpisodeOnly = 4,

    /// <summary>
    /// Date-based pattern: YYYY.MM.DD (e.g., 2023.12.25)
    /// </summary>
    DateBased = 5
}