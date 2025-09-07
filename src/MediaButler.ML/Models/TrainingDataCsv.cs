namespace MediaButler.ML.Models;

/// <summary>
/// Represents a single row from the training data CSV file.
/// This is a value object for importing training data in the specified format.
/// </summary>
/// <remarks>
/// CSV Format: id;Category;FileName
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable data structure
/// - Single responsibility: Only holds CSV row data
/// - No complecting: Pure data without processing logic
/// </remarks>
public record TrainingDataCsvRow
{
    /// <summary>
    /// Gets the unique identifier for this training sample.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the category/series name that this filename should be classified into.
    /// </summary>
    /// <example>"THE OFFICE", "BREAKING BAD", "GAME OF THRONES"</example>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the filename that should be classified into the specified category.
    /// </summary>
    /// <example>"The.Office.S01E01.Pilot.1080p.WEB-DL.x264-GROUP.mkv"</example>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether this is a valid training sample.
    /// </summary>
    public bool IsValid => Id > 0 && 
                          !string.IsNullOrWhiteSpace(Category) && 
                          !string.IsNullOrWhiteSpace(FileName);

    /// <summary>
    /// Converts this CSV row to a training sample.
    /// </summary>
    /// <returns>TrainingSample for ML training pipeline</returns>
    public TrainingSample ToTrainingSample()
    {
        return new TrainingSample
        {
            Filename = FileName,
            ExpectedCategory = Category,
            AddedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["ImportId"] = Id,
                ["Source"] = "CSV Import"
            }
        };
    }
}

/// <summary>
/// Represents the result of importing training data from CSV.
/// This is a value object containing import statistics and results.
/// </summary>
public record CsvImportResult
{
    /// <summary>
    /// Gets the total number of rows found in the CSV file.
    /// </summary>
    public int TotalRows { get; init; }

    /// <summary>
    /// Gets the number of valid rows that were processed.
    /// </summary>
    public int ValidRows { get; init; }

    /// <summary>
    /// Gets the number of rows that were skipped due to validation errors.
    /// </summary>
    public int SkippedRows { get; init; }

    /// <summary>
    /// Gets the number of duplicate rows that were ignored.
    /// </summary>
    public int DuplicateRows { get; init; }

    /// <summary>
    /// Gets the unique categories found in the data.
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the training samples that were successfully imported.
    /// </summary>
    public IReadOnlyList<TrainingSample> ImportedSamples { get; init; } = Array.Empty<TrainingSample>();

    /// <summary>
    /// Gets any errors or issues encountered during import.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the time taken to process the import.
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// Indicates whether the import was successful overall.
    /// </summary>
    public bool IsSuccessful => ValidRows > 0 && Errors.Count == 0;

    /// <summary>
    /// Gets the import success rate as a percentage.
    /// </summary>
    public double SuccessRate => TotalRows > 0 ? (double)ValidRows / TotalRows * 100 : 0;
}

/// <summary>
/// Configuration for CSV training data import.
/// </summary>
public record CsvImportConfiguration
{
    /// <summary>
    /// Gets or sets the CSV separator character.
    /// </summary>
    /// <remarks>Default: ';' (semicolon) as specified</remarks>
    public char Separator { get; init; } = ';';

    /// <summary>
    /// Gets or sets whether the CSV file has a header row.
    /// </summary>
    /// <remarks>Default: false (no header row expected)</remarks>
    public bool HasHeader { get; init; } = false;

    /// <summary>
    /// Gets or sets whether to skip duplicate filenames.
    /// </summary>
    /// <remarks>Default: true (avoid duplicate training samples)</remarks>
    public bool SkipDuplicates { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to normalize category names to uppercase.
    /// </summary>
    /// <remarks>Default: true (consistent with MediaButler conventions)</remarks>
    public bool NormalizeCategoryNames { get; init; } = true;

    /// <summary>
    /// Gets or sets the maximum number of rows to import (0 = no limit).
    /// </summary>
    /// <remarks>Default: 0 (import all rows)</remarks>
    public int MaxRows { get; init; } = 0;

    /// <summary>
    /// Gets or sets whether to validate filename extensions.
    /// </summary>
    /// <remarks>Default: true (only import video file extensions)</remarks>
    public bool ValidateFileExtensions { get; init; } = true;

    /// <summary>
    /// Gets or sets the valid video file extensions.
    /// </summary>
    public IReadOnlyList<string> ValidExtensions { get; init; } = new List<string>
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".m2ts"
    };
}