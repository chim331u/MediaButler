using System;
using MediaButler.Core.Common;

namespace MediaButler.Core.Entities;

/// <summary>
/// Represents a specific version of the machine learning model.
/// Allows for tracking model history and rolling back if necessary.
/// </summary>
public class ModelVersion : BaseEntity
{
    /// <summary>
    /// Unique identifier for the model version.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Simple semantic version string (e.g., "1.0.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// The file path relative to the model directory where this model is stored.
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>
    /// JSON string containing performance metrics for this specific version.
    /// </summary>
    public string? Metrics { get; set; }

    /// <summary>
    /// Indicates if this is the currently active model used for predictions.
    /// Only one model version should be active at a time.
    /// </summary>
    public bool IsCurrent { get; set; }
}
