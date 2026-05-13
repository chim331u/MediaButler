using System;
using System.Threading.Tasks;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Interface for the ML Model Manager (The Kernel).
/// Responsible for thread-safe prediction and managing the potential hot-reload of the model.
/// </summary>
public interface IMLModelManager
{
    /// <summary>
    /// Predicts the category for a given file input.
    /// Thread-safe and handles potentially swapping models in the background.
    /// </summary>
    /// <param name="input">The file info input.</param>
    /// <returns>Prediction result containing the category and confidence.</returns>
    FileCategoryPrediction Predict(FileCategoryInput input);

    /// <summary>
    /// Checks if a model is currently loaded and ready for use.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Invalidates the current model, forcing a reload on the next prediction request.
    /// This is called by the training service when a new model version is promoted.
    /// </summary>
    void ReloadModel();
}
