namespace MediaButler.ML.Models;

/// <summary>
/// Represents information about the ML model including performance metrics and metadata.
/// This is a value object containing model health and performance data.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable data structure
/// - Single responsibility: Only holds model metadata and metrics
/// - No complecting: Pure data without training or prediction logic
/// </remarks>
public record ModelInfo
{
    /// <summary>
    /// Gets the model version identifier.
    /// </summary>
    /// <example>"1.0.0", "2.1.3"</example>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the algorithm used for training.
    /// </summary>
    /// <example>"FastTree", "LightGBM", "SdcaMulticlass"</example>
    public string Algorithm { get; init; } = string.Empty;

    /// <summary>
    /// Gets when this model was trained.
    /// </summary>
    public DateTime TrainedAt { get; init; }

    /// <summary>
    /// Gets the total number of training samples used.
    /// </summary>
    public int TrainingSamples { get; init; }

    /// <summary>
    /// Gets the number of categories the model can predict.
    /// </summary>
    public int CategoryCount { get; init; }

    /// <summary>
    /// Gets the model accuracy on the test dataset (0.0 to 1.0).
    /// </summary>
    public float TestAccuracy { get; init; }

    /// <summary>
    /// Gets the model precision on the test dataset (0.0 to 1.0).
    /// </summary>
    public float TestPrecision { get; init; }

    /// <summary>
    /// Gets the model recall on the test dataset (0.0 to 1.0).
    /// </summary>
    public float TestRecall { get; init; }

    /// <summary>
    /// Gets the model F1 score on the test dataset (0.0 to 1.0).
    /// </summary>
    public float TestF1Score { get; init; }

    /// <summary>
    /// Gets the model file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the average prediction time in milliseconds.
    /// </summary>
    public double AverageInferenceTimeMs { get; init; }

    /// <summary>
    /// Gets the categories that the model can predict.
    /// </summary>
    public IReadOnlyList<string> AvailableCategories { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets additional metadata about the model.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();

    /// <summary>
    /// Indicates whether this model meets performance requirements.
    /// </summary>
    public bool IsPerformant => TestAccuracy >= 0.85f && AverageInferenceTimeMs <= 500.0;

    /// <summary>
    /// Gets the model file size in a human-readable format.
    /// </summary>
    public string FileSizeFormatted => FormatFileSize(FileSizeBytes);

    /// <summary>
    /// Gets a display-friendly accuracy percentage.
    /// </summary>
    public string AccuracyPercentage => $"{TestAccuracy * 100:F1}%";

    /// <summary>
    /// Formats bytes into human-readable string.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}