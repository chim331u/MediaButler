namespace MediaButler.ML.Models;

/// <summary>
/// Release group features for identifying and categorizing content sources.
/// Release groups often indicate quality levels and content characteristics.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable release group feature data
/// - Single responsibility: Only holds release group characteristics
/// - Declarative: Clear release group indicators without processing logic
/// </remarks>
public sealed record ReleaseGroupFeatures
{
    /// <summary>
    /// The identified release group name.
    /// </summary>
    public required string ReleaseGroup { get; init; }

    /// <summary>
    /// Reputation tier of the release group (based on known quality).
    /// </summary>
    public required ReleaseGroupReputation Reputation { get; init; }

    /// <summary>
    /// Regional focus of the release group (Italian, English, etc.).
    /// </summary>
    public required ReleaseGroupRegion Region { get; init; }

    /// <summary>
    /// Specialization of the release group (anime, TV shows, movies).
    /// </summary>
    public required ReleaseGroupSpecialization Specialization { get; init; }

    /// <summary>
    /// Estimated quality tier typically associated with this group.
    /// </summary>
    public required QualityTier TypicalQuality { get; init; }

    /// <summary>
    /// Whether this is a well-known/established release group.
    /// </summary>
    public required bool IsWellKnown { get; init; }

    /// <summary>
    /// Confidence in release group identification (0-1).
    /// </summary>
    public required double IdentificationConfidence { get; init; }

    /// <summary>
    /// Length of the release group name (can indicate legitimacy).
    /// </summary>
    public int NameLength => ReleaseGroup.Length;

    /// <summary>
    /// Whether the release group name contains numbers.
    /// </summary>
    public bool ContainsNumbers => ReleaseGroup.Any(char.IsDigit);

    /// <summary>
    /// Whether the release group follows typical naming patterns.
    /// </summary>
    public bool HasTypicalPattern => IsTypicalReleaseGroupPattern(ReleaseGroup);

    /// <summary>
    /// Number of features this contributes to ML model.
    /// </summary>
    public int FeatureCount => 12;

    /// <summary>
    /// Converts release group features to ML feature array.
    /// </summary>
    /// <returns>Feature array representing release group characteristics</returns>
    public float[] ToFeatureArray()
    {
        return new[]
        {
            // Categorical features
            (float)Reputation,
            (float)Region,
            (float)Specialization,
            (float)TypicalQuality,
            
            // Binary features
            IsWellKnown ? 1f : 0f,
            ContainsNumbers ? 1f : 0f,
            HasTypicalPattern ? 1f : 0f,
            
            // Numeric features
            (float)IdentificationConfidence,
            Math.Min(NameLength / 20f, 1f), // normalized name length
            
            // Derived features
            (Reputation == ReleaseGroupReputation.Premium) ? 1f : 0f,
            (Region == ReleaseGroupRegion.Italian) ? 1f : 0f, // Italian content focus
            (Specialization == ReleaseGroupSpecialization.TVShows) ? 1f : 0f
        };
    }

    /// <summary>
    /// Gets feature names for release group features.
    /// </summary>
    /// <returns>Feature names corresponding to ToFeatureArray()</returns>
    public IReadOnlyList<string> GetFeatureNames()
    {
        return new[]
        {
            "ReleaseGroup_Reputation",
            "ReleaseGroup_Region",
            "ReleaseGroup_Specialization",
            "ReleaseGroup_TypicalQuality",
            "ReleaseGroup_IsWellKnown",
            "ReleaseGroup_ContainsNumbers",
            "ReleaseGroup_HasTypicalPattern",
            "ReleaseGroup_IdentificationConfidence",
            "ReleaseGroup_NameLength",
            "ReleaseGroup_IsPremium",
            "ReleaseGroup_IsItalian",
            "ReleaseGroup_IsTVSpecialized"
        }.AsReadOnly();
    }

    /// <summary>
    /// Creates release group features from release group name.
    /// </summary>
    /// <param name="releaseGroupName">Release group name from tokenization</param>
    /// <returns>Release group features for ML</returns>
    public static ReleaseGroupFeatures FromReleaseGroupName(string releaseGroupName)
    {
        if (string.IsNullOrWhiteSpace(releaseGroupName))
            throw new ArgumentException("Release group name cannot be null or empty", nameof(releaseGroupName));

        var reputation = DetermineReputation(releaseGroupName);
        var region = DetermineRegion(releaseGroupName);
        var specialization = DetermineSpecialization(releaseGroupName);
        var quality = DetermineTypicalQuality(releaseGroupName, reputation);
        var isWellKnown = IsKnownReleaseGroup(releaseGroupName);
        var confidence = CalculateIdentificationConfidence(releaseGroupName, isWellKnown);

        return new ReleaseGroupFeatures
        {
            ReleaseGroup = releaseGroupName,
            Reputation = reputation,
            Region = region,
            Specialization = specialization,
            TypicalQuality = quality,
            IsWellKnown = isWellKnown,
            IdentificationConfidence = confidence
        };
    }

