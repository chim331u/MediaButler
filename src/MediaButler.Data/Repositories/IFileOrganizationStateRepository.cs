using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Services;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Repository interface for FileOrganizationStateEntity with specialized queries.
/// </summary>
public interface IFileOrganizationStateRepository : IRepository<FileOrganizationStateEntity>
{
    /// <summary>
    /// Gets the organization state for a specific file by hash.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>State entity if found, null otherwise</returns>
    Task<FileOrganizationStateEntity?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all files currently in a specific organization state.
    /// </summary>
    /// <param name="state">State to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of state entities</returns>
    Task<List<FileOrganizationStateEntity>> GetByStateAsync(OrganizationState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets stale organization states (files stuck in InProgress older than threshold).
    /// </summary>
    /// <param name="olderThan">Get states updated before this timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of stale state entities</returns>
    Task<List<FileOrganizationStateEntity>> GetStaleStatesAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes organization state for a specific file.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records deleted</returns>
    Task<int> DeleteByFileHashAsync(string fileHash, CancellationToken cancellationToken = default);
}
