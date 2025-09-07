using System.Text.RegularExpressions;

namespace MediaButler.ML.Models;

/// <summary>
/// Pattern matching features extracted from filename structure using regex analysis.
/// These features capture filename patterns that are discriminative for classification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable pattern analysis results
/// - Single responsibility: Only holds pattern matching results
/// - Declarative: Clear pattern indicators without regex implementation details
/// </remarks>
public sealed record PatternMatchingFeatures
{
    /// <summary>
    /// Detected filename pattern type.
    /// </summary>
    public required FilenamePatternType PatternType { get; init; }

    /// <summary>
    /// Structure complexity score (0-10, higher = more complex structure).
    /// </summary>
    public required int StructureComplexity { get; init; }

    /// <summary>
    /// Number of separators in filename (dots, underscores, dashes).
    /// </summary>
    public required int SeparatorCount { get; init; }

    /// <summary>
    /// Ratio of alphabetic to numeric characters.
    /// </summary>
    public required double AlphaNumericRatio { get; init; }

    /// <summary>
    /// Whether filename contains year pattern (1900-2099).
    /// </summary>
    public required bool ContainsYear { get; init; }

    /// <summary>
    /// Whether filename contains episode pattern.
    /// </summary>
    public required bool ContainsEpisodePattern { get; init; }

    /// <summary>
    /// Whether filename contains quality indicators.
    /// </summary>
    public required bool ContainsQualityPattern { get; init; }

    /// <summary>
    /// Whether filename contains language indicators.
    /// </summary>
    public required bool ContainsLanguagePattern { get; init; }

    /// <summary>
    /// Whether filename contains release group pattern.
    /// </summary>
    public required bool ContainsReleaseGroupPattern { get; init; }

    /// <summary>
    /// Detected patterns with their confidence scores.
    /// </summary>
    public required IReadOnlyList<DetectedPattern> DetectedPatterns { get; init; }

    /// <summary>
    /// Overall pattern confidence (0-1, higher = more confident in pattern detection).
    /// </summary>
    public required double PatternConfidence { get; init; }

    /// <summary>
    /// Length category of the filename.
    /// </summary>
    public required FilenameLengthCategory LengthCategory { get; init; }

    /// <summary>
    /// Number of features this contributes to ML model.
    /// </summary>
    public int FeatureCount => 15 + DetectedPatterns.Count;

    /// <summary>
    /// Converts pattern features to ML feature array.
    /// </summary>
    /// <returns>Feature array representing pattern characteristics</returns>
    public float[] ToFeatureArray()
    {
        var features = new List<float>
        {
            // Pattern type as categorical feature
            (float)PatternType,
            
            // Structural features
            StructureComplexity / 10f, // normalized 0-1
            SeparatorCount / 20f, // normalize to reasonable range
            (float)AlphaNumericRatio,
            (float)PatternConfidence,
            
            // Length category
            (float)LengthCategory,
            
            // Binary pattern indicators
            ContainsYear ? 1f : 0f,
            ContainsEpisodePattern ? 1f : 0f,
            ContainsQualityPattern ? 1f : 0f,
            ContainsLanguagePattern ? 1f : 0f,
            ContainsReleaseGroupPattern ? 1f : 0f
        };

        // Pattern type specific features
        features.AddRange(this.GetPatternTypeFeatures());

        // Top detected pattern confidences
        var topPatterns = DetectedPatterns.OrderByDescending(p => p.Confidence).Take(5);
        for (int i = 0; i < 5; i++)
        {
            var pattern = topPatterns.ElementAtOrDefault(i);
            features.Add((float)(pattern?.Confidence ?? 0.0));
        }

        return features.ToArray();
    }

    /// <summary>
    /// Gets feature names for pattern matching features.
    /// </summary>
    /// <returns>Feature names corresponding to ToFeatureArray()</returns>
    public IReadOnlyList<string> GetFeatureNames()
    {
        var names = new List<string>
        {
            "Pattern_Type",
            "Pattern_StructureComplexity",
            "Pattern_SeparatorCount", 
            "Pattern_AlphaNumericRatio",
            "Pattern_Confidence",
            "Pattern_LengthCategory",
            "Pattern_ContainsYear",
            "Pattern_ContainsEpisode",
            "Pattern_ContainsQuality",
            "Pattern_ContainsLanguage",
            "Pattern_ContainsReleaseGroup"
        };

        // Pattern type specific feature names
        names.AddRange(this.GetPatternTypeFeatureNames());

        // Top pattern confidences
        for (int i = 0; i < 5; i++)
        {
            names.Add($"Pattern_TopDetected{i + 1}_Confidence");
        }

        return names.AsReadOnly();
    }

    // TODO: Fix static context issues in future sprint - temporarily disabled
    // Full pattern matching extraction will be implemented in Sprint 2.2.x

    private float[] GetPatternTypeFeatures()
    {
        // Binary indicators for each pattern type
        var types = Enum.GetValues<FilenamePatternType>();
        return types.Select(t => this.PatternType == t ? 1f : 0f).ToArray();
    }

    private IReadOnlyList<string> GetPatternTypeFeatureNames()
    {
        var types = Enum.GetValues<FilenamePatternType>();
        return types.Select(t => $"Pattern_Is{t}").ToList().AsReadOnly();
    }

