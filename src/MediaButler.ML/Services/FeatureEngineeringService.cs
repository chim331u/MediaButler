using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediaButler.ML.Configuration;

namespace MediaButler.ML.Services;

/// <summary>
/// Implementation of feature engineering service for ML classification.
/// This service transforms tokenized filename data into comprehensive feature vectors.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles feature extraction from tokens
/// - No complecting: Separate from tokenization, classification, and domain logic
/// - Values over state: Pure functions transforming tokens to features
/// - Compose don't complex: Independent feature extractors composed together
/// - Declarative: Clear feature extraction without complex state management
/// </remarks>
public class FeatureEngineeringService : IFeatureEngineeringService
{
    private readonly ILogger<FeatureEngineeringService> _logger;
    private readonly MLConfiguration _config;

    // Known Italian release groups from training data analysis
    private static readonly HashSet<string> KnownItalianReleaseGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "NovaRip", "DarkSideMux", "Pir8", "iGM", "UBi", "NTb", "MIXED", "IGM", 
        "BaMax", "FoV", "KILLERS", "LOL", "DIMENSION", "SVA", "AFG"
    };

    // High-value tokens that are strongly discriminative
    private static readonly HashSet<string> HighValueTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        // Italian series indicators
        "trono", "spade", "giganti", "attacco", "piece", "hero", "academia",
        
        // Quality indicators  
        "1080p", "720p", "webmux", "hdtv", "bluray", "x264", "x265",
        
        // Language indicators
        "ita", "italian", "eng", "sub", "dub"
    };

    public FeatureEngineeringService(
        ILogger<FeatureEngineeringService> logger,
        IOptions<MLConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public Result<FeatureVector> ExtractFeatures(TokenizedFilename tokenizedFilename)
    {
        try
        {
            if (tokenizedFilename == null)
                return Result<FeatureVector>.Failure("Tokenized filename cannot be null");

            _logger.LogDebug("Extracting features from tokenized filename: {Filename}", 
                tokenizedFilename.OriginalFilename);

            // Extract all feature components
            var tokenAnalysisResult = AnalyzeTokenFrequency(tokenizedFilename.SeriesTokens);
            if (!tokenAnalysisResult.IsSuccess)
                return Result<FeatureVector>.Failure($"Token analysis failed: {tokenAnalysisResult.Error}");

            var ngramResult = GenerateNGrams(tokenizedFilename.AllTokens, 2); // Bigrams
            if (!ngramResult.IsSuccess)
                return Result<FeatureVector>.Failure($"N-gram generation failed: {ngramResult.Error}");

            var qualityFeaturesResult = tokenizedFilename.QualityInfo != null 
                ? ExtractQualityFeatures(tokenizedFilename.QualityInfo)
                : Result<QualityFeatures>.Success(CreateDefaultQualityFeatures());
            if (!qualityFeaturesResult.IsSuccess)
                return Result<FeatureVector>.Failure($"Quality feature extraction failed: {qualityFeaturesResult.Error}");

            // TODO: Fix PatternMatchingFeatures static context issues in future sprint
            var patternFeatures = CreateSimplePatternFeatures(tokenizedFilename.OriginalFilename);

            // Extract optional features
            EpisodeFeatures? episodeFeatures = null;
            if (tokenizedFilename.EpisodeInfo != null)
            {
                episodeFeatures = EpisodeFeatures.FromEpisodeInfo(tokenizedFilename.EpisodeInfo);
            }

            ReleaseGroupFeatures? releaseGroupFeatures = null;
            if (!string.IsNullOrEmpty(tokenizedFilename.ReleaseGroup))
            {
                releaseGroupFeatures = ReleaseGroupFeatures.FromReleaseGroupName(tokenizedFilename.ReleaseGroup);
            }

            // Combine all features into vector
            var featureVector = new FeatureVector
            {
                OriginalFilename = tokenizedFilename.OriginalFilename,
                TokenFeatures = tokenAnalysisResult.Value,
                NGramFeatures = ngramResult.Value,
                QualityFeatures = qualityFeaturesResult.Value,
                PatternFeatures = patternFeatures,
                EpisodeFeatures = episodeFeatures,
                ReleaseGroupFeatures = releaseGroupFeatures,
                ExtractedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Extracted {FeatureCount} features from filename: {Filename}",
                featureVector.FeatureCount, tokenizedFilename.OriginalFilename);

            return Result<FeatureVector>.Success(featureVector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting features from filename: {Filename}", 
                tokenizedFilename?.OriginalFilename ?? "unknown");
            return Result<FeatureVector>.Failure($"Feature extraction error: {ex.Message}");
        }
    }

    public Result<TokenFrequencyAnalysis> AnalyzeTokenFrequency(IReadOnlyList<string> seriesTokens)
    {
        try
        {
            if (seriesTokens == null || !seriesTokens.Any())
                return Result<TokenFrequencyAnalysis>.Failure("Series tokens cannot be null or empty");

            _logger.LogDebug("Analyzing token frequency for {TokenCount} tokens", seriesTokens.Count);

            // Count token frequencies
            var tokenCounts = seriesTokens.GroupBy(t => t.ToLowerInvariant())
                                         .ToDictionary(g => g.Key, g => g.Count());

            var totalTokens = seriesTokens.Count;
            var uniqueTokens = tokenCounts.Count;
            
            // Calculate importance scores
            var frequentTokens = tokenCounts.OrderByDescending(kvp => kvp.Value)
                                           .Take(10)
                                           .Select(kvp => CreateTokenFrequency(kvp.Key, kvp.Value, totalTokens))
                                           .ToList();

            var rareTokens = tokenCounts.Where(kvp => kvp.Value == 1)
                                       .Take(5)
                                       .Select(kvp => CreateTokenFrequency(kvp.Key, kvp.Value, totalTokens))
                                       .ToList();

            // Calculate metrics
            var avgTokenLength = seriesTokens.Average(t => t.Length);
            var alphaTokens = seriesTokens.Count(t => t.All(char.IsLetter));
            var numericTokens = seriesTokens.Count(t => t.Any(char.IsDigit));
            var alphaNumericRatio = numericTokens > 0 ? (double)alphaTokens / numericTokens : double.MaxValue;
            
            // Diversity score (Shannon entropy approximation)
            var diversityScore = CalculateDiversityScore(tokenCounts, totalTokens);
            
            // Language indicators
            var languageIndicators = DetectLanguageIndicators(seriesTokens);

            var analysis = new TokenFrequencyAnalysis
            {
                TotalTokens = totalTokens,
                FrequentTokens = frequentTokens.AsReadOnly(),
                RareTokens = rareTokens.AsReadOnly(),
                AverageTokenLength = avgTokenLength,
                AlphaNumericRatio = Math.Min(alphaNumericRatio, 10.0), // cap for stability
                DiversityScore = diversityScore,
                LanguageIndicators = languageIndicators.AsReadOnly()
            };

            return Result<TokenFrequencyAnalysis>.Success(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing token frequency");
            return Result<TokenFrequencyAnalysis>.Failure($"Token frequency analysis error: {ex.Message}");
        }
    }

    public Result<IReadOnlyList<NGramFeature>> GenerateNGrams(IReadOnlyList<string> tokens, int n)
    {
        try
        {
            if (tokens == null || !tokens.Any())
                return Result<IReadOnlyList<NGramFeature>>.Failure("Tokens cannot be null or empty");

            if (n < 1 || n > 5)
                return Result<IReadOnlyList<NGramFeature>>.Failure("N-gram size must be between 1 and 5");

            _logger.LogDebug("Generating {N}-grams from {TokenCount} tokens", n, tokens.Count);

            var ngrams = new List<NGramFeature>();

            // Generate N-grams
            for (int i = 0; i <= tokens.Count - n; i++)
            {
                var ngramTokens = tokens.Skip(i).Take(n).ToList();
                var context = DetermineNGramContext(ngramTokens);
                var discriminativePower = CalculateDiscriminativePower(ngramTokens);
                var isCrossBoundary = DetermineIfCrossBoundary(ngramTokens, context);

                var ngram = new NGramFeature
                {
                    N = n,
                    Tokens = ngramTokens.AsReadOnly(),
                    Frequency = 1, // Single occurrence in this filename
                    RelativeFrequency = 1.0 / (tokens.Count - n + 1), // Relative to possible N-grams
                    DiscriminativePower = discriminativePower,
                    Context = context,
                    IsCrossBoundary = isCrossBoundary
                };

                ngrams.Add(ngram);
            }

            // Remove duplicate N-grams and aggregate frequencies
            var uniqueNGrams = ngrams.GroupBy(ng => ng.NGramText.ToLowerInvariant())
                                    .Select(g => CreateAggregatedNGram(g.ToList()))
                                    .OrderByDescending(ng => ng.DiscriminativePower)
                                    .Take(Math.Min(20, ngrams.Count)) // Limit for performance
                                    .ToList();

            return Result<IReadOnlyList<NGramFeature>>.Success(uniqueNGrams.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating N-grams");
            return Result<IReadOnlyList<NGramFeature>>.Failure($"N-gram generation error: {ex.Message}");
        }
    }

    public Result<QualityFeatures> ExtractQualityFeatures(QualityInfo qualityInfo)
    {
        try
        {
            if (qualityInfo == null)
                return Result<QualityFeatures>.Failure("Quality info cannot be null");

            _logger.LogDebug("Extracting quality features from quality info: {Resolution}, {Source}, {VideoCodec}",
                qualityInfo.Resolution, qualityInfo.Source, qualityInfo.VideoCodec);

            var qualityFeatures = QualityFeatures.FromQualityInfo(qualityInfo);
            
            return Result<QualityFeatures>.Success(qualityFeatures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting quality features");
            return Result<QualityFeatures>.Failure($"Quality feature extraction error: {ex.Message}");
        }
    }

    public Result<PatternMatchingFeatures> ExtractPatternFeatures(string originalFilename)
    {
        try
        {
            if (string.IsNullOrEmpty(originalFilename))
                return Result<PatternMatchingFeatures>.Failure("Filename cannot be null or empty");

            _logger.LogDebug("Extracting pattern features from filename: {Filename}", originalFilename);

            // Use simplified pattern features for now
            var patternFeatures = CreateSimplePatternFeatures(originalFilename);
            
            return Result<PatternMatchingFeatures>.Success(patternFeatures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting pattern features from filename: {Filename}", originalFilename);
            return Result<PatternMatchingFeatures>.Failure($"Pattern feature extraction error: {ex.Message}");
        }
    }

    // Private helper methods

    private TokenFrequency CreateTokenFrequency(string token, int count, int totalTokens)
    {
        var relativeFreq = (double)count / totalTokens;
        var importanceScore = CalculateTokenImportance(token, count, totalTokens);
        var category = CategorizeToken(token);
        var isCommon = IsCommonAcrossSeries(token);

        return new TokenFrequency
        {
            Token = token,
            Count = count,
            RelativeFrequency = relativeFreq,
            ImportanceScore = importanceScore,
            Category = category,
            IsCommonAcrossSeries = isCommon
        };
    }

    private double CalculateTokenImportance(string token, int count, int totalTokens)
    {
        var baseScore = Math.Log(count + 1); // Log frequency
        
        // Boost for high-value tokens
        if (HighValueTokens.Contains(token))
            baseScore *= 2.0;
            
        // Boost for longer, more specific tokens
        if (token.Length > 5)
            baseScore *= 1.5;
            
        // Penalize very common words
        if (IsVeryCommonWord(token))
            baseScore *= 0.5;

        return Math.Min(baseScore, 10.0); // Cap at 10
    }

    private string CategorizeToken(string token)
    {
        var lowerToken = token.ToLowerInvariant();
        
        // Quality indicators
        if (lowerToken.Contains("1080") || lowerToken.Contains("720") || 
            lowerToken.Contains("web") || lowerToken.Contains("hdtv"))
            return "quality";
            
        // Language indicators  
        if (lowerToken.Contains("ita") || lowerToken.Contains("eng") || 
            lowerToken.Contains("sub") || lowerToken.Contains("dub"))
            return "language";
            
        // Episode indicators
        if (char.IsDigit(lowerToken[0]) || lowerToken.Contains("season") || lowerToken.Contains("episode"))
            return "episode";
            
        // Default to series name
        return "series_name";
    }

    private bool IsCommonAcrossSeries(string token)
    {
        // Very common words that appear in many different series
        var commonWords = new[] { "the", "and", "of", "in", "la", "il", "di", "e", "season", "episode" };
        return commonWords.Contains(token.ToLowerInvariant());
    }

    private bool IsVeryCommonWord(string token)
    {
        var veryCommon = new[] { "the", "and", "of", "in", "on", "at", "to", "for", "with", "by" };
        return veryCommon.Contains(token.ToLowerInvariant());
    }

    private double CalculateDiversityScore(Dictionary<string, int> tokenCounts, int totalTokens)
    {
        // Shannon entropy as diversity measure
        var entropy = 0.0;
        foreach (var count in tokenCounts.Values)
        {
            var probability = (double)count / totalTokens;
            entropy -= probability * Math.Log2(probability);
        }
        
        // Normalize to 0-1 range (max entropy for uniform distribution)
        var maxEntropy = Math.Log2(tokenCounts.Count);
        return maxEntropy > 0 ? entropy / maxEntropy : 0.0;
    }

    private List<string> DetectLanguageIndicators(IReadOnlyList<string> tokens)
    {
        var indicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var languagePatterns = new[] { "ita", "italian", "eng", "english", "sub", "dub", "multi" };
        
        foreach (var token in tokens)
        {
            foreach (var pattern in languagePatterns)
            {
                if (token.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    indicators.Add(pattern);
                }
            }
        }
        
        return indicators.ToList();
    }

    private NGramContext DetermineNGramContext(List<string> ngramTokens)
    {
        var text = string.Join(" ", ngramTokens).ToLowerInvariant();
        
        // Quality context
        if (text.Contains("1080") || text.Contains("720") || text.Contains("web") || text.Contains("hdtv"))
            return NGramContext.Quality;
            
        // Episode context
        if (text.Any(char.IsDigit) && (text.Contains("x") || text.Contains("season") || text.Contains("episode")))
            return NGramContext.Episode;
            
        // Language context
        if (text.Contains("ita") || text.Contains("eng") || text.Contains("sub"))
            return NGramContext.Language;
            
        // Technical context
        if (text.Contains("x264") || text.Contains("x265") || text.Contains("mux"))
            return NGramContext.Technical;
            
        // Default to series name
        return NGramContext.SeriesName;
    }

    private double CalculateDiscriminativePower(List<string> ngramTokens)
    {
        var power = 0.5; // Base power
        
        // Higher power for longer N-grams
        power += ngramTokens.Count * 0.1;
        
        // Higher power for high-value tokens
        foreach (var token in ngramTokens)
        {
            if (HighValueTokens.Contains(token))
                power += 0.3;
        }
        
        // Lower power for very common combinations
        var text = string.Join(" ", ngramTokens).ToLowerInvariant();
        if (text.Contains("the") || text.Contains("and") || text.Contains("of"))
            power -= 0.2;
            
        return Math.Max(0.1, Math.Min(1.0, power));
    }

    private bool DetermineIfCrossBoundary(List<string> ngramTokens, NGramContext context)
    {
        // Check if N-gram spans different semantic categories
        var contexts = ngramTokens.Select(token => DetermineNGramContext(new List<string> { token })).ToList();
        return contexts.Distinct().Count() > 1;
    }

    private NGramFeature CreateAggregatedNGram(List<NGramFeature> duplicates)
    {
        var first = duplicates.First();
        var totalFreq = duplicates.Sum(ng => ng.Frequency);
        var avgDiscriminative = duplicates.Average(ng => ng.DiscriminativePower);
        var avgRelativeFreq = duplicates.Average(ng => ng.RelativeFrequency);

        return new NGramFeature
        {
            N = first.N,
            Tokens = first.Tokens,
            Frequency = totalFreq,
            RelativeFrequency = avgRelativeFreq,
            DiscriminativePower = avgDiscriminative,
            Context = first.Context,
            IsCrossBoundary = first.IsCrossBoundary
        };
    }

    private static PatternMatchingFeatures CreateSimplePatternFeatures(string filename)
    {
        // Simplified pattern features for now - full implementation in future sprint
        return new PatternMatchingFeatures
        {
            PatternType = FilenamePatternType.Simple,
            StructureComplexity = Math.Min(filename.Count(c => "._-".Contains(c)) / 2, 10),
            SeparatorCount = filename.Count(c => "._- ".Contains(c)),
            AlphaNumericRatio = CalculateSimpleAlphaNumericRatio(filename),
            ContainsYear = System.Text.RegularExpressions.Regex.IsMatch(filename, @"\b(19|20)\d{2}\b"),
            ContainsEpisodePattern = System.Text.RegularExpressions.Regex.IsMatch(filename, @"[Ss]\d{1,2}[Ee]\d{1,2}|\d{1,2}x\d{1,2}"),
            ContainsQualityPattern = System.Text.RegularExpressions.Regex.IsMatch(filename, @"\b(1080p|720p|480p|2160p|4K)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            ContainsLanguagePattern = System.Text.RegularExpressions.Regex.IsMatch(filename, @"\b(ITA|ENG|SUB|DUB)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            ContainsReleaseGroupPattern = filename.Contains('[') || filename.Contains('-'),
            DetectedPatterns = new List<DetectedPattern>().AsReadOnly(),
            PatternConfidence = 0.5,
            LengthCategory = filename.Length switch
            {
                < 20 => FilenameLengthCategory.Short,
                < 50 => FilenameLengthCategory.Medium,
                < 100 => FilenameLengthCategory.Long,
                _ => FilenameLengthCategory.VeryLong
            }
        };
    }

    private static double CalculateSimpleAlphaNumericRatio(string filename)
    {
        var alphaCount = filename.Count(char.IsLetter);
        var numCount = filename.Count(char.IsDigit);
        return numCount > 0 ? (double)alphaCount / numCount : 10.0; // cap for stability
    }

    private static QualityFeatures CreateDefaultQualityFeatures()
    {
        return new QualityFeatures
        {
            ResolutionTier = QualityTier.Unknown,
            SourceTier = QualityTier.Unknown,
            VideoCodec = null,
            AudioCodec = null,
            Resolution = null,
            Source = null,
            HasHDR = false,
            HasMultipleAudio = false,
            QualityScore = 0
        };
    }
}