using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Interface for filename tokenization and series name extraction.
/// This service operates independently of domain concerns, focusing solely on filename analysis.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only tokenizes filenames
/// - No complecting: Separate from domain business logic
/// - Values over state: Pure functions with immutable inputs/outputs
/// </remarks>
public interface ITokenizerService
{
    /// <summary>
    /// Extracts series name from filename by identifying and removing episode markers, quality indicators, and release tags.
    /// </summary>
    /// <param name="filename">The filename to analyze (without path)</param>
    /// <returns>Result containing the extracted series name or error information</returns>
    /// <example>
    /// Input: "The.Walking.Dead.S11E24.FINAL.ITA.ENG.1080p.mkv"
    /// Output: "The Walking Dead"
    /// </example>
    Result<string> ExtractSeriesName(string filename);

    /// <summary>
    /// Tokenizes filename into meaningful components for ML feature extraction.
    /// </summary>
    /// <param name="filename">The filename to tokenize</param>
    /// <returns>Result containing tokenized components or error information</returns>
    Result<TokenizedFilename> TokenizeFilename(string filename);

    /// <summary>
    /// Extracts episode information (season/episode numbers) from filename.
    /// </summary>
    /// <param name="filename">The filename to analyze</param>
    /// <returns>Result containing episode information or error if not found</returns>
    Result<EpisodeInfo> ExtractEpisodeInfo(string filename);

    /// <summary>
    /// Identifies quality indicators in filename (resolution, codec, source).
    /// </summary>
    /// <param name="filename">The filename to analyze</param>
    /// <returns>Result containing quality information or error information</returns>
    Result<QualityInfo> ExtractQualityInfo(string filename);
}