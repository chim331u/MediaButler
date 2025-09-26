namespace MediaButler.Web.Models;

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
/// Response for ML evaluation queue operation
/// </summary>
public record MlEvaluationResponse
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Total number of files queued for ML evaluation
    /// </summary>
    public int TotalFilesQueued { get; init; }

    /// <summary>
    /// Descriptive message about the operation result
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the files were queued
    /// </summary>
    public DateTime QueuedAt { get; init; }

    /// <summary>
    /// Estimated processing time in minutes
    /// </summary>
    public int EstimatedProcessingTimeMinutes { get; init; }
}