namespace MediaButler.ML.Models;

/// <summary>
/// Analysis of token frequencies and their discriminative power for classification.
/// This helps identify which tokens are most important for series identification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable frequency analysis results
/// - Single responsibility: Only holds token frequency data
/// - Declarative: Clear representation of token importance
/// </remarks>
public sealed record TokenFrequencyAnalysis
{
    /// <summary>
    /// Total number of unique tokens analyzed.
    /// </summary>
    public required int TotalTokens { get; init; }

    /// <summary>
    /// Most frequent tokens with their counts and importance scores.
    /// </summary>
    public required IReadOnlyList<TokenFrequency> FrequentTokens { get; init; }

    /// <summary>
    /// Rare tokens that might be highly discriminative.
    /// </summary>
    public required IReadOnlyList<TokenFrequency> RareTokens { get; init; }

    /// <summary>
    /// Average token length in the analysis.
    /// </summary>
    public required double AverageTokenLength { get; init; }

    /// <summary>
    /// Ratio of alphabetic to numeric tokens.
    /// </summary>
    public required double AlphaNumericRatio { get; init; }

    /// <summary>
    /// Diversity score based on token distribution (0-1, higher = more diverse).
    /// </summary>
    public required double DiversityScore { get; init; }

    /// <summary>
    /// Language indicators found in tokens (ITA, ENG, etc.).
    /// </summary>
    public required IReadOnlyList<string> LanguageIndicators { get; init; }

    /// <summary>
    /// Number of features this analysis contributes to the ML model.
    /// </summary>
    public int FeatureCount => 6 + FrequentTokens.Count + RareTokens.Count + LanguageIndicators.Count;

    /// <summary>
    /// Converts token frequency analysis to feature array for ML.
    /// </summary>
    /// <returns>Feature array representing token frequency characteristics</returns>
    public float[] ToFeatureArray()
    {
        var features = new List<float>
        {
            TotalTokens,
            (float)AverageTokenLength,
            (float)AlphaNumericRatio,
            (float)DiversityScore,
            FrequentTokens.Count,
            RareTokens.Count
        };

        // Add top frequent token scores (normalized)
        var topFrequent = FrequentTokens.Take(10).ToList();
        for (int i = 0; i < 10; i++)
        {
            features.Add(i < topFrequent.Count ? (float)topFrequent[i].ImportanceScore : 0f);
        }

        // Add rare token presence indicators (binary features)
        var topRare = RareTokens.Take(5).ToList();
        for (int i = 0; i < 5; i++)
        {
            features.Add(i < topRare.Count ? 1f : 0f);
        }

        // Language indicator presence
        var commonLanguages = new[] { "ita", "eng", "sub", "dub", "multi" };
        features.AddRange(commonLanguages.Select(lang => 
            LanguageIndicators.Any(li => li.Equals(lang, StringComparison.OrdinalIgnoreCase)) ? 1f : 0f));

        return features.ToArray();
    }

    /// <summary>
    /// Gets feature names for this token frequency analysis.
    /// </summary>
    /// <returns>Ordered feature names corresponding to ToFeatureArray()</returns>
    public IReadOnlyList<string> GetFeatureNames()
    {
        var names = new List<string>
        {
            "TotalTokens",
            "AverageTokenLength", 
            "AlphaNumericRatio",
            "DiversityScore",
            "FrequentTokensCount",
            "RareTokensCount"
        };

        // Top frequent token importance scores
        for (int i = 0; i < 10; i++)
        {
            names.Add($"FrequentToken{i + 1}_ImportanceScore");
        }

        // Rare token presence indicators
        for (int i = 0; i < 5; i++)
        {
            names.Add($"RareToken{i + 1}_Present");
        }

        // Language indicators
        var commonLanguages = new[] { "ita", "eng", "sub", "dub", "multi" };
        names.AddRange(commonLanguages.Select(lang => $"Language_{lang.ToUpper()}_Present"));

        return names.AsReadOnly();
    }
}

/// <summary>
/// Individual token frequency information with importance scoring.
/// </summary>
public sealed record TokenFrequency
{
    /// <summary>
    /// The token string.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// How many times this token appears.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Relative frequency (0-1) within the analyzed set.
    /// </summary>
    public required double RelativeFrequency { get; init; }

    /// <summary>
    /// Importance score for classification (higher = more discriminative).
    /// </summary>
    public required double ImportanceScore { get; init; }

    /// <summary>
    /// Token category (e.g., "series_name", "quality", "language").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Whether this token appears in multiple different series (lower discriminative power).
    /// </summary>
    public required bool IsCommonAcrossSeries { get; init; }
}