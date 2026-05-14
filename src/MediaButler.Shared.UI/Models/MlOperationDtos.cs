using System;

namespace MediaButler.Shared.UI.Models;

/// <summary>
/// Request for ML evaluation/re-evaluation.
/// </summary>
public class MlEvaluationRequest
{
    public string? FilterByCategory { get; set; }
    public bool ForceReEvaluation { get; set; } = true;
}

/// <summary>
/// Response from ML evaluation operation.
/// </summary>
public class MlEvaluationResponse
{
    public bool Success { get; set; }
    public int TotalFilesQueued { get; set; }
    public required string Message { get; set; }
    public DateTime QueuedAt { get; set; }
    public int EstimatedProcessingTimeMinutes { get; set; }
}

/// <summary>
/// Training session DTO for ML model training.
/// </summary>
public class TrainingSessionDto
{
    public required string SessionId { get; set; }
    public required string Status { get; set; }  // "Running", "Completed", "Failed"
    public required string Message { get; set; }
    public DateTime StartedAt { get; set; }
    public double? Accuracy { get; set; }
    public int? TrainingSampleCount { get; set; }
    public int? CategoryCount { get; set; }
    public int? ModelVersion { get; set; }
}
