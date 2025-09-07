namespace MediaButler.ML.Models;

/// <summary>
/// Represents quality and technical information extracted from a filename.
/// This is a value object containing video/audio quality indicators.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable data structure
/// - Single responsibility: Only holds quality/technical data
/// - No complecting: Pure data without behavior
/// </remarks>
public record QualityInfo
{
    /// <summary>
    /// Gets the video resolution if identified.
    /// </summary>
    /// <example>"1080p", "720p", "4K", "2160p"</example>
    public string? Resolution { get; init; }

    /// <summary>
    /// Gets the video codec if identified.
    /// </summary>
    /// <example>"x264", "x265", "HEVC", "AV1"</example>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Gets the audio codec if identified.
    /// </summary>
    /// <example>"AAC", "AC3", "DTS", "FLAC"</example>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// Gets the source type if identified.
    /// </summary>
    /// <example>"BluRay", "WEBRip", "HDTV", "DVD"</example>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the video quality tier based on resolution and source.
    /// </summary>
    public QualityTier QualityTier { get; init; }

    /// <summary>
    /// Gets additional quality indicators found in the filename.
    /// </summary>
    /// <example>HDR, DV (Dolby Vision), IMAX, etc.</example>
    public IReadOnlyList<string> AdditionalIndicators { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets language codes found in the filename.
    /// </summary>
    /// <example>["ENG", "ITA", "FRA"]</example>
    public IReadOnlyList<string> LanguageCodes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets subtitle indicators if present.
    /// </summary>
    /// <example>["SUB", "DUB", "CC"]</example>
    public IReadOnlyList<string> SubtitleIndicators { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Indicates whether this represents valid quality information.
    /// </summary>
    public bool HasQualityInfo => !string.IsNullOrEmpty(Resolution) || 
                                  !string.IsNullOrEmpty(VideoCodec) || 
                                  !string.IsNullOrEmpty(Source);

    /// <summary>
    /// Gets a display-friendly string representation of the quality information.
    /// </summary>
    /// <returns>Quality string like "1080p BluRay x264" or "Unknown Quality" if no info</returns>
    public string ToDisplayString()
    {
        if (!HasQualityInfo) return "Unknown Quality";
        
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Resolution)) parts.Add(Resolution);
        if (!string.IsNullOrEmpty(Source)) parts.Add(Source);
        if (!string.IsNullOrEmpty(VideoCodec)) parts.Add(VideoCodec);
        
        return string.Join(" ", parts);
    }
}

/// <summary>
/// Represents the overall quality tier of a media file.
/// </summary>
public enum QualityTier
{
    /// <summary>
    /// Quality tier could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Low quality (480p or lower, poor source).
    /// </summary>
    Low = 1,

    /// <summary>
    /// Standard quality (720p, good source).
    /// </summary>
    Standard = 2,

    /// <summary>
    /// High quality (1080p, excellent source).
    /// </summary>
    High = 3,

    /// <summary>
    /// Ultra-high quality (4K/2160p, premium source).
    /// </summary>
    UltraHigh = 4,

    /// <summary>
    /// Premium quality (4K+ with HDR, Dolby Vision, etc.).
    /// </summary>
    Premium = 5
}