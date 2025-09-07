namespace MediaButler.ML.Models;

/// <summary>
/// Comprehensive feature vector representing all extracted ML features from a filename.
/// This is an immutable value object containing all features needed for classification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable data structure with no behavior
/// - Single responsibility: Only holds feature data, no processing logic
/// - Declarative: Clear data structure without implementation details
/// </remarks>
public sealed record FeatureVector
{
    /// <summary>
    /// Original filename that features were extracted from.
    /// </summary>
    public required string OriginalFilename { get; init; }

    /// <summary>
    /// Token-based features including frequency analysis.
    /// </summary>
    public required TokenFrequencyAnalysis TokenFeatures { get; init; }

    /// <summary>
    /// N-gram features for contextual information.
    /// </summary>
    public required IReadOnlyList<NGramFeature> NGramFeatures { get; init; }

    /// <summary>
    /// Quality-based features from video quality indicators.
    /// </summary>
    public required QualityFeatures QualityFeatures { get; init; }

    /// <summary>
    /// Pattern matching features from filename structure.
    /// </summary>
    public required PatternMatchingFeatures PatternFeatures { get; init; }

    /// <summary>
    /// Episode-related features if episode information is available.
    /// </summary>
    public EpisodeFeatures? EpisodeFeatures { get; init; }

    /// <summary>
    /// Release group features if release group is identified.
    /// </summary>
    public ReleaseGroupFeatures? ReleaseGroupFeatures { get; init; }

    /// <summary>
    /// Total number of features in this vector (for ML model compatibility).
    /// </summary>
    public int FeatureCount => CalculateFeatureCount();

    /// <summary>
    /// Feature extraction timestamp for debugging and analysis.
    /// </summary>
    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Converts feature vector to flat array suitable for ML.NET models.
    /// </summary>
    /// <returns>Flattened feature array</returns>
    public float[] ToFeatureArray()
    {
        var features = new List<float>();

        // Token frequency features
        features.AddRange(TokenFeatures.ToFeatureArray());

        // N-gram features  
        features.AddRange(NGramFeatures.SelectMany(ng => ng.ToFeatureArray()));

        // Quality features
        features.AddRange(QualityFeatures.ToFeatureArray());

        // Pattern features
        features.AddRange(PatternFeatures.ToFeatureArray());

        // Optional episode features
        if (EpisodeFeatures != null)
            features.AddRange(EpisodeFeatures.ToFeatureArray());

        // Optional release group features
        if (ReleaseGroupFeatures != null)
            features.AddRange(ReleaseGroupFeatures.ToFeatureArray());

        return features.ToArray();
    }

    /// <summary>
    /// Gets feature names for ML model training and debugging.
    /// </summary>
    /// <returns>Ordered list of feature names corresponding to ToFeatureArray()</returns>
    public IReadOnlyList<string> GetFeatureNames()
    {
        var names = new List<string>();

        names.AddRange(TokenFeatures.GetFeatureNames());
        names.AddRange(NGramFeatures.SelectMany(ng => ng.GetFeatureNames()));
        names.AddRange(QualityFeatures.GetFeatureNames());
        names.AddRange(PatternFeatures.GetFeatureNames());

        if (EpisodeFeatures != null)
            names.AddRange(EpisodeFeatures.GetFeatureNames());

        if (ReleaseGroupFeatures != null)
            names.AddRange(ReleaseGroupFeatures.GetFeatureNames());

        return names.AsReadOnly();
    }

    private int CalculateFeatureCount()
    {
        int count = TokenFeatures.FeatureCount +
                   NGramFeatures.Sum(ng => ng.FeatureCount) +
                   QualityFeatures.FeatureCount +
                   PatternFeatures.FeatureCount;

        if (EpisodeFeatures != null)
            count += EpisodeFeatures.FeatureCount;

        if (ReleaseGroupFeatures != null)
            count += ReleaseGroupFeatures.FeatureCount;

        return count;
    }
}