using System.Collections.Generic;
using System.Threading.Tasks;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Abstraction for providing data to the training pipeline.
/// Allows swapping data sources (e.g., CSV, SQL, In-Memory) without changing the trainer.
/// </summary>
public interface ITrainingDataProvider
{
    /// <summary>
    /// loads the training data as an enumerable of FileCategoryInput.
    /// </summary>
    /// <returns>A collection of training examples (Input + Label).</returns>
    Task<IEnumerable<FileCategoryInput>> LoadTrainingDataAsync();
    
    /// <summary>
    /// Checks if there is enough data to perform a valid training session.
    /// </summary>
    /// <param name="minSamples">Minimum required samples.</param>
    /// <returns>True if sufficient data exists.</returns>
    Task<bool> HasSufficientDataAsync(int minSamples);
}
