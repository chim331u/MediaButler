using System.ComponentModel.DataAnnotations;

namespace MediaButler.ML.Models;

/// <summary>
/// Result of batch classification operation containing multiple predictions.
/// </summary>
public sealed record BatchClassificationResult
{
    /// <summary>
    /// Gets individual classification results for each filename.
    /// </summary>
    public required IReadOnlyList<ClassificationResult> Results { get; init; }

    /// <summary>
    /// Gets the total number of files processed.
    /// </summary>
    public int TotalFiles => Results.Count;

    /// <summary>
    /// Gets the number of successful classifications.
    /// </summary>
    public int SuccessfulClassifications => Results.Count(r => r.Decision != ClassificationDecision.Failed);

    /// <summary>
    /// Gets the average confidence score across all successful predictions.
    /// </summary>
    public double AverageConfidence => 
        Results.Where(r => r.Decision != ClassificationDecision.Failed).DefaultIfEmpty().Average(r => r?.Confidence ?? 0.0f);

    /// <summary>
    /// Gets timestamp when batch processing was completed.
    /// </summary>
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets processing duration for the entire batch.
    /// </summary>
    public TimeSpan ProcessingDuration { get; init; }
}

/// <summary>
/// Result of filename validation for prediction processing.
/// </summary>
public sealed record FilenameValidationResult
{
    /// <summary>
    /// Gets whether the filename is valid for processing.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the confidence that this filename can be processed effectively.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double ProcessingConfidence { get; init; }

    /// <summary>
    /// Gets validation issues found with the filename.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

    /// <summary>
    /// Gets recommendations for improving filename processability.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets detected complexity indicators in the filename.
    /// </summary>
    public required FilenameComplexity Complexity { get; init; }

    /// <summary>
    /// Gets Italian content indicators detected in the filename.
    /// </summary>
    public required ItalianContentIndicators ItalianIndicators { get; init; }
}

/// <summary>
/// Represents filename complexity analysis for prediction processing.
/// </summary>
public sealed record FilenameComplexity
{
    /// <summary>
    /// Gets the overall complexity score (0-10).
    /// </summary>
    [Range(0, 10)]
    public required int ComplexityScore { get; init; }

    /// <summary>
    /// Gets whether the filename structure is considered complex.
    /// </summary>
    public bool IsComplex => ComplexityScore > 6;

    /// <summary>
    /// Gets the number of tokens identified in the filename.
    /// </summary>
    public required int TokenCount { get; init; }

    /// <summary>
    /// Gets the number of different separators used.
    /// </summary>
    public required int SeparatorCount { get; init; }

    /// <summary>
    /// Gets whether special patterns (episode, quality, etc.) were detected.
    /// </summary>
    public required bool HasSpecialPatterns { get; init; }

    /// <summary>
    /// Gets pattern types detected in the filename.
    /// </summary>
    public IReadOnlyList<string> DetectedPatterns { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Italian content indicators detected in filename analysis.
/// </summary>
public sealed record ItalianContentIndicators
{
    /// <summary>
    /// Contains Italian language codes (ITA, Italian).
    /// </summary>
    public required bool HasItalianLanguage { get; init; }

    /// <summary>
    /// Contains known Italian release groups.
    /// </summary>
    public required bool HasItalianReleaseGroup { get; init; }

    /// <summary>
    /// The Italian release group if detected.
    /// </summary>
    public string? ItalianReleaseGroup { get; init; }

    /// <summary>
    /// Contains patterns typical of Italian TV series.
    /// </summary>
    public required bool HasItalianSeries { get; init; }

    /// <summary>
    /// Confidence that this is Italian content (0-1).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double ItalianConfidence { get; init; }
}

/// <summary>
/// Performance statistics for the prediction service.
/// </summary>
public sealed record PredictionPerformanceStats
{
    /// <summary>
    /// Gets the total number of predictions made.
    /// </summary>
    public required long TotalPredictions { get; init; }

    /// <summary>
    /// Gets the number of successful predictions.
    /// </summary>
    public required long SuccessfulPredictions { get; init; }

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    public double SuccessRate => TotalPredictions > 0 ? (double)SuccessfulPredictions / TotalPredictions : 0.0;

    /// <summary>
    /// Gets the average prediction time.
    /// </summary>
    public required TimeSpan AveragePredictionTime { get; init; }

    /// <summary>
    /// Gets the average confidence score across all predictions.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double AverageConfidence { get; init; }

    /// <summary>
    /// Gets predictions broken down by confidence level.
    /// </summary>
    public required ConfidenceLevelStats ConfidenceBreakdown { get; init; }

    /// <summary>
    /// Gets statistics collection timestamp.
    /// </summary>
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the time period covered by these statistics.
    /// </summary>
    public required TimeSpan StatsPeriod { get; init; }
}

/// <summary>
/// Statistics broken down by confidence levels.
/// </summary>
public sealed record ConfidenceLevelStats
{
    /// <summary>
    /// Gets count of high confidence predictions (>0.8).
    /// </summary>
    public required long HighConfidence { get; init; }

    /// <summary>
    /// Gets count of medium confidence predictions (0.5-0.8).
    /// </summary>
    public required long MediumConfidence { get; init; }

    /// <summary>
    /// Gets count of low confidence predictions (less than 0.5).
    /// </summary>
    public required long LowConfidence { get; init; }

    /// <summary>
    /// Gets the total number of predictions covered.
    /// </summary>
    public long Total => HighConfidence + MediumConfidence + LowConfidence;

    /// <summary>
    /// Gets percentage of high confidence predictions.
    /// </summary>
    public double HighConfidencePercentage => Total > 0 ? (double)HighConfidence / Total * 100 : 0.0;
}