namespace MediaButler.ML.Models;

/// <summary>
/// Represents the result of ML classification for a filename.
/// This is a value object containing the prediction results and confidence metrics.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable data structure
/// - Single responsibility: Only holds classification results
/// - No complecting: Pure data without domain business logic
/// </remarks>
public record ClassificationResult
{
    /// <summary>
    /// Gets the filename that was classified.
    /// </summary>
    public string Filename { get; init; } = string.Empty;

    /// <summary>
    /// Gets the predicted category for the filename.
    /// </summary>
    /// <example>"THE OFFICE", "BREAKING BAD", "UNKNOWN"</example>
    public string PredictedCategory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the confidence score of the prediction (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Higher values indicate higher confidence:
    /// - Greater than 0.85: Auto-classify (pending confirmation)
    /// - 0.50-0.85: Suggest with alternatives
    /// - Less than 0.50: Likely new series
    /// </remarks>
    public float Confidence { get; init; }

    /// <summary>
    /// Gets alternative category predictions with their confidence scores.
    /// </summary>
    public IReadOnlyList<CategoryPrediction> AlternativePredictions { get; init; } = Array.Empty<CategoryPrediction>();

    /// <summary>
    /// Gets the classification decision based on confidence thresholds.
    /// </summary>
    public ClassificationDecision Decision { get; init; }

    /// <summary>
    /// Gets additional features extracted during classification.
    /// </summary>
    public IReadOnlyDictionary<string, object> Features { get; init; } = 
        new Dictionary<string, object>();

    /// <summary>
    /// Gets the model version used for this classification.
    /// </summary>
    public string ModelVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when this classification was performed.
    /// </summary>
    public DateTime ClassifiedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the processing time in milliseconds for this classification.
    /// </summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>
    /// Indicates whether this is a high-confidence classification.
    /// </summary>
    public bool IsHighConfidence => Confidence > 0.85f;

    /// <summary>
    /// Indicates whether this classification suggests a new/unknown series.
    /// </summary>
    public bool IsLikelyNewSeries => Confidence < 0.50f;

    /// <summary>
    /// Gets a display-friendly confidence percentage.
    /// </summary>
    public string ConfidencePercentage => $"{Confidence * 100:F1}%";
}

/// <summary>
/// Represents an alternative category prediction with confidence score.
/// </summary>
public record CategoryPrediction
{
    /// <summary>
    /// Gets the category name.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the confidence score for this category (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Gets a display-friendly confidence percentage.
    /// </summary>
    public string ConfidencePercentage => $"{Confidence * 100:F1}%";
}

/// <summary>
/// Represents the classification decision based on confidence analysis.
/// </summary>
public enum ClassificationDecision
{
    /// <summary>
    /// Classification failed or could not be determined.
    /// </summary>
    Failed = 0,

    /// <summary>
    /// High confidence - recommend auto-classification pending user confirmation.
    /// </summary>
    AutoClassify = 1,

    /// <summary>
    /// Medium confidence - suggest category with alternatives for user review.
    /// </summary>
    SuggestWithAlternatives = 2,

    /// <summary>
    /// Low confidence - likely new series, ask user for manual categorization.
    /// </summary>
    RequestManualCategorization = 3,

    /// <summary>
    /// Confidence below minimum threshold - classification unreliable.
    /// </summary>
    Unreliable = 4
}