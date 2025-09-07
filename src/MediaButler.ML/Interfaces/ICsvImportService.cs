using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Interface for importing training data from CSV files.
/// This service handles CSV parsing and validation without domain coupling.
/// </summary>
/// <remarks>
/// Expected CSV Format: id;Category;FileName
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles CSV import
/// - No complecting: Separate from training data storage and ML processing
/// - Values over state: Returns immutable import results
/// - Declarative: Describes what import does, not how
/// </remarks>
public interface ICsvImportService
{
    /// <summary>
    /// Imports training data from a CSV file with the specified format.
    /// </summary>
    /// <param name="csvFilePath">Path to the CSV file to import</param>
    /// <param name="configuration">Import configuration options</param>
    /// <returns>Result containing import statistics and imported data or error information</returns>
    Task<Result<CsvImportResult>> ImportFromCsvAsync(string csvFilePath, CsvImportConfiguration? configuration = null);

    /// <summary>
    /// Imports training data from CSV content string.
    /// </summary>
    /// <param name="csvContent">The CSV content as a string</param>
    /// <param name="configuration">Import configuration options</param>
    /// <returns>Result containing import statistics and imported data or error information</returns>
    Task<Result<CsvImportResult>> ImportFromCsvContentAsync(string csvContent, CsvImportConfiguration? configuration = null);

    /// <summary>
    /// Validates CSV file format without importing the data.
    /// </summary>
    /// <param name="csvFilePath">Path to the CSV file to validate</param>
    /// <param name="configuration">Import configuration options</param>
    /// <returns>Result containing validation results or error information</returns>
    Task<Result<CsvValidationResult>> ValidateCsvFormatAsync(string csvFilePath, CsvImportConfiguration? configuration = null);

    /// <summary>
    /// Gets statistics about a CSV file without importing it.
    /// </summary>
    /// <param name="csvFilePath">Path to the CSV file to analyze</param>
    /// <param name="configuration">Import configuration options</param>
    /// <returns>Result containing CSV statistics or error information</returns>
    Task<Result<CsvStatistics>> GetCsvStatisticsAsync(string csvFilePath, CsvImportConfiguration? configuration = null);

    /// <summary>
    /// Exports training data to CSV format.
    /// </summary>
    /// <param name="trainingSamples">Training samples to export</param>
    /// <param name="outputPath">Path where to save the CSV file</param>
    /// <param name="configuration">Export configuration options</param>
    /// <returns>Result indicating success or error information</returns>
    Task<Result<bool>> ExportToCsvAsync(IEnumerable<TrainingSample> trainingSamples, string outputPath, CsvImportConfiguration? configuration = null);
}

/// <summary>
/// Represents the result of CSV validation.
/// </summary>
public record CsvValidationResult
{
    /// <summary>
    /// Gets whether the CSV format is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets validation errors found in the CSV.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets validation warnings (non-blocking issues).
    /// </summary>
    public IReadOnlyList<string> ValidationWarnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the number of rows that would be imported.
    /// </summary>
    public int ValidRowCount { get; init; }

    /// <summary>
    /// Gets the number of rows that would be skipped.
    /// </summary>
    public int InvalidRowCount { get; init; }

    /// <summary>
    /// Gets sample categories found in the data.
    /// </summary>
    public IReadOnlyList<string> SampleCategories { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents statistics about a CSV file.
/// </summary>
public record CsvStatistics
{
    /// <summary>
    /// Gets the total number of rows in the CSV.
    /// </summary>
    public int TotalRows { get; init; }

    /// <summary>
    /// Gets the number of unique categories.
    /// </summary>
    public int UniqueCategories { get; init; }

    /// <summary>
    /// Gets the distribution of samples per category.
    /// </summary>
    public IReadOnlyDictionary<string, int> CategoryDistribution { get; init; } = 
        new Dictionary<string, int>();

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets common filename patterns found in the data.
    /// </summary>
    public IReadOnlyList<string> CommonPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the estimated processing time for import.
    /// </summary>
    public TimeSpan EstimatedProcessingTime { get; init; }

    /// <summary>
    /// Gets whether the dataset appears balanced across categories.
    /// </summary>
    public bool IsBalanced => UniqueCategories > 0 && 
        CategoryDistribution.Values.Max() / (double)CategoryDistribution.Values.Min() <= 2.0;
}