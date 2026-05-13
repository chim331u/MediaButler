using Microsoft.ML.Data;

namespace MediaButler.ML.Models;

/// <summary>
/// Input data class for ML model prediction.
/// Represents the features used for classification.
/// </summary>
public class FileCategoryInput
{
    /// <summary>
    /// The original filename including extension.
    /// </summary>
    [LoadColumn(0)]
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// The extracted series name (if available/used by feature engineering).
    /// </summary>
    [LoadColumn(1)]
    public string? SeriesName { get; set; }
}

/// <summary>
/// Output data class for ML model prediction.
/// Represents the prediction result from the model.
/// </summary>
public class FileCategoryPrediction
{
    /// <summary>
    /// The predicted category label.
    /// </summary>
    [ColumnName("PredictedLabel")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The confidence scores for all categories.
    /// </summary>
    public float[] Score { get; set; } = Array.Empty<float>();
}
