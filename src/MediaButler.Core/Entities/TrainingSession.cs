using System;
using MediaButler.Core.Common;

namespace MediaButler.Core.Entities;

/// <summary>
/// Represents a machine learning training session.
/// Tracks the execution, status, and outcome of a model training process.
/// </summary>
public class TrainingSession : BaseEntity
{
    /// <summary>
    /// Unique identifier for the training session.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The timestamp when the training session started.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The timestamp when the training session ended (completed or failed).
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// The current status of the training session.
    /// </summary>
    public string Status { get; set; } = "Pending"; // e.g., Pending, Running, Completed, Failed

    /// <summary>
    /// The number of samples used for training.
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// JSON string containing performance metrics (accuracy, log loss, etc.).
    /// </summary>
    public string? Metrics { get; set; }

    /// <summary>
    /// Detailed logs or error messages from the training process.
    /// </summary>
    public string? Log { get; set; }

    /// <summary>
    /// The version of the model produced by this session, if successful.
    /// </summary>
    public string? GeneratedModelVersion { get; set; }
}
