using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Service for extracting ML features from tokenized filename data.
/// This service operates independently of both tokenization and classification concerns.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles feature extraction from tokens
/// - No complecting: Separate from tokenization, classification, and domain logic
/// - Values over state: Pure functions that transform tokens to features
/// - Compose don't complex: Independent feature extractors that can be composed
/// </remarks>
public interface IFeatureEngineeringService
{
    /// <summary>
    /// Extracts comprehensive feature set from tokenized filename data.
    /// </summary>
    /// <param name="tokenizedFilename">The tokenized filename containing all extracted information</param>
    /// <returns>Feature vector suitable for ML classification</returns>
    Result<FeatureVector> ExtractFeatures(TokenizedFilename tokenizedFilename);

    /// <summary>
    /// Analyzes token frequencies to identify important discriminative features.
    /// </summary>
    /// <param name="seriesTokens">Tokens representing the series name</param>
    /// <returns>Token frequency analysis results</returns>
    Result<TokenFrequencyAnalysis> AnalyzeTokenFrequency(IReadOnlyList<string> seriesTokens);

    /// <summary>
    /// Generates N-grams from token sequences for context-aware features.
    /// </summary>
    /// <param name="tokens">Input token sequence</param>
    /// <param name="n">N-gram size (1 for unigrams, 2 for bigrams, etc.)</param>
    /// <returns>Generated N-grams with frequency information</returns>
    Result<IReadOnlyList<NGramFeature>> GenerateNGrams(IReadOnlyList<string> tokens, int n);

    /// <summary>
    /// Extracts quality-based features for classification.
    /// </summary>
    /// <param name="qualityInfo">Quality information from tokenization</param>
    /// <returns>Quality feature representation</returns>
    Result<QualityFeatures> ExtractQualityFeatures(QualityInfo qualityInfo);

    /// <summary>
    /// Generates regex pattern matching features for filename structure analysis.
    /// </summary>
    /// <param name="originalFilename">Original filename to analyze</param>
    /// <returns>Pattern matching feature set</returns>
    Result<PatternMatchingFeatures> ExtractPatternFeatures(string originalFilename);
}