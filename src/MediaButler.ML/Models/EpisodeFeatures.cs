namespace MediaButler.ML.Models;

/// <summary>
/// Episode-specific features for TV show classification and organization.
/// These features help distinguish between different TV series and episode types.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable episode feature data
/// - Single responsibility: Only holds episode-related features
/// - Declarative: Clear episode characteristics without processing logic
/// </remarks>
public sealed record EpisodeFeatures
{
    /// <summary>
    /// Season number if available.
    /// </summary>
    public int? SeasonNumber { get; init; }

    /// <summary>
    /// Episode number if available.
    /// </summary>
    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// Type of episode pattern detected.
    /// </summary>
    public required EpisodePatternType PatternType { get; init; }

    /// <summary>
    /// Whether this appears to be a multi-part episode.
    /// </summary>
    public required bool IsMultiPart { get; init; }

    /// <summary>
    /// Whether this appears to be a special episode (pilot, finale, etc.).
    /// </summary>
    public required bool IsSpecialEpisode { get; init; }

    /// <summary>
    /// Episode title if extractable from filename.
    /// </summary>
    public string? EpisodeTitle { get; init; }

    /// <summary>
    /// Confidence in episode information extraction (0-1).
    /// </summary>
    public required double ExtractionConfidence { get; init; }

    /// <summary>
    /// Whether episode follows standard numbering pattern.
    /// </summary>
    public required bool HasStandardNumbering { get; init; }

    /// <summary>
    /// Whether this is likely from a long-running series (episode > 100).
    /// </summary>
    public bool IsLongRunningSeries => EpisodeNumber > 100;

    /// <summary>
    /// Estimated series maturity based on season/episode numbers.
    /// </summary>
    public SeriesMaturity EstimatedMaturity => CalculateMaturity();

    /// <summary>
    /// Number of features this contributes to ML model.
    /// </summary>
    public int FeatureCount => 12;

    /// <summary>
    /// Converts episode features to ML feature array.
    /// </summary>
    /// <returns>Feature array representing episode characteristics</returns>
    public float[] ToFeatureArray()
    {
        return new[]
        {
            // Numeric features (normalized)
            SeasonNumber.HasValue ? Math.Min(SeasonNumber.Value / 20f, 1f) : 0f, // normalize to reasonable range
            EpisodeNumber.HasValue ? Math.Min(EpisodeNumber.Value / 200f, 1f) : 0f, // normalize for long series
            
            // Categorical features
            (float)PatternType,
            (float)EstimatedMaturity,
            
            // Binary features
            IsMultiPart ? 1f : 0f,
            IsSpecialEpisode ? 1f : 0f,
            HasStandardNumbering ? 1f : 0f,
            IsLongRunningSeries ? 1f : 0f,
            !string.IsNullOrEmpty(EpisodeTitle) ? 1f : 0f,
            
            // Confidence and pattern features
            (float)ExtractionConfidence,
            SeasonNumber.HasValue ? 1f : 0f,
            EpisodeNumber.HasValue ? 1f : 0f
        };
    }

    /// <summary>
    /// Gets feature names for episode features.
    /// </summary>
    /// <returns>Feature names corresponding to ToFeatureArray()</returns>
    public IReadOnlyList<string> GetFeatureNames()
    {
        return new[]
        {
            "Episode_SeasonNumber",
            "Episode_EpisodeNumber",
            "Episode_PatternType",
            "Episode_EstimatedMaturity",
            "Episode_IsMultiPart",
            "Episode_IsSpecialEpisode",
            "Episode_HasStandardNumbering",
            "Episode_IsLongRunningSeries",
            "Episode_HasTitle",
            "Episode_ExtractionConfidence",
            "Episode_HasSeasonNumber",
            "Episode_HasEpisodeNumber"
        }.AsReadOnly();
    }

    /// <summary>
    /// Creates episode features from EpisodeInfo tokenization result.
    /// </summary>
    /// <param name="episodeInfo">Episode info from tokenization</param>
    /// <returns>Episode features for ML</returns>
    public static EpisodeFeatures FromEpisodeInfo(EpisodeInfo episodeInfo)
    {
        return new EpisodeFeatures
        {
            SeasonNumber = episodeInfo.Season,
            EpisodeNumber = episodeInfo.Episode,
            PatternType = episodeInfo.PatternType,
            IsMultiPart = DetectMultiPart(episodeInfo.RawPattern),
            IsSpecialEpisode = DetectSpecialEpisode(episodeInfo.AdditionalInfo),
            EpisodeTitle = episodeInfo.AdditionalInfo,
            ExtractionConfidence = CalculateExtractionConfidence(episodeInfo),
            HasStandardNumbering = HasStandardPattern(episodeInfo.PatternType)
        };
    }

    private SeriesMaturity CalculateMaturity()
    {
        var seasonCount = SeasonNumber ?? 1;
        var episodeCount = EpisodeNumber ?? 1;

        // Long-running anime/series patterns
        if (episodeCount > 500) return SeriesMaturity.VeryLongRunning;
        if (episodeCount > 100) return SeriesMaturity.LongRunning;
        
        // Traditional TV series patterns
        if (seasonCount > 10) return SeriesMaturity.Established;
        if (seasonCount > 5) return SeriesMaturity.Mature;
        if (seasonCount > 2) return SeriesMaturity.Developing;
        
        return SeriesMaturity.New;
    }

    private static bool DetectMultiPart(string rawPattern)
    {
        if (string.IsNullOrEmpty(rawPattern))
            return false;

        // Look for multi-part indicators
        var multiPartPatterns = new[] { "-", "pt", "part", "parte" };
        return multiPartPatterns.Any(pattern => 
            rawPattern.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool DetectSpecialEpisode(string? additionalInfo)
    {
        if (string.IsNullOrEmpty(additionalInfo))
            return false;

        var specialKeywords = new[] 
        { 
            "pilot", "finale", "special", "ova", "movie", "film", 
            "recap", "summary", "extra", "bonus", "director" 
        };
        
        return specialKeywords.Any(keyword => 
            additionalInfo.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static double CalculateExtractionConfidence(EpisodeInfo episodeInfo)
    {
        var confidence = 0.5; // base confidence

        // Higher confidence for standard patterns
        if (episodeInfo.PatternType == EpisodePatternType.Standard)
            confidence += 0.3;
        else if (episodeInfo.PatternType == EpisodePatternType.EpisodeOnly)
            confidence += 0.2;

        // Bonus for having both season and episode
        if (episodeInfo.Season.HasValue && episodeInfo.Episode.HasValue)
            confidence += 0.2;

        // Penalty for date-based episodes (less reliable)
        if (episodeInfo.PatternType == EpisodePatternType.DateBased)
            confidence -= 0.2;

        return Math.Max(0.0, Math.Min(1.0, confidence));
    }

    private static bool HasStandardPattern(EpisodePatternType patternType)
    {
        return patternType is EpisodePatternType.Standard or 
                             EpisodePatternType.Alternative or
                             EpisodePatternType.EpisodeOnly;
    }
}

/// <summary>
/// Series maturity levels based on episode/season patterns.
/// </summary>
public enum SeriesMaturity
{
    New = 0,           // 1-2 seasons, few episodes
    Developing = 1,    // 3-5 seasons
    Mature = 2,        // 6-10 seasons  
    Established = 3,   // 10+ seasons
    LongRunning = 4,   // 100+ episodes (anime style)
    VeryLongRunning = 5 // 500+ episodes (soap operas, long anime)
}