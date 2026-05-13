using System;
using System.Threading.Tasks;
using MediaButler.Core.Entities;

namespace MediaButler.Core.Interfaces;

/// <summary>
/// Abstraction for persisting ML-related entities.
/// Decouples the ML logic from the specific data access implementation.
/// </summary>
public interface IMLPersistenceService
{
    /// <summary>
    /// Saves or updates a training session.
    /// </summary>
    Task SaveTrainingSessionAsync(TrainingSession session);

    /// <summary>
    /// Saves a new model version.
    /// </summary>
    Task SaveModelVersionAsync(ModelVersion version);

    /// <summary>
    /// Retrieves the latest active model version.
    /// </summary>
    Task<ModelVersion?> GetLatestModelVersionAsync();

    /// <summary>
    /// Mark a specific model version as the current active one.
    /// </summary>
    Task SetActiveModelVersionAsync(Guid modelVersionId);
}
