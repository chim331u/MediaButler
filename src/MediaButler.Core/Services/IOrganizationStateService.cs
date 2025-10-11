using MediaButler.Core.Models;

namespace MediaButler.Core.Services;

/// <summary>
/// Service for managing file organization state with database-backed persistence.
/// Replaces static state dictionary to enable testability and proper service scoping.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles by:
/// - Separating state management from organization orchestration
/// - Making state persistence explicit and database-backed
/// - Enabling proper dependency injection and testing
/// - Eliminating static mutable state
/// </remarks>
public interface IOrganizationStateService
{
    /// <summary>
    /// Sets the organization state for a file.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file</param>
    /// <param name="state">New organization state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetStateAsync(string fileHash, OrganizationState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current organization state for a file.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current state, or Pending if no state exists</returns>
    Task<OrganizationState> GetStateAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears organization state for a file (useful after successful completion).
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearStateAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all files currently in a specific state.
    /// </summary>
    /// <param name="state">State to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of file hashes in the specified state</returns>
    Task<List<string>> GetFilesByStateAsync(OrganizationState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all stale organization states (files in InProgress state older than threshold).
    /// </summary>
    /// <param name="olderThan">Clear states older than this duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of states cleared</returns>
    Task<int> ClearStaleStatesAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
}