    private static bool IsTypicalReleaseGroupPattern(string name)
    {
        // Typical patterns: 3-15 characters, mix of letters/numbers, not all caps random
        if (name.Length < 3 || name.Length > 15)
            return false;

        // Should have some letters
        if (!name.Any(char.IsLetter))
            return false;

        // Not too many numbers (spam pattern)
        var digitRatio = name.Count(char.IsDigit) / (double)name.Length;
        if (digitRatio > 0.5)
            return false;

        return true;
    }

    private static ReleaseGroupReputation DetermineReputation(string name)
    {
        // Based on Italian training data analysis
        var premiumGroups = new[] { "NovaRip", "DarkSideMux", "Pir8", "iGM", "UBi" };
        var qualityGroups = new[] { "NTb", "MIXED", "IGM" };
        var unknownPatterns = new[] { "x264", "BaMax", "FoV" };

        var upperName = name.ToUpperInvariant();

        if (premiumGroups.Any(g => upperName.Contains(g.ToUpperInvariant())))
            return ReleaseGroupReputation.Premium;
        
        if (qualityGroups.Any(g => upperName.Contains(g.ToUpperInvariant())))
            return ReleaseGroupReputation.Good;
            
        if (unknownPatterns.Any(g => upperName.Contains(g.ToUpperInvariant())))
            return ReleaseGroupReputation.Unknown;

        // Default reputation based on name characteristics
        if (name.Length >= 4 && name.Length <= 8 && char.IsUpper(name[0]))
            return ReleaseGroupReputation.Average;

        return ReleaseGroupReputation.Unknown;
    }

    private static ReleaseGroupRegion DetermineRegion(string name)
    {
        var italianIndicators = new[] { "ITA", "NovaRip", "DarkSideMux", "Pir8" };
        var englishIndicators = new[] { "ENG", "PROPER", "REPACK" };

        var upperName = name.ToUpperInvariant();

        if (italianIndicators.Any(i => upperName.Contains(i)))
            return ReleaseGroupRegion.Italian;
            
        if (englishIndicators.Any(i => upperName.Contains(i)))
            return ReleaseGroupRegion.English;

        return ReleaseGroupRegion.International;
    }

    private static ReleaseGroupSpecialization DetermineSpecialization(string name)
    {
        var tvIndicators = new[] { "TV", "HDTV", "Series" };
        var animeIndicators = new[] { "Anime", "Sub", "Fansub" };
        var movieIndicators = new[] { "BluRay", "BDRip", "Movie" };

        var upperName = name.ToUpperInvariant();

        if (tvIndicators.Any(i => upperName.Contains(i)))
            return ReleaseGroupSpecialization.TVShows;
            
        if (animeIndicators.Any(i => upperName.Contains(i)))
            return ReleaseGroupSpecialization.Anime;
            
        if (movieIndicators.Any(i => upperName.Contains(i)))
            return ReleaseGroupSpecialization.Movies;

        return ReleaseGroupSpecialization.General;
    }

    private static QualityTier DetermineTypicalQuality(string name, ReleaseGroupReputation reputation)
    {
        return reputation switch
        {
            ReleaseGroupReputation.Premium => QualityTier.UltraHigh,
            ReleaseGroupReputation.Good => QualityTier.High,
            ReleaseGroupReputation.Average => QualityTier.Standard,
            ReleaseGroupReputation.Poor => QualityTier.Low,
            _ => QualityTier.Unknown
        };
    }

    private static bool IsKnownReleaseGroup(string name)
    {
        // Based on training data, these are commonly seen Italian release groups
        var knownGroups = new[]
        {
            "NovaRip", "DarkSideMux", "Pir8", "iGM", "UBi", "NTb", "MIXED", "IGM",
            "BaMax", "FoV", "KILLERS", "LOL", "DIMENSION", "SVA", "AFG"
        };

        return knownGroups.Any(g => g.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static double CalculateIdentificationConfidence(string name, bool isWellKnown)
    {
        var confidence = 0.5; // base confidence

        if (isWellKnown)
            confidence += 0.3;

        // Higher confidence for typical patterns
        if (IsTypicalReleaseGroupPattern(name))
            confidence += 0.2;

        // Lower confidence for very short names
        if (name.Length < 3)
            confidence -= 0.3;

        // Lower confidence for names that look like technical specs
        if (name.All(char.IsDigit) || name.ToLower().Contains("x264") || name.ToLower().Contains("x265"))
            confidence -= 0.2;

        return Math.Max(0.1, Math.Min(1.0, confidence));
    }
}

/// <summary>
/// Reputation levels for release groups based on known quality.
/// </summary>
public enum ReleaseGroupReputation
{
    Unknown = 0,
    Poor = 1,
    Average = 2,
    Good = 3,
    Premium = 4
}

/// <summary>
/// Regional focus of release groups.
/// </summary>
public enum ReleaseGroupRegion
{
    Unknown = 0,
    Italian = 1,
    English = 2,
    French = 3,
    German = 4,
    Spanish = 5,
    International = 6
}

/// <summary>
/// Specialization areas for release groups.
/// </summary>
public enum ReleaseGroupSpecialization
{
    Unknown = 0,
    General = 1,
    TVShows = 2,
    Movies = 3,
    Anime = 4,
    Documentaries = 5,
    Adult = 6
}