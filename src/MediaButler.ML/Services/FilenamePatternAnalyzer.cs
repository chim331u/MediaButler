using System.Text.RegularExpressions;
using MediaButler.ML.Models;

namespace MediaButler.ML.Services;

/// <summary>
/// Analyzes filename patterns to extract insights for improving tokenization and classification.
/// This service operates independently of domain concerns, focusing solely on pattern recognition.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only analyzes filename patterns
/// - No complecting: Separate from ML training and domain logic
/// - Values over state: Returns immutable analysis results
/// - Declarative: Describes patterns found, not how they were found
/// </remarks>
public class FilenamePatternAnalyzer
{
    private static readonly Regex[] EpisodePatterns = new[]
    {
        new Regex(@"[Ss](\d+)[Ee](\d+)", RegexOptions.Compiled), // S01E01
        new Regex(@"(\d+)x(\d+)", RegexOptions.Compiled), // 1x01  
        new Regex(@"Season\s*(\d+).*?Episode\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase), // Season 1 Episode 1
        new Regex(@"[Ee]p?(\d+)", RegexOptions.Compiled), // E01, Ep01
        new Regex(@"(\d{4})[.\-_](\d{2})[.\-_](\d{2})", RegexOptions.Compiled) // 2023.12.25 (date-based)
    };

    private static readonly Regex[] QualityPatterns = new[]
    {
        new Regex(@"\b(2160p|4K|UHD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(1080p|FHD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(720p|HD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(480p|SD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(BluRay|BDRip|BRRip)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(WEBRip|WEB-DL|WEBDL)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(HDTV|PDTV|SDTV)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(DVDRip|DVD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    private static readonly Regex[] CodecPatterns = new[]
    {
        new Regex(@"\b(x264|H\.264|AVC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(x265|H\.265|HEVC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(XviD|DivX)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(AV1)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    private static readonly Regex[] LanguagePatterns = new[]
    {
        new Regex(@"\b(ENG|EN|ENGLISH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(ITA|IT|ITALIAN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(FRA|FR|FRENCH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(GER|DE|GERMAN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(SPA|ES|SPANISH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(SUB|SUBS|SUBTITLES)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(DUB|DUBBED)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    private static readonly Regex[] ReleasePatterns = new[]
    {
        new Regex(@"\b(FINAL|REPACK|PROPER|REAL)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(EXTENDED|UNCUT|DIRECTORS?\.CUT)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\b(LIMITED|INTERNAL)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    /// <summary>
    /// Analyzes multiple filenames to extract common patterns and insights.
    /// </summary>
    /// <param name="filenames">Collection of filenames to analyze</param>
    /// <returns>Pattern analysis results</returns>
    public PatternAnalysisResult AnalyzePatterns(IEnumerable<string> filenames)
    {
        var filenameList = filenames.ToList();
        var totalFiles = filenameList.Count;

        if (totalFiles == 0)
        {
            return new PatternAnalysisResult
            {
                TotalFilesAnalyzed = 0,
                CommonSeparators = Array.Empty<string>(),
                EpisodePatterns = Array.Empty<string>(),
                QualityIndicators = Array.Empty<string>(),
                LanguageCodes = Array.Empty<string>(),
                ReleasePatterns = Array.Empty<string>(),
                SeriesNamePatterns = Array.Empty<string>(),
                Recommendations = new[] { "No files provided for analysis" }
            };
        }

        // Analyze separators
        var separators = AnalyzeSeparators(filenameList);
        
        // Analyze episode patterns
        var episodePatterns = AnalyzeEpisodePatterns(filenameList);
        
        // Analyze quality indicators
        var qualityIndicators = AnalyzeQualityIndicators(filenameList);
        
        // Analyze language codes
        var languageCodes = AnalyzeLanguageCodes(filenameList);
        
        // Analyze release patterns
        var releasePatterns = AnalyzeReleasePatterns(filenameList);
        
        // Analyze series name patterns
        var seriesNamePatterns = AnalyzeSeriesNamePatterns(filenameList);
        
        // Generate recommendations
        var recommendations = GenerateRecommendations(
            separators, episodePatterns, qualityIndicators, 
            languageCodes, releasePatterns, totalFiles);

        return new PatternAnalysisResult
        {
            TotalFilesAnalyzed = totalFiles,
            CommonSeparators = separators,
            EpisodePatterns = episodePatterns,
            QualityIndicators = qualityIndicators,
            LanguageCodes = languageCodes,
            ReleasePatterns = releasePatterns,
            SeriesNamePatterns = seriesNamePatterns,
            Recommendations = recommendations,
            AnalyzedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Analyzes a single filename and extracts all identifiable components.
    /// </summary>
    /// <param name="filename">The filename to analyze</param>
    /// <returns>Detailed filename analysis</returns>
    public FilenameAnalysis AnalyzeFilename(string filename)
    {
        var episodeInfo = ExtractEpisodeInfo(filename);
        var qualityInfo = ExtractQualityInfo(filename);
        var languageInfo = ExtractLanguageInfo(filename);
        var releaseInfo = ExtractReleaseInfo(filename);
        var seriesName = ExtractPotentialSeriesName(filename);

        return new FilenameAnalysis
        {
            OriginalFilename = filename,
            ExtractedSeriesName = seriesName,
            EpisodeInfo = episodeInfo,
            QualityInfo = qualityInfo,
            LanguageInfo = languageInfo,
            ReleaseInfo = releaseInfo,
            Confidence = CalculateExtractionConfidence(episodeInfo, qualityInfo, seriesName),
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private static List<string> AnalyzeSeparators(List<string> filenames)
    {
        var separatorCounts = new Dictionary<char, int>();
        var commonSeparators = new[] { '.', '_', '-', ' ' };

        foreach (var filename in filenames)
        {
            foreach (var separator in commonSeparators)
            {
                var count = filename.Count(c => c == separator);
                separatorCounts[separator] = separatorCounts.GetValueOrDefault(separator, 0) + count;
            }
        }

        return separatorCounts
            .Where(kvp => kvp.Value > filenames.Count * 0.1) // Present in at least 10% of files
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key.ToString())
            .ToList();
    }

    private static List<string> AnalyzeEpisodePatterns(List<string> filenames)
    {
        var patternCounts = new Dictionary<string, int>();

        foreach (var filename in filenames)
        {
            foreach (var pattern in EpisodePatterns)
            {
                if (pattern.IsMatch(filename))
                {
                    var patternName = GetPatternName(pattern);
                    patternCounts[patternName] = patternCounts.GetValueOrDefault(patternName, 0) + 1;
                }
            }
        }

        return patternCounts
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private static List<string> AnalyzeQualityIndicators(List<string> filenames)
    {
        var qualitySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filename in filenames)
        {
            foreach (var pattern in QualityPatterns)
            {
                var matches = pattern.Matches(filename);
                foreach (Match match in matches)
                {
                    qualitySet.Add(match.Value.ToUpperInvariant());
                }
            }
        }

        return qualitySet.OrderBy(q => q).ToList();
    }

    private static List<string> AnalyzeLanguageCodes(List<string> filenames)
    {
        var languageSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filename in filenames)
        {
            foreach (var pattern in LanguagePatterns)
            {
                var matches = pattern.Matches(filename);
                foreach (Match match in matches)
                {
                    languageSet.Add(match.Value.ToUpperInvariant());
                }
            }
        }

        return languageSet.OrderBy(l => l).ToList();
    }

    private static List<string> AnalyzeReleasePatterns(List<string> filenames)
    {
        var releaseSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filename in filenames)
        {
            foreach (var pattern in ReleasePatterns)
            {
                var matches = pattern.Matches(filename);
                foreach (Match match in matches)
                {
                    releaseSet.Add(match.Value.ToUpperInvariant());
                }
            }
        }

        return releaseSet.OrderBy(r => r).ToList();
    }

    private static List<string> AnalyzeSeriesNamePatterns(List<string> filenames)
    {
        // This is a simplified analysis - in practice, this would be more sophisticated
        var commonWords = new Dictionary<string, int>();

        foreach (var filename in filenames)
        {
            var potentialSeries = ExtractPotentialSeriesName(filename);
            if (!string.IsNullOrWhiteSpace(potentialSeries))
            {
                var words = potentialSeries.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (word.Length > 2) // Skip very short words
                    {
                        commonWords[word.ToUpperInvariant()] = commonWords.GetValueOrDefault(word.ToUpperInvariant(), 0) + 1;
                    }
                }
            }
        }

        return commonWords
            .Where(kvp => kvp.Value > 1) // Must appear in multiple files
            .OrderByDescending(kvp => kvp.Value)
            .Take(20) // Top 20 most common words
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private static string ExtractPotentialSeriesName(string filename)
    {
        // Remove file extension
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
        
        // Find the first episode pattern and extract everything before it
        foreach (var pattern in EpisodePatterns)
        {
            var match = pattern.Match(nameWithoutExt);
            if (match.Success)
            {
                var seriesPart = nameWithoutExt.Substring(0, match.Index).Trim();
                // Replace common separators with spaces and clean up
                return CleanSeriesName(seriesPart);
            }
        }

        // If no episode pattern found, use the whole name (less common)
        return CleanSeriesName(nameWithoutExt);
    }

    private static string CleanSeriesName(string input)
    {
        // Replace separators with spaces
        var cleaned = input.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
        
        // Remove multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        
        return cleaned.Trim();
    }

    private static EpisodeInfo? ExtractEpisodeInfo(string filename)
    {
        foreach (var pattern in EpisodePatterns)
        {
            var match = pattern.Match(filename);
            if (match.Success && match.Groups.Count >= 3)
            {
                if (int.TryParse(match.Groups[1].Value, out var season) &&
                    int.TryParse(match.Groups[2].Value, out var episode))
                {
                    return new EpisodeInfo
                    {
                        Season = season,
                        Episode = episode,
                        RawPattern = match.Value,
                        PatternType = GetEpisodePatternType(pattern)
                    };
                }
            }
        }

        return null;
    }

    private static QualityInfo ExtractQualityInfo(string filename)
    {
        var resolution = ExtractFirstMatch(filename, QualityPatterns.Take(4).ToArray());
        var source = ExtractFirstMatch(filename, QualityPatterns.Skip(4).Take(4).ToArray());
        var codec = ExtractFirstMatch(filename, CodecPatterns);

        return new QualityInfo
        {
            Resolution = resolution,
            Source = source,
            VideoCodec = codec,
            QualityTier = DetermineQualityTier(resolution, source)
        };
    }

    private static List<string> ExtractLanguageInfo(string filename)
    {
        var languages = new List<string>();

        foreach (var pattern in LanguagePatterns)
        {
            var matches = pattern.Matches(filename);
            foreach (Match match in matches)
            {
                languages.Add(match.Value.ToUpperInvariant());
            }
        }

        return languages;
    }

    private static List<string> ExtractReleaseInfo(string filename)
    {
        var releases = new List<string>();

        foreach (var pattern in ReleasePatterns)
        {
            var matches = pattern.Matches(filename);
            foreach (Match match in matches)
            {
                releases.Add(match.Value.ToUpperInvariant());
            }
        }

        return releases;
    }

    private static string? ExtractFirstMatch(string filename, Regex[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = pattern.Match(filename);
            if (match.Success)
            {
                return match.Value;
            }
        }
        return null;
    }

    private static QualityTier DetermineQualityTier(string? resolution, string? source)
    {
        return resolution?.ToUpperInvariant() switch
        {
            "2160P" or "4K" or "UHD" => QualityTier.Premium,
            "1080P" or "FHD" => source?.ToUpperInvariant() switch
            {
                "BLURAY" or "BDRIP" => QualityTier.UltraHigh,
                _ => QualityTier.High
            },
            "720P" or "HD" => QualityTier.Standard,
            "480P" or "SD" => QualityTier.Low,
            _ => QualityTier.Unknown
        };
    }

    private static EpisodePatternType GetEpisodePatternType(Regex pattern) => pattern.ToString() switch
    {
        var p when p.Contains(@"[Ss](\d+)[Ee](\d+)") => EpisodePatternType.Standard,
        var p when p.Contains(@"(\d+)x(\d+)") => EpisodePatternType.Alternative,
        var p when p.Contains("Season") => EpisodePatternType.Verbose,
        var p when p.Contains(@"[Ee]p?(\d+)") => EpisodePatternType.EpisodeOnly,
        var p when p.Contains(@"(\d{4})") => EpisodePatternType.DateBased,
        _ => EpisodePatternType.None
    };

    private static string GetPatternName(Regex pattern) => pattern.ToString() switch
    {
        var p when p.Contains(@"[Ss](\d+)[Ee](\d+)") => "Standard (S##E##)",
        var p when p.Contains(@"(\d+)x(\d+)") => "Alternative (##x##)",
        var p when p.Contains("Season") => "Verbose (Season # Episode #)",
        var p when p.Contains(@"[Ee]p?(\d+)") => "Episode Only (E##/Ep##)",
        var p when p.Contains(@"(\d{4})") => "Date-based (YYYY.MM.DD)",
        _ => "Unknown Pattern"
    };

    private static float CalculateExtractionConfidence(EpisodeInfo? episodeInfo, QualityInfo qualityInfo, string seriesName)
    {
        float confidence = 0.0f;

        // Episode info adds significant confidence
        if (episodeInfo?.IsValid == true) confidence += 0.4f;

        // Quality info adds some confidence
        if (qualityInfo.HasQualityInfo) confidence += 0.2f;

        // Series name adds confidence based on length and structure
        if (!string.IsNullOrWhiteSpace(seriesName))
        {
            if (seriesName.Length > 3) confidence += 0.2f;
            if (seriesName.Contains(' ')) confidence += 0.1f; // Multi-word series names are more likely correct
            if (seriesName.All(c => char.IsLetterOrDigit(c) || c == ' ')) confidence += 0.1f; // Clean characters
        }

        return Math.Min(confidence, 1.0f);
    }

    private static List<string> GenerateRecommendations(
        List<string> separators, List<string> episodePatterns, List<string> qualityIndicators,
        List<string> languageCodes, List<string> releasePatterns, int totalFiles)
    {
        var recommendations = new List<string>();

        if (separators.Count > 0)
        {
            recommendations.Add($"Primary separators: {string.Join(", ", separators.Take(3))}");
        }

        if (episodePatterns.Count > 0)
        {
            recommendations.Add($"Most common episode pattern: {episodePatterns.First()}");
        }

        if (qualityIndicators.Count > 3)
        {
            recommendations.Add("High variety of quality indicators - consider quality-based organization");
        }

        if (languageCodes.Count > 2)
        {
            recommendations.Add("Multiple languages detected - enable language filtering");
        }

        if (totalFiles > 100)
        {
            recommendations.Add("Large dataset - enable batch processing for optimal performance");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Standard configuration should work well for this dataset");
        }

        return recommendations;
    }
}

/// <summary>
/// Represents the result of analyzing filename patterns.
/// </summary>
public record PatternAnalysisResult
{
    /// <summary>
    /// Gets the total number of files analyzed.
    /// </summary>
    public int TotalFilesAnalyzed { get; init; }

    /// <summary>
    /// Gets the common separators found in filenames.
    /// </summary>
    public IReadOnlyList<string> CommonSeparators { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the episode patterns identified.
    /// </summary>
    public IReadOnlyList<string> EpisodePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the quality indicators found.
    /// </summary>
    public IReadOnlyList<string> QualityIndicators { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the language codes identified.
    /// </summary>
    public IReadOnlyList<string> LanguageCodes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the release patterns found.
    /// </summary>
    public IReadOnlyList<string> ReleasePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets common words found in series names.
    /// </summary>
    public IReadOnlyList<string> SeriesNamePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets recommendations for tokenization configuration.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets when this analysis was performed.
    /// </summary>
    public DateTime AnalyzedAt { get; init; }
}

/// <summary>
/// Represents the analysis of a single filename.
/// </summary>
public record FilenameAnalysis
{
    /// <summary>
    /// Gets the original filename that was analyzed.
    /// </summary>
    public string OriginalFilename { get; init; } = string.Empty;

    /// <summary>
    /// Gets the extracted series name.
    /// </summary>
    public string ExtractedSeriesName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the episode information if found.
    /// </summary>
    public EpisodeInfo? EpisodeInfo { get; init; }

    /// <summary>
    /// Gets the quality information extracted.
    /// </summary>
    public QualityInfo QualityInfo { get; init; } = new();

    /// <summary>
    /// Gets the language information found.
    /// </summary>
    public IReadOnlyList<string> LanguageInfo { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the release information found.
    /// </summary>
    public IReadOnlyList<string> ReleaseInfo { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the confidence score of the extraction (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Gets when this analysis was performed.
    /// </summary>
    public DateTime AnalyzedAt { get; init; }
}