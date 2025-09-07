namespace MediaButler.ML.Models;

/// <summary>
/// Represents a training data split for ML model training.
/// This is a value object containing train/validation/test datasets.
/// </summary>
public record TrainingDataSplit
{
    /// <summary>
    /// Gets the training dataset samples.
    /// </summary>
    public IReadOnlyList<TrainingSample> TrainingSet { get; init; } = Array.Empty<TrainingSample>();

    /// <summary>
    /// Gets the validation dataset samples.
    /// </summary>
    public IReadOnlyList<TrainingSample> ValidationSet { get; init; } = Array.Empty<TrainingSample>();

    /// <summary>
    /// Gets the test dataset samples.
    /// </summary>
    public IReadOnlyList<TrainingSample> TestSet { get; init; } = Array.Empty<TrainingSample>();

    /// <summary>
    /// Gets the total number of samples across all sets.
    /// </summary>
    public int TotalSamples => TrainingSet.Count + ValidationSet.Count + TestSet.Count;

    /// <summary>
    /// Gets the split ratios used.
    /// </summary>
    public (double Train, double Validation, double Test) SplitRatios { get; init; }
}

/// <summary>
/// Represents a single training sample with filename and expected category.
/// This is a value object for ML training data.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable training sample data
/// - Single responsibility: Only holds one training example
/// - Declarative: Clear training data without processing logic
/// </remarks>
public sealed record TrainingSample
{
    /// <summary>
    /// Gets the filename for training.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Gets the expected/correct category for this filename.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets the confidence in this category assignment (0-1).
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets the source of this training sample.
    /// </summary>
    public required TrainingSampleSource Source { get; init; }

    /// <summary>
    /// Gets when this sample was added to the training set.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets whether this sample was manually verified.
    /// </summary>
    public bool IsManuallyVerified { get; init; }

    /// <summary>
    /// Gets optional extracted features for this sample.
    /// </summary>
    public FeatureVector? ExtractedFeatures { get; init; }

    /// <summary>
    /// Gets optional notes about this training sample.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Creates a training sample from user feedback.
    /// </summary>
    /// <param name="filename">Filename from user interaction</param>
    /// <param name="category">User-confirmed category</param>
    /// <param name="confidence">Confidence in assignment (default: 1.0)</param>
    /// <returns>Training sample for dataset</returns>
    public static TrainingSample FromUserFeedback(string filename, string category, double confidence = 1.0)
    {
        return new TrainingSample
        {
            Filename = filename,
            Category = category,
            Confidence = confidence,
            Source = TrainingSampleSource.UserFeedback,
            CreatedAt = DateTime.UtcNow,
            IsManuallyVerified = true
        };
    }

    /// <summary>
    /// Creates a training sample from automated analysis.
    /// </summary>
    /// <param name="filename">Analyzed filename</param>
    /// <param name="category">Predicted category</param>
    /// <param name="confidence">Analysis confidence</param>
    /// <returns>Training sample for dataset</returns>
    public static TrainingSample FromAutomatedAnalysis(string filename, string category, double confidence)
    {
        return new TrainingSample
        {
            Filename = filename,
            Category = category,
            Confidence = confidence,
            Source = TrainingSampleSource.AutomatedAnalysis,
            CreatedAt = DateTime.UtcNow,
            IsManuallyVerified = false
        };
    }
}

/// <summary>
/// Represents training data validation results.
/// This is a value object containing data quality metrics.
/// </summary>
public record TrainingDataValidation
{
    /// <summary>
    /// Gets whether the training data passed validation.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets validation issues found in the data.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

    /// <summary>
    /// Gets recommendations for improving data quality.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the overall data quality score (0.0 to 1.0).
    /// </summary>
    public float QualityScore { get; init; }
}

/// <summary>
/// Represents a validation issue found in training data.
/// </summary>
public record ValidationIssue
{
    /// <summary>
    /// Gets the severity of this issue.
    /// </summary>
    public IssueSeverity Severity { get; init; }

    /// <summary>
    /// Gets the description of the issue.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the affected samples or categories.
    /// </summary>
    public IReadOnlyList<string> AffectedItems { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents the severity of a validation issue.
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Informational message that doesn't affect training.
    /// </summary>
    Info = 0,
    
    /// <summary>
    /// Warning about potential issues that should be reviewed.
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Error that affects training quality but doesn't prevent it.
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Critical issue that prevents training from succeeding.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Represents statistics about the training dataset.
/// This is a value object containing dataset metrics.
/// </summary>
public record DatasetStatistics
{
    /// <summary>
    /// Gets the total number of training samples.
    /// </summary>
    public int TotalSamples { get; init; }

    /// <summary>
    /// Gets the number of unique categories.
    /// </summary>
    public int UniqueCategories { get; init; }

    /// <summary>
    /// Gets samples per category breakdown.
    /// </summary>
    public IReadOnlyDictionary<string, int> SamplesPerCategory { get; init; } = 
        new Dictionary<string, int>();

    /// <summary>
    /// Gets the date range of the training data.
    /// </summary>
    public (DateTime Earliest, DateTime Latest) DateRange { get; init; }

    /// <summary>
    /// Gets the average samples per category.
    /// </summary>
    public double AverageSamplesPerCategory => UniqueCategories > 0 ? (double)TotalSamples / UniqueCategories : 0;

    /// <summary>
    /// Gets whether the dataset is balanced (no category has less than 10% of average).
    /// </summary>
    public bool IsBalanced
    {
        get
        {
            if (UniqueCategories == 0) return false;
            var threshold = AverageSamplesPerCategory * 0.1;
            return SamplesPerCategory.Values.All(count => count >= threshold);
        }
    }
}

/// <summary>
/// Represents the result of importing training data from a file.
/// </summary>
public record ImportResult
{
    /// <summary>
    /// Gets the number of samples successfully imported.
    /// </summary>
    public int ImportedSamples { get; init; }

    /// <summary>
    /// Gets the number of samples that were skipped (duplicates, invalid, etc.).
    /// </summary>
    public int SkippedSamples { get; init; }

    /// <summary>
    /// Gets the number of new categories discovered during import.
    /// </summary>
    public int NewCategories { get; init; }

    /// <summary>
    /// Gets import warnings and issues.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the total processing time for the import.
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }
}

/// <summary>
/// Source of a training sample for tracking data quality.
/// </summary>
public enum TrainingSampleSource
{
    /// <summary>
    /// Sample from user feedback/confirmation.
    /// </summary>
    UserFeedback = 0,
    
    /// <summary>
    /// Sample from automated analysis.
    /// </summary>
    AutomatedAnalysis = 1,
    
    /// <summary>
    /// Sample from imported data.
    /// </summary>
    ImportedData = 2,
    
    /// <summary>
    /// Sample from manual curation.
    /// </summary>
    ManualCuration = 3,
    
    /// <summary>
    /// Sample from synthetic generation.
    /// </summary>
    SyntheticGeneration = 4
}