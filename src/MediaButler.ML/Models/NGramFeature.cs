namespace MediaButler.ML.Models;

/// <summary>
/// N-gram feature representing sequential token patterns for context-aware classification.
/// N-grams capture word order and context that individual tokens might miss.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable N-gram representation
/// - Single responsibility: Only holds N-gram pattern data
/// - Compose don't complex: Can be combined with other features independently
/// </remarks>
public sealed record NGramFeature
{
    /// <summary>
    /// The N-gram size (1 = unigram, 2 = bigram, 3 = trigram, etc.).
    /// </summary>
    public required int N { get; init; }

    /// <summary>
    /// The tokens that make up this N-gram in sequence.
    /// </summary>
    public required IReadOnlyList<string> Tokens { get; init; }

    /// <summary>
    /// Combined string representation of the N-gram.
    /// </summary>
    public string NGramText => string.Join(" ", Tokens);

    /// <summary>
    /// Frequency of this N-gram in the current filename context.
    /// </summary>
    public required int Frequency { get; init; }

    /// <summary>
    /// Relative frequency compared to other N-grams of same size (0-1).
    /// </summary>
    public required double RelativeFrequency { get; init; }

    /// <summary>
    /// Discriminative power score for this N-gram (higher = more distinctive).
    /// </summary>
    public required double DiscriminativePower { get; init; }

    /// <summary>
    /// Context category this N-gram likely represents.
    /// </summary>
    public required NGramContext Context { get; init; }

    /// <summary>
    /// Whether this N-gram spans across different semantic boundaries (e.g., series + quality).
    /// </summary>
    public required bool IsCrossBoundary { get; init; }

    /// <summary>
    /// Number of features this N-gram contributes to the ML model.
    /// </summary>
    public int FeatureCount => 4; // frequency, relative_freq, discriminative_power, context_encoding

    /// <summary>
    /// Converts N-gram to feature array for ML model.
    /// </summary>
    /// <returns>Feature array representing this N-gram</returns>
    public float[] ToFeatureArray()
    {
        return new[]
        {
            (float)Frequency,
            (float)RelativeFrequency,
            (float)DiscriminativePower,
            (float)Context // enum ordinal as feature
        };
    }

    /// <summary>
    /// Gets feature names for this N-gram.
    /// </summary>
    /// <returns>Feature names corresponding to ToFeatureArray()</returns>
    public IReadOnlyList<string> GetFeatureNames()
    {
        var prefix = $"NGram{N}_{NGramText.Replace(" ", "_")}";
        return new[]
        {
            $"{prefix}_Frequency",
            $"{prefix}_RelativeFrequency", 
            $"{prefix}_DiscriminativePower",
            $"{prefix}_Context"
        }.AsReadOnly();
    }

    /// <summary>
    /// Checks if this N-gram contains a specific token.
    /// </summary>
    /// <param name="token">Token to search for</param>
    /// <returns>True if N-gram contains the token</returns>
    public bool ContainsToken(string token) => 
        Tokens.Any(t => t.Equals(token, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the position of a token within this N-gram (0-based).
    /// </summary>
    /// <param name="token">Token to find position for</param>
    /// <returns>Position index or -1 if not found</returns>
    public int GetTokenPosition(string token)
    {
        for (int i = 0; i < Tokens.Count; i++)
        {
            if (Tokens[i].Equals(token, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}

/// <summary>
/// Context categories for N-gram classification to understand semantic meaning.
/// </summary>
public enum NGramContext
{
    /// <summary>
    /// N-gram represents part of a series name.
    /// </summary>
    SeriesName = 0,

    /// <summary>
    /// N-gram represents quality information (resolution, source, codec).
    /// </summary>
    Quality = 1,

    /// <summary>
    /// N-gram represents episode information (season, episode, title).
    /// </summary>
    Episode = 2,

    /// <summary>
    /// N-gram represents language information.
    /// </summary>
    Language = 3,

    /// <summary>
    /// N-gram represents release group or technical info.
    /// </summary>
    Technical = 4,

    /// <summary>
    /// N-gram spans multiple contexts or is unclassified.
    /// </summary>
    Mixed = 5,

    /// <summary>
    /// Unknown or unclassified context.
    /// </summary>
    Unknown = 6
}

/// <summary>
/// Collection of N-grams with analysis methods for feature extraction.
/// </summary>
public sealed record NGramCollection
{
    /// <summary>
    /// All N-grams in the collection.
    /// </summary>
    public required IReadOnlyList<NGramFeature> NGrams { get; init; }

    /// <summary>
    /// N-grams grouped by their size (1-grams, 2-grams, etc.).
    /// </summary>
    public ILookup<int, NGramFeature> BySize => NGrams.ToLookup(ng => ng.N);

    /// <summary>
    /// N-grams grouped by their context category.
    /// </summary>
    public ILookup<NGramContext, NGramFeature> ByContext => NGrams.ToLookup(ng => ng.Context);

    /// <summary>
    /// Gets the most discriminative N-grams for classification.
    /// </summary>
    /// <param name="topCount">Number of top N-grams to return</param>
    /// <returns>Most discriminative N-grams ordered by discriminative power</returns>
    public IReadOnlyList<NGramFeature> GetMostDiscriminative(int topCount = 10) =>
        NGrams.OrderByDescending(ng => ng.DiscriminativePower)
              .Take(topCount)
              .ToList()
              .AsReadOnly();

    /// <summary>
    /// Gets N-grams that likely represent the series name.
    /// </summary>
    /// <returns>Series name N-grams</returns>
    public IReadOnlyList<NGramFeature> GetSeriesNameNGrams() =>
        NGrams.Where(ng => ng.Context == NGramContext.SeriesName)
              .OrderByDescending(ng => ng.DiscriminativePower)
              .ToList()
              .AsReadOnly();
}