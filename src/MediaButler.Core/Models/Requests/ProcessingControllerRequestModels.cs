namespace MediaButler.Core.Models.Requests;

/// <summary>
/// Request for queuing ML evaluation
/// </summary>
public record MlEvaluationRequest
{
    /// <summary>
    /// Optional category filter. If provided, only files in this category will be processed.
    /// </summary>
    public string? FilterByCategory { get; init; }

    /// <summary>
    /// Force re-evaluation even if files already have a SuggestedCategory.
    /// </summary>
    public bool ForceReEvaluation { get; init; } = true;
}

/// <summary>
/// Request for filename classification
/// </summary>
public record ClassificationRequest
{
    /// <summary>
    /// The filename to classify (e.g., "Breaking.Bad.S05E16.FINAL.1080p.mkv")
    /// </summary>
    public required string Filename { get; init; }
}
