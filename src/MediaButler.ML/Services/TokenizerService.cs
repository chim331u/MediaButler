using System.Text.RegularExpressions;
using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;

namespace MediaButler.ML.Services;

/// <summary>
/// Implementation of filename tokenization and series name extraction service.
/// This service operates independently of domain concerns, focusing solely on filename analysis.
/// </summary>
/// <remarks>
/// Optimized for Italian content based on training data analysis:
/// - Supports both #x## (8x04) and S##E## episode patterns
/// - Handles Italian language indicators (ITA, iTALiAN, ITA_ENG)
/// - Recognizes Italian video sources (WEBMux, HDTVMux, DLMux)
///
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only tokenizes filenames
/// - No complecting: Separate from domain business logic
/// - Values over state: Pure functions with immutable inputs/outputs
/// - Declarative: Clear pattern matching without complex state
///
/// ARM32 optimization: Using source-generated regexes (.NET 7+) for:
/// - 15-20% faster than compiled regexes
/// - Zero JIT overhead (pre-compiled to IL)
/// - Reduced CPU usage and faster startup time
/// </remarks>
public partial class TokenizerService : ITokenizerService
{
    private readonly ILogger<TokenizerService> _logger;

    // Source-generated episode patterns optimized for Italian content
    [GeneratedRegex(@"(\d{1,2})x(\d{1,2})", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternAlternative(); // 8x04 (most common in Italian data)

    [GeneratedRegex(@"[Ss](\d{1,2})[Ee](\d{1,2})")]
    private static partial Regex EpisodePatternStandard(); // S01E01 (standard pattern)

    [GeneratedRegex(@"Season\s*(\d{1,2}).*?Episode\s*(\d{1,2})", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternVerbose(); // Season 1 Episode 1

    [GeneratedRegex(@"[Ee]p?(\d{1,2})")]
    private static partial Regex EpisodePatternEpisodeOnly(); // E01, Ep01 (episode only)

    [GeneratedRegex(@"(\d{4})[.\-_](\d{2})[.\-_](\d{2})")]
    private static partial Regex EpisodePatternDateBased(); // Date-based episodes

    [GeneratedRegex(@"\b(\d{3,4})\b")]
    private static partial Regex EpisodePatternLargeNumber(); // Large episode numbers like 1089 for One Piece

    // Episode patterns array (initialized from source-generated regexes)
    private static readonly Regex[] EpisodePatterns = new[]
    {
        EpisodePatternAlternative(),
        EpisodePatternStandard(),
        EpisodePatternVerbose(),
        EpisodePatternEpisodeOnly(),
        EpisodePatternDateBased(),
        EpisodePatternLargeNumber()
    };

    // Source-generated quality patterns observed in Italian training data
    [GeneratedRegex(@"\b(2160p|4K|UHD)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPattern4K();

    [GeneratedRegex(@"\b(1080p|FHD)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPattern1080p();

    [GeneratedRegex(@"\b(720p|HD)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPattern720p();

    [GeneratedRegex(@"\b(480p|SD)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPattern480p();

    [GeneratedRegex(@"\b(WEBMux|WEBDL|WEB-DL|WEB-DLMux)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternWebMux(); // Most common in Italian content

    [GeneratedRegex(@"\b(HDTVMux|HDTV)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternHDTV(); // Very common

    [GeneratedRegex(@"\b(DLMux|DL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternDLMux(); // Italian specific

    [GeneratedRegex(@"\b(BluRay|BDRip|BRRip)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternBluRay();

    [GeneratedRegex(@"\b(DVDRip|DVD)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternDVD();

    [GeneratedRegex(@"\b(x264|H\.264|AVC)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternX264();

    [GeneratedRegex(@"\b(x265|H\.265|HEVC|h264)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternX265(); // h264 variant seen in data

    [GeneratedRegex(@"\b(XviD|DivX)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualityPatternXviD();

    // Quality patterns array (initialized from source-generated regexes)
    private static readonly Regex[] QualityPatterns = new[]
    {
        // Resolution patterns
        QualityPattern4K(),
        QualityPattern1080p(),
        QualityPattern720p(),
        QualityPattern480p(),
        // Source patterns
        QualityPatternWebMux(),
        QualityPatternHDTV(),
        QualityPatternDLMux(),
        QualityPatternBluRay(),
        QualityPatternDVD(),
        // Codec patterns
        QualityPatternX264(),
        QualityPatternX265(),
        QualityPatternXviD()
    };

    // Source-generated language patterns specific to Italian content
    [GeneratedRegex(@"\b(ITA|iTALiAN|ITALIAN)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePatternItalian(); // Most common

    [GeneratedRegex(@"\b(ITA_ENG|ENG_ITA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePatternDual(); // Dual language

    [GeneratedRegex(@"\b(ENG|EN|ENGLISH)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePatternEnglish();

    [GeneratedRegex(@"\b(SUB|SUBS|SUBTITLES|forced)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePatternSubtitles(); // Subtitle indicators

    [GeneratedRegex(@"\b(DUB|DUBBED)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePatternDubbed();

    // Language patterns array (initialized from source-generated regexes)
    private static readonly Regex[] LanguagePatterns = new[]
    {
        LanguagePatternItalian(),
        LanguagePatternDual(),
        LanguagePatternEnglish(),
        LanguagePatternSubtitles(),
        LanguagePatternDubbed()
    };

    // Source-generated release patterns and groups (observed in Italian data)
    [GeneratedRegex(@"\b(REPACK|PROPER|REAL|FINAL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReleasePatternRepack();

    [GeneratedRegex(@"\b(EXTENDED|UNCUT|DIRECTORS?\.CUT)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReleasePatternExtended();

    [GeneratedRegex(@"\b(LIMITED|INTERNAL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReleasePatternLimited();

    [GeneratedRegex(@"\b(UBi|NovaRip|DarkSideMux|Pir8|iGM)\b")]
    private static partial Regex ReleasePatternItalianGroups(); // Common in Italian data

    [GeneratedRegex(@"-([A-Za-z0-9]+)$")]
    private static partial Regex ReleasePatternGenericGroup(); // General release group pattern

    // Release patterns array (initialized from source-generated regexes)
    private static readonly Regex[] ReleasePatterns = new[]
    {
        ReleasePatternRepack(),
        ReleasePatternExtended(),
        ReleasePatternLimited(),
        ReleasePatternItalianGroups(),
        ReleasePatternGenericGroup()
    };

    // Source-generated utility patterns
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesPattern(); // For normalizing whitespace

    [GeneratedRegex(@"-([A-Za-z0-9]+)(?:\.[a-z]+)?$")]
    private static partial Regex GenericReleaseGroupPattern(); // For extracting release groups

    // Common separators in Italian filenames
    private static readonly char[] CommonSeparators = { '.', '_', '-', ' ' };

    // Words to remove during tokenization (Italian-specific stopwords)
    // Note: Very conservative list - only remove clearly technical/noise words
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Only technical terms that are clearly not part of series names
        "pack", "complete", "season", "vol", "volume",
        
        // Common English prepositions that may be removed in some cases
        "of"
    };

    public TokenizerService(ILogger<TokenizerService> logger)
    {
        _logger = logger;
    }

    public Result<string> ExtractSeriesName(string filename)
    {
        try
        {
            if (string.IsNullOrEmpty(filename))
            {
                return Result<string>.Failure("Filename cannot be null or empty");
            }
            
            if (string.IsNullOrWhiteSpace(filename))
            {
                return Result<string>.Failure("Could not extract a valid series name");
            }

            _logger.LogDebug("Extracting series name from: {Filename}", filename);

            // Remove file extension
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            
            // Find episode pattern and extract everything before it
            var seriesName = ExtractSeriesNameBeforeEpisode(nameWithoutExtension);
            
            if (string.IsNullOrWhiteSpace(seriesName))
            {
                // If no episode pattern found, try to clean the entire filename
                seriesName = CleanFilenameForSeriesName(nameWithoutExtension);
            }

            var cleanedName = CleanAndNormalizeSeriesName(seriesName);
            
            if (string.IsNullOrWhiteSpace(cleanedName))
            {
                return Result<string>.Failure("Could not extract a valid series name");
            }

            _logger.LogDebug("Extracted series name: '{SeriesName}' from '{Filename}'", cleanedName, filename);
            return Result<string>.Success(cleanedName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting series name from filename: {Filename}", filename);
            return Result<string>.Failure($"Error extracting series name: {ex.Message}");
        }
    }

    public Result<TokenizedFilename> TokenizeFilename(string filename)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return Result<TokenizedFilename>.Failure("Filename cannot be null or empty");
            }

            _logger.LogDebug("Tokenizing filename: {Filename}", filename);

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var extension = Path.GetExtension(filename).TrimStart('.');

            // Extract all components
            var seriesNameResult = ExtractSeriesName(filename);
            var episodeInfo = ExtractEpisodeInfoInternal(nameWithoutExtension);
            // var qualityInfo = ExtractQualityInfoInternal(nameWithoutExtension);

            // Tokenize the filename
            var allTokens = TokenizeString(nameWithoutExtension);
            var seriesTokens = seriesNameResult.IsSuccess ? 
                TokenizeString(seriesNameResult.Value) : new List<string>();

            // Identify filtered tokens (quality, language, release info)
            var filteredTokens = IdentifyFilteredTokens(nameWithoutExtension);

            // Extract metadata
            var metadata = ExtractMetadata(nameWithoutExtension);

            var result = new TokenizedFilename
            {
                OriginalFilename = filename,
                SeriesTokens = seriesTokens.AsReadOnly(),
                AllTokens = allTokens.AsReadOnly(),
                FilteredTokens = filteredTokens.AsReadOnly(),
                FileExtension = extension,
                EpisodeInfo = episodeInfo,
                // QualityInfo = qualityInfo,
                ReleaseGroup = ExtractReleaseGroup(nameWithoutExtension),
                Metadata = metadata.AsReadOnly()
            };

            _logger.LogDebug("Tokenized filename: {TokenCount} total tokens, {SeriesTokenCount} series tokens", 
                allTokens.Count, seriesTokens.Count);

            return Result<TokenizedFilename>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tokenizing filename: {Filename}", filename);
            return Result<TokenizedFilename>.Failure($"Error tokenizing filename: {ex.Message}");
        }
    }

    public Result<EpisodeInfo> ExtractEpisodeInfo(string filename)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return Result<EpisodeInfo>.Failure("Filename cannot be null or empty");
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var episodeInfo = ExtractEpisodeInfoInternal(nameWithoutExtension);

            if (episodeInfo == null)
            {
                return Result<EpisodeInfo>.Failure("No episode information found in filename");
            }

            return Result<EpisodeInfo>.Success(episodeInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting episode info from filename: {Filename}", filename);
            return Result<EpisodeInfo>.Failure($"Error extracting episode info: {ex.Message}");
        }
    }

    // public Result<QualityInfo> ExtractQualityInfo(string filename)
    // {
    //     try
    //     {
    //         if (string.IsNullOrWhiteSpace(filename))
    //         {
    //             return Result<QualityInfo>.Failure("Filename cannot be null or empty");
    //         }
    //
    //         var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
    //         var qualityInfo = ExtractQualityInfoInternal(nameWithoutExtension);
    //
    //         return Result<QualityInfo>.Success(qualityInfo);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error extracting quality info from filename: {Filename}", filename);
    //         return Result<QualityInfo>.Failure($"Error extracting quality info: {ex.Message}");
    //     }
    // }

    // Private helper methods

    private string ExtractSeriesNameBeforeEpisode(string nameWithoutExtension)
    {
        foreach (var pattern in EpisodePatterns)
        {
            var match = pattern.Match(nameWithoutExtension);
            if (match.Success)
            {
                // Extract everything before the episode pattern
                var seriesPart = nameWithoutExtension.Substring(0, match.Index).Trim();
                return seriesPart;
            }
        }

        return string.Empty;
    }

    private string CleanFilenameForSeriesName(string input)
    {
        // Remove quality indicators, language codes, and release patterns
        var cleaned = input;

        // Remove quality patterns
        foreach (var pattern in QualityPatterns)
        {
            cleaned = pattern.Replace(cleaned, " ");
        }

        // Remove language patterns
        foreach (var pattern in LanguagePatterns)
        {
            cleaned = pattern.Replace(cleaned, " ");
        }

        // Remove release patterns
        foreach (var pattern in ReleasePatterns)
        {
            cleaned = pattern.Replace(cleaned, " ");
        }

        return cleaned;
    }

    private string CleanAndNormalizeSeriesName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var cleaned = input;

        // Replace separators with spaces
        foreach (var separator in CommonSeparators)
        {
            cleaned = cleaned.Replace(separator, ' ');
        }

        // Remove multiple spaces and normalize (using source-generated regex)
        cleaned = MultipleSpacesPattern().Replace(cleaned, " ").Trim();

        // Split into words and filter - keep important single letters like "A" in series names
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !StopWords.Contains(word))
            .ToList();

        // Capitalize all words with title case for series names
        var capitalizedWords = words.Select(word =>
        {
            // Preserve all-caps words like NCIS, FBI, CSI, etc.
            if (word.Length <= 4 && word.All(c => char.IsUpper(c) || char.IsDigit(c)))
                return word.ToUpperInvariant();
            
            // Title case for everything else
            return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
        }).ToList();

        var result = string.Join(" ", capitalizedWords);
        return result.Length > 100 ? result.Substring(0, 100).Trim() : result; // Reasonable length limit
    }

    private static string CapitalizeWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return word;

        // Handle common Italian/English patterns
        if (word.Length == 1)
            return word.ToUpperInvariant();

        // Keep Italian articles and prepositions lowercase if they're not the first word
        var italianArticles = new[] { "di", "del", "della", "dello", "dei", "delle", "of", "the" };
        if (italianArticles.Contains(word, StringComparer.OrdinalIgnoreCase))
            return word.ToLowerInvariant();

        // Preserve all-caps words like NCIS, FBI, CSI, etc.
        if (word.Length <= 4 && word.All(c => char.IsUpper(c) || char.IsDigit(c)))
            return word.ToUpperInvariant();

        return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
    }

    private EpisodeInfo? ExtractEpisodeInfoInternal(string nameWithoutExtension)
    {
        for (int i = 0; i < EpisodePatterns.Length; i++)
        {
            var pattern = EpisodePatterns[i];
            var match = pattern.Match(nameWithoutExtension);
            
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
                        PatternType = GetPatternType(i),
                        AdditionalInfo = ExtractEpisodeTitle(nameWithoutExtension, match)
                    };
                }
            }
        }

        return null;
    }

    // private QualityInfo ExtractQualityInfoInternal(string nameWithoutExtension)
    // {
    //     var resolution = ExtractFirstMatch(nameWithoutExtension, QualityPatterns.Take(4).ToArray());
    //     var source = ExtractFirstMatch(nameWithoutExtension, QualityPatterns.Skip(4).Take(5).ToArray());
    //     var codec = ExtractFirstMatch(nameWithoutExtension, QualityPatterns.Skip(9).ToArray());
    //
    //     var languageIndicators = new List<string>();
    //     foreach (var pattern in LanguagePatterns)
    //     {
    //         var matches = pattern.Matches(nameWithoutExtension);
    //         languageIndicators.AddRange(matches.Select(m => m.Value.ToUpperInvariant()));
    //     }
    //
    //     var additionalIndicators = new List<string>();
    //     foreach (var pattern in ReleasePatterns.Take(3)) // Only quality-related release patterns
    //     {
    //         var matches = pattern.Matches(nameWithoutExtension);
    //         additionalIndicators.AddRange(matches.Select(m => m.Value.ToUpperInvariant()));
    //     }
    //
    //     return new QualityInfo
    //     {
    //         Resolution = resolution,
    //         VideoCodec = codec,
    //         Source = source,
    //         QualityTier = DetermineQualityTier(resolution, source),
    //         AdditionalIndicators = additionalIndicators.AsReadOnly(),
    //         LanguageCodes = languageIndicators.AsReadOnly()
    //     };
    // }

    private List<string> TokenizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();

        var tokens = new List<string>();
        
        // Replace separators with spaces
        var normalized = input;
        foreach (var separator in CommonSeparators)
        {
            normalized = normalized.Replace(separator, ' ');
        }

        // Split and filter tokens
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 1) // Minimum token length
            .Select(token => token.ToLowerInvariant())
            .Where(token => !StopWords.Contains(token));

        tokens.AddRange(words);
        return tokens;
    }

    private List<string> IdentifyFilteredTokens(string nameWithoutExtension)
    {
        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add quality indicators
        foreach (var pattern in QualityPatterns)
        {
            var matches = pattern.Matches(nameWithoutExtension);
            foreach (Match match in matches)
            {
                filtered.Add(match.Value);
            }
        }

        // Add language indicators
        foreach (var pattern in LanguagePatterns)
        {
            var matches = pattern.Matches(nameWithoutExtension);
            foreach (Match match in matches)
            {
                filtered.Add(match.Value);
            }
        }

        // Add release patterns
        foreach (var pattern in ReleasePatterns)
        {
            var matches = pattern.Matches(nameWithoutExtension);
            foreach (Match match in matches)
            {
                filtered.Add(match.Value);
            }
        }

        return filtered.ToList();
    }

    private Dictionary<string, string> ExtractMetadata(string nameWithoutExtension)
    {
        var metadata = new Dictionary<string, string>();

        // Add language information
        var languages = new List<string>();
        foreach (var pattern in LanguagePatterns)
        {
            var matches = pattern.Matches(nameWithoutExtension);
            foreach (Match match in matches)
            {
                languages.Add(match.Value.ToUpperInvariant());
            }
        }
        if (languages.Count > 0)
        {
            metadata["Languages"] = string.Join(", ", languages.Distinct());
        }

        // Add release group
        var releaseGroup = ExtractReleaseGroup(nameWithoutExtension);
        if (!string.IsNullOrWhiteSpace(releaseGroup))
        {
            metadata["ReleaseGroup"] = releaseGroup;
        }

        // Add processing hints
        if (nameWithoutExtension.Contains("forced", StringComparison.OrdinalIgnoreCase))
        {
            metadata["SubtitleType"] = "Forced";
        }

        return metadata;
    }

    private string? ExtractReleaseGroup(string nameWithoutExtension)
    {
        // Try specific Italian release groups first
        var italianGroups = new[] { "UBi", "NovaRip", "DarkSideMux", "Pir8", "iGM" };
        foreach (var group in italianGroups)
        {
            if (nameWithoutExtension.Contains(group, StringComparison.OrdinalIgnoreCase))
            {
                return group;
            }
        }

        // Try general release group pattern (ends with -GROUP) using source-generated regex
        var match = GenericReleaseGroupPattern().Match(nameWithoutExtension);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    private string? ExtractFirstMatch(string input, Regex[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = pattern.Match(input);
            if (match.Success)
            {
                return match.Value;
            }
        }
        return null;
    }

    // private static QualityTier DetermineQualityTier(string? resolution, string? source)
    // {
    //     return resolution?.ToUpperInvariant() switch
    //     {
    //         "2160P" or "4K" or "UHD" => QualityTier.Premium,
    //         "1080P" or "FHD" => source?.ToUpperInvariant() switch
    //         {
    //             "BLURAY" or "BDRip" => QualityTier.UltraHigh,
    //             "WEBMUX" or "WEB-DL" or "WEBDL" or "WEB-DLMUX" => QualityTier.High,
    //             _ => QualityTier.High
    //         },
    //         "720P" or "HD" => QualityTier.Standard,
    //         "480P" or "SD" => QualityTier.Low,
    //         _ => QualityTier.Unknown
    //     };
    // }

    private static EpisodePatternType GetPatternType(int patternIndex) => patternIndex switch
    {
        0 => EpisodePatternType.Alternative, // #x## pattern (most common in Italian data)
        1 => EpisodePatternType.Standard,    // S##E## pattern
        2 => EpisodePatternType.Verbose,     // Season # Episode # pattern
        3 => EpisodePatternType.EpisodeOnly, // E## pattern
        4 => EpisodePatternType.DateBased,   // Date pattern
        _ => EpisodePatternType.None
    };

    private string? ExtractEpisodeTitle(string nameWithoutExtension, Match episodeMatch)
    {
        try
        {
            // Look for episode title after the episode pattern
            var afterEpisode = nameWithoutExtension.Substring(episodeMatch.Index + episodeMatch.Length);
            
            // Clean up the potential title
            var tokens = afterEpisode.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Simple heuristic: title parts are usually tokens that are not quality/language indicators
            var titleParts = tokens.Where(t => t.Length >= 1 && !IsQualityOrLanguageIndicator(t)).ToList();

            if (titleParts.Count == 0) return null;

            var title = string.Join(" ", titleParts.Select(CapitalizeWord)).Trim();
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch
        {
            return null;
        }
    }

    private bool IsQualityOrLanguageIndicator(string token)
    {
        return QualityPatterns.Any(pattern => pattern.IsMatch(token)) ||
               LanguagePatterns.Any(pattern => pattern.IsMatch(token)) ||
               ReleasePatterns.Any(pattern => pattern.IsMatch(token));
    }
}