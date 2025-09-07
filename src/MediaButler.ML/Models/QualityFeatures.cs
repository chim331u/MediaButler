namespace MediaButler.ML.Models;

/// <summary>
/// Quality-based features extracted from video quality indicators in filenames.
/// These features help classify content based on technical characteristics.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable quality feature representation  
/// - Single responsibility: Only holds quality-related feature data
/// - Declarative: Clear quality characteristics without processing logic
/// </remarks>
public sealed record QualityFeatures
{
    /// <summary>
    /// Resolution tier as a categorical feature.
    /// </summary>
    public required QualityTier ResolutionTier { get; init; }

    /// <summary>
    /// Source quality tier (BluRay > Web > HDTV > DVD).
    /// </summary>
    public required QualityTier SourceTier { get; init; }

    /// <summary>
    /// Detected video codec if present.
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Detected audio codec if present.
    /// </summary>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// Raw resolution string (1080p, 720p, etc.).
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// Raw source string (WEB-DL, BluRay, etc.).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Whether the file indicates HDR content.
    /// </summary>
    public required bool HasHDR { get; init; }

    /// <summary>
    /// Whether the file indicates multiple audio tracks.
    /// </summary>
    public required bool HasMultipleAudio { get; init; }

    /// <summary>
    /// Estimated quality score (0-100) based on all quality indicators.
    /// </summary>
    public required int QualityScore { get; init; }

    /// <summary>
    /// Indicates if this is likely a high-quality release.
    /// </summary>
    public bool IsHighQuality => QualityScore >= 75;

    /// <summary>
    /// Indicates if this is likely a low-quality release.
    /// </summary>
    public bool IsLowQuality => QualityScore <= 25;

    /// <summary>
    /// Number of features this contributes to the ML model.
    /// </summary>
    public int FeatureCount => 12; // Various quality indicators as features

    /// <summary>
    /// Converts quality features to ML feature array.
    /// </summary>
    /// <returns>Feature array representing quality characteristics</returns>
    public float[] ToFeatureArray()
    {
        return new[]
        {
            // Categorical features (enum ordinals)
            (float)ResolutionTier,
            (float)SourceTier,
            
            // Binary features
            HasHDR ? 1f : 0f,
            HasMultipleAudio ? 1f : 0f,
            IsHighQuality ? 1f : 0f,
            IsLowQuality ? 1f : 0f,
            
            // Numeric features
            QualityScore / 100f, // normalized 0-1
            
            // Codec presence indicators
            !string.IsNullOrEmpty(VideoCodec) ? 1f : 0f,
            !string.IsNullOrEmpty(AudioCodec) ? 1f : 0f,
            
            // Specific codec features (common ones)
            VideoCodec?.Equals("x264", StringComparison.OrdinalIgnoreCase) == true ? 1f : 0f,
            VideoCodec?.Equals("x265", StringComparison.OrdinalIgnoreCase) == true ? 1f : 0f,
            VideoCodec?.Equals("HEVC", StringComparison.OrdinalIgnoreCase) == true ? 1f : 0f
        };
    }

    /// <summary>
    /// Gets feature names for quality features.
    /// </summary>
    /// <returns>Feature names corresponding to ToFeatureArray()</returns>
    public IReadOnlyList<string> GetFeatureNames()
    {
        return new[]
        {
            "Quality_ResolutionTier",
            "Quality_SourceTier", 
            "Quality_HasHDR",
            "Quality_HasMultipleAudio",
            "Quality_IsHighQuality",
            "Quality_IsLowQuality",
            "Quality_QualityScore",
            "Quality_HasVideoCodec",
            "Quality_HasAudioCodec",
            "Quality_IsX264",
            "Quality_IsX265", 
            "Quality_IsHEVC"
        }.AsReadOnly();
    }

    /// <summary>
    /// Creates quality features from QualityInfo tokenization result.
    /// </summary>
    /// <param name="qualityInfo">Quality info from tokenization</param>
    /// <returns>Quality features for ML</returns>
    public static QualityFeatures FromQualityInfo(QualityInfo qualityInfo)
    {
        var qualityScore = CalculateQualityScore(qualityInfo);
        
        return new QualityFeatures
        {
            ResolutionTier = qualityInfo.QualityTier,
            SourceTier = DetermineSourceTier(qualityInfo.Source),
            VideoCodec = qualityInfo.VideoCodec,
            AudioCodec = qualityInfo.AudioCodec,
            Resolution = qualityInfo.Resolution,
            Source = qualityInfo.Source,
            HasHDR = DetectHDR(qualityInfo.Source, qualityInfo.VideoCodec),
            HasMultipleAudio = DetectMultipleAudio(qualityInfo.Source),
            QualityScore = qualityScore
        };
    }

    private static int CalculateQualityScore(QualityInfo qualityInfo)
    {
        var score = 0;

        // Resolution scoring
        score += qualityInfo.QualityTier switch
        {
            QualityTier.Premium => 40,    // 4K/2160p
            QualityTier.UltraHigh => 35,  // 1080p BluRay
            QualityTier.High => 30,       // 1080p Web
            QualityTier.Standard => 20,   // 720p
            QualityTier.Low => 10,        // 480p
            _ => 0
        };

        // Source scoring
        if (qualityInfo.Source != null)
        {
            score += qualityInfo.Source.ToUpper() switch
            {
                var s when s.Contains("BLURAY") || s.Contains("BDRIP") => 35,
                var s when s.Contains("WEB") => 25,
                var s when s.Contains("HDTV") => 20,
                var s when s.Contains("DVD") => 15,
                _ => 10
            };
        }

        // Codec scoring
        if (qualityInfo.VideoCodec != null)
        {
            score += qualityInfo.VideoCodec.ToUpper() switch
            {
                "HEVC" or "H265" or "X265" => 25,
                "AVC" or "H264" or "X264" => 20,
                _ => 10
            };
        }

        return Math.Min(score, 100); // Cap at 100
    }

    private static QualityTier DetermineSourceTier(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return QualityTier.Unknown;

        return source.ToUpper() switch
        {
            var s when s.Contains("BLURAY") || s.Contains("BDRIP") => QualityTier.UltraHigh,
            var s when s.Contains("WEB") => QualityTier.High,
            var s when s.Contains("HDTV") => QualityTier.Standard,
            var s when s.Contains("DVD") => QualityTier.Low,
            _ => QualityTier.Unknown
        };
    }

    private static bool DetectHDR(string? source, string? codec)
    {
        var indicators = new[] { source, codec };
        return indicators.Any(i => i?.Contains("HDR", StringComparison.OrdinalIgnoreCase) == true ||
                                   i?.Contains("DV", StringComparison.OrdinalIgnoreCase) == true ||
                                   i?.Contains("DOLBY", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool DetectMultipleAudio(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return false;

        return source.Contains("MULTI", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("DUAL", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("TrueHD", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("DTS", StringComparison.OrdinalIgnoreCase);
    }
}