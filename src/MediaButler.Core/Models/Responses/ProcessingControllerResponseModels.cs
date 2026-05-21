using System;
using System.Collections.Generic;

namespace MediaButler.Core.Models.Responses;

/// <summary>
/// Processing queue status information
/// </summary>
public record ProcessingQueueStatus
{
    public int QueueSize { get; init; }
    public int ActiveJobs { get; init; }
    public int CompletedToday { get; init; }
    public int FailedToday { get; init; }
    public int AvgProcessingTimeMs { get; init; }
    public DateTime LastActivity { get; init; } = DateTime.UtcNow;
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

/// <summary>
/// Response containing classification results with top 5 predictions
/// </summary>
public record ClassificationResponse
{
    /// <summary>
    /// The original filename that was classified
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// The predicted category (top prediction)
    /// </summary>
    public required string PredictedCategory { get; init; }

    /// <summary>
    /// Confidence score for the top prediction (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Confidence score as a percentage (0 to 100)
    /// </summary>
    public double ConfidencePercentage { get; init; }

    /// <summary>
    /// Top 5 category predictions with confidence scores, ordered by confidence (descending)
    /// </summary>
    public required List<CategoryPredictionDto> Top5Predictions { get; init; }

    /// <summary>
    /// Timestamp when the classification was performed
    /// </summary>
    public DateTime ClassifiedAt { get; init; }

    /// <summary>
    /// Version of the ML model used for classification
    /// </summary>
    public required int ModelVersion { get; init; }
}

/// <summary>
/// A single category prediction with confidence score (DTO for API response)
/// </summary>
public record CategoryPredictionDto
{
    /// <summary>
    /// The predicted category name
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Confidence score as a percentage (0 to 100)
    /// </summary>
    public double ConfidencePercentage { get; init; }

    /// <summary>
    /// Rank of this prediction (1 = top prediction, 2-5 = alternatives)
    /// </summary>
    public int Rank { get; init; }
}