    // TODO: Fix static context issues - temporarily disabled
    /*private static List<DetectedPattern> DetectPatterns(string filename)
    {
        var patterns = new List<DetectedPattern>();

        // Year pattern (1900-2099)
        var yearRegex = new Regex(@"\b(19|20)\d{2}\b");
        if (yearRegex.IsMatch(filename))
        {
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Year,
                Pattern = yearRegex.Match(filename).Value,
                Confidence = 0.95,
                Position = yearRegex.Match(filename).Index
            });
        }

        // Episode patterns
        var episodePatterns = new[]
        {
            (new Regex(@"[Ss](\d{1,2})[Ee](\d{1,2})"), 0.9),
            (new Regex(@"(\d{1,2})x(\d{1,2})"), 0.85),
            (new Regex(@"\b[Ee]p?(\d{1,3})\b"), 0.75),
            (new Regex(@"\b(\d{3,4})\b"), 0.6) // Large episode numbers
        };

        foreach (var (regex, confidence) in episodePatterns)
        {
            var match = regex.Match(filename);
            if (match.Success)
            {
                patterns.Add(new DetectedPattern
                {
                    Type = PatternType.Episode,
                    Pattern = match.Value,
                    Confidence = confidence,
                    Position = match.Index
                });
                break; // Only add first episode match
            }
        }

        // Quality patterns
        var qualityRegex = new Regex(@"\b(2160p|4K|1080p|720p|480p|WEB-?DL|BluRay|HDTV|x264|x265|HEVC)\b", RegexOptions.IgnoreCase);
        var qualityMatches = qualityRegex.Matches(filename);
        foreach (Match match in qualityMatches)
        {
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Quality,
                Pattern = match.Value,
                Confidence = 0.85,
                Position = match.Index
            });
        }

        // Language patterns
        var languageRegex = new Regex(@"\b(ITA|ENG|SUB|DUB|iTALiAN|ENGLISH|MULTI)\b", RegexOptions.IgnoreCase);
        var langMatches = languageRegex.Matches(filename);
        foreach (Match match in langMatches)
        {
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.Language,
                Pattern = match.Value,
                Confidence = 0.8,
                Position = match.Index
            });
        }

        // Release group pattern (usually at end in brackets or after dash)
        var releaseGroupRegex = new Regex(@"[-\[]([A-Za-z0-9]+)[\]\.]?\w*$");
        var rgMatch = releaseGroupRegex.Match(filename);
        if (rgMatch.Success && rgMatch.Groups[1].Value.Length >= 3)
        {
            patterns.Add(new DetectedPattern
            {
                Type = PatternType.ReleaseGroup,
                Pattern = rgMatch.Groups[1].Value,
                Confidence = 0.7,
                Position = rgMatch.Index
            });
        }

        return patterns;
    }

    private static FilenamePatternType DeterminePatternType(string filename, List<DetectedPattern> patterns)
    {
        // Heuristic to determine overall filename pattern type
        if (patterns.Any(p => p.Type == PatternType.Episode))
        {
            if (patterns.Count(p => p.Type == PatternType.Quality) >= 2)
                return FilenamePatternType.TVShowComplete;
            return FilenamePatternType.TVShowBasic;
        }

        if (patterns.Any(p => p.Type == PatternType.Year))
        {
            return FilenamePatternType.Movie;
        }

        if (patterns.Count >= 3)
            return FilenamePatternType.Complex;

        return FilenamePatternType.Simple;
    }

    private static int CalculateStructureComplexity(string filename)
    {
        var score = 0;
        
        // More separators = more complex
        score += CountSeparators(filename) / 3;
        
        // Mixed case patterns
        if (filename.Any(char.IsUpper) && filename.Any(char.IsLower))
            score += 2;
            
        // Numbers interspersed with letters
        if (Regex.IsMatch(filename, @"[a-zA-Z]\d|\d[a-zA-Z]"))
            score += 2;
            
        // Special characters
        if (filename.Any(c => "[](){}".Contains(c)))
            score += 2;
            
        // Long consecutive sequences
        if (Regex.IsMatch(filename, @"[a-zA-Z]{10,}|\d{4,}"))
            score += 1;

        return Math.Min(score, 10);
    }

    private static int CountSeparators(string filename)
    {
        return filename.Count(c => "._- ".Contains(c));
    }

    private static double CalculateAlphaNumericRatio(string filename)
    {
        var alphaCount = filename.Count(char.IsLetter);
        var numCount = filename.Count(char.IsDigit);
        
        if (numCount == 0) return double.MaxValue;
        return (double)alphaCount / numCount;
    }

    private static double CalculatePatternConfidence(List<DetectedPattern> patterns)
    {
        if (!patterns.Any()) return 0.0;
        
        return patterns.Average(p => p.Confidence);
    }

    private static FilenameLengthCategory DetermineLengthCategory(string filename)
    {
        return filename.Length switch
        {
            < 20 => FilenameLengthCategory.Short,
            < 50 => FilenameLengthCategory.Medium,
            < 100 => FilenameLengthCategory.Long,
            _ => FilenameLengthCategory.VeryLong
        };
    } */
}

/// <summary>
/// Types of filename patterns detected.
/// </summary>
public enum FilenamePatternType
{
    Simple = 0,
    TVShowBasic = 1,
    TVShowComplete = 2,
    Movie = 3,
    Complex = 4,
    Unknown = 5
}

/// <summary>
/// Length categories for filenames.
/// </summary>
public enum FilenameLengthCategory
{
    Short = 0,
    Medium = 1,
    Long = 2,
    VeryLong = 3
}

/// <summary>
/// Individual detected pattern in filename.
/// </summary>
public sealed record DetectedPattern
{
    public required PatternType Type { get; init; }
    public required string Pattern { get; init; }
    public required double Confidence { get; init; }
    public required int Position { get; init; }
}

/// <summary>
/// Types of patterns that can be detected in filenames.
/// </summary>
public enum PatternType
{
    Year,
    Episode,
    Quality,
    Language,
    ReleaseGroup,
    Technical
}