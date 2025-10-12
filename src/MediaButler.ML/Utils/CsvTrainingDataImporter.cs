using System.Globalization;
using MediaButler.Core.Common;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;

namespace MediaButler.ML.Utils;

/// <summary>
/// Utility class for importing training data from CSV files.
/// Handles the flexible CSV format: Filename;Category (with optional header).
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles CSV file parsing
/// - Values over state: Returns immutable collections
/// - No complecting: Independent utility with no external dependencies
/// </remarks>
public class CsvTrainingDataImporter
{
    private readonly ILogger<CsvTrainingDataImporter> _logger;

    public CsvTrainingDataImporter(ILogger<CsvTrainingDataImporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Imports training samples from a CSV file with format: Filename;Category
    /// </summary>
    /// <param name="csvFilePath">Path to the CSV file</param>
    /// <param name="config">Import configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing imported training samples</returns>
    public async Task<Result<CsvImportResult>> ImportFromCsvAsync(
        string csvFilePath,
        CsvImportConfiguration? config = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            config ??= new CsvImportConfiguration();

            _logger.LogInformation("Importing training data from CSV: {FilePath}", csvFilePath);

            if (!File.Exists(csvFilePath))
            {
                return Result<CsvImportResult>.Failure($"CSV file not found: {csvFilePath}");
            }

            var startTime = DateTime.UtcNow;
            var lines = await File.ReadAllLinesAsync(csvFilePath, cancellationToken);

            var samples = new List<TrainingSample>();
            var errors = new List<string>();
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var validRows = 0;
            var skippedRows = 0;
            var duplicateRows = 0;
            var startLine = config.HasHeader ? 1 : 0; // Skip header if present

            _logger.LogDebug("Processing {LineCount} lines (HasHeader: {HasHeader})",
                lines.Length, config.HasHeader);

            for (int i = startLine; i < lines.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("CSV import cancelled at line {LineNumber}", i);
                    break;
                }

                var line = lines[i].Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    skippedRows++;
                    continue;
                }

                // Check max rows limit
                if (config.MaxRows > 0 && validRows >= config.MaxRows)
                {
                    _logger.LogInformation("Reached max rows limit: {MaxRows}", config.MaxRows);
                    break;
                }

                // Parse CSV line
                var parseResult = ParseCsvLine(line, config, i + 1);

                if (!parseResult.IsSuccess)
                {
                    errors.Add($"Line {i + 1}: {parseResult.Error}");
                    skippedRows++;
                    continue;
                }

                var (filename, category) = parseResult.Value;

                // Validate file extension
                if (config.ValidateFileExtensions)
                {
                    var extension = Path.GetExtension(filename).ToLowerInvariant();
                    if (!config.ValidExtensions.Contains(extension))
                    {
                        errors.Add($"Line {i + 1}: Unsupported file extension '{extension}' for file: {filename}");
                        skippedRows++;
                        continue;
                    }
                }

                // Check for duplicates
                if (config.SkipDuplicates && !seenFilenames.Add(filename))
                {
                    duplicateRows++;
                    _logger.LogDebug("Skipping duplicate filename: {Filename}", filename);
                    continue;
                }

                // Normalize category name if requested
                var finalCategory = config.NormalizeCategoryNames
                    ? category.ToUpperInvariant()
                    : category;

                categories.Add(finalCategory);

                // Create training sample
                var sample = new TrainingSample
                {
                    Filename = filename,
                    Category = finalCategory,
                    Confidence = 0.9, // High confidence for imported data
                    Source = TrainingSampleSource.ImportedData,
                    CreatedAt = DateTime.UtcNow,
                    IsManuallyVerified = true, // CSV data is manually curated
                    Notes = $"Imported from CSV line {i + 1}"
                };

                samples.Add(sample);
                validRows++;
            }

            var processingTime = DateTime.UtcNow - startTime;

            var result = new CsvImportResult
            {
                TotalRows = lines.Length - startLine,
                ValidRows = validRows,
                SkippedRows = skippedRows,
                DuplicateRows = duplicateRows,
                Categories = categories.OrderBy(c => c).ToList().AsReadOnly(),
                ImportedSamples = samples.AsReadOnly(),
                Errors = errors.AsReadOnly(),
                ProcessingTime = processingTime
            };

            _logger.LogInformation(
                "CSV import completed: {ValidRows}/{TotalRows} rows imported, {Categories} categories, {Duplicates} duplicates, {Errors} errors in {Duration}ms",
                result.ValidRows, result.TotalRows, result.Categories.Count,
                result.DuplicateRows, result.Errors.Count, result.ProcessingTime.TotalMilliseconds);

            return Result<CsvImportResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing CSV file: {FilePath}", csvFilePath);
            return Result<CsvImportResult>.Failure($"CSV import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a single CSV line in the format: Filename;Category
    /// </summary>
    private Result<(string Filename, string Category)> ParseCsvLine(
        string line,
        CsvImportConfiguration config,
        int lineNumber)
    {
        try
        {
            var parts = line.Split(config.Separator);

            if (parts.Length < 2)
            {
                return Result<(string, string)>.Failure(
                    $"Invalid format - expected 'Filename{config.Separator}Category', got {parts.Length} parts");
            }

            var filename = parts[0].Trim();
            var category = parts[1].Trim();

            if (string.IsNullOrWhiteSpace(filename))
            {
                return Result<(string, string)>.Failure("Filename cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return Result<(string, string)>.Failure("Category cannot be empty");
            }

            return Result<(string, string)>.Success((filename, category));
        }
        catch (Exception ex)
        {
            return Result<(string, string)>.Failure($"Failed to parse line: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that the CSV file has the expected format.
    /// Checks the first non-empty line for proper structure.
    /// </summary>
    public async Task<Result<bool>> ValidateCsvFormatAsync(
        string csvFilePath,
        CsvImportConfiguration? config = null)
    {
        try
        {
            config ??= new CsvImportConfiguration();

            if (!File.Exists(csvFilePath))
            {
                return Result<bool>.Failure($"CSV file not found: {csvFilePath}");
            }

            var lines = await File.ReadAllLinesAsync(csvFilePath);

            if (lines.Length == 0)
            {
                return Result<bool>.Failure("CSV file is empty");
            }

            // Check first non-empty line
            var firstDataLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

            if (firstDataLine == null)
            {
                return Result<bool>.Failure("CSV file contains only empty lines");
            }

            var parts = firstDataLine.Split(config.Separator);

            if (parts.Length < 2)
            {
                return Result<bool>.Failure(
                    $"Invalid CSV format - expected at least 2 columns separated by '{config.Separator}', found {parts.Length}");
            }

            _logger.LogInformation("CSV format validation passed for: {FilePath}", csvFilePath);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating CSV format: {FilePath}", csvFilePath);
            return Result<bool>.Failure($"CSV validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a preview of the first N rows from the CSV file.
    /// Useful for inspecting data before full import.
    /// </summary>
    public async Task<Result<IReadOnlyList<(string Filename, string Category)>>> GetCsvPreviewAsync(
        string csvFilePath,
        int previewRows = 10,
        CsvImportConfiguration? config = null)
    {
        try
        {
            config ??= new CsvImportConfiguration();

            if (!File.Exists(csvFilePath))
            {
                return Result<IReadOnlyList<(string, string)>>.Failure($"CSV file not found: {csvFilePath}");
            }

            var lines = await File.ReadAllLinesAsync(csvFilePath);
            var preview = new List<(string Filename, string Category)>();

            var startLine = config.HasHeader ? 1 : 0;
            var endLine = Math.Min(lines.Length, startLine + previewRows);

            for (int i = startLine; i < endLine; i++)
            {
                var line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parseResult = ParseCsvLine(line, config, i + 1);

                if (parseResult.IsSuccess)
                {
                    preview.Add(parseResult.Value);
                }
            }

            return Result<IReadOnlyList<(string, string)>>.Success(preview.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CSV preview: {FilePath}", csvFilePath);
            return Result<IReadOnlyList<(string, string)>>.Failure($"CSV preview failed: {ex.Message}");
        }
    }
}
