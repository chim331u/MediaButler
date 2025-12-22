namespace MediaButler.Mobile.Models;

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
    public required string JobId { get; set; }
    public int FilesQueued { get; set; }
    public DateTime QueuedAt { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Training session DTO for ML model training.
/// </summary>
public class TrainingSessionDto
{
    public required string SessionId { get; set; }
    public required string Status { get; set; }  // "Running", "Completed", "Failed"
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? Accuracy { get; set; }
    public int SampleCount { get; set; }
    public string? ModelVersion { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Metrics { get; set; }
}
