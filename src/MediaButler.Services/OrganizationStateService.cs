using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Services;
using MediaButler.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services;

/// <summary>
/// Service for managing file organization state with database-backed persistence.
/// Replaces static state dictionary to enable testability and proper service scoping.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles by:
/// - Separating state management from organization orchestration (composition over complecting)
/// - Making state persistence explicit and database-backed (values over hidden state)
/// - Enabling proper dependency injection and testing (no static mutable state)
/// - Providing clean, focused interface for state operations
/// </remarks>
public class OrganizationStateService : IOrganizationStateService
{
    private readonly IFileOrganizationStateRepository _repository;
    private readonly ILogger<OrganizationStateService> _logger;

    public OrganizationStateService(
        IFileOrganizationStateRepository repository,
        ILogger<OrganizationStateService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task SetStateAsync(string fileHash, OrganizationState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileHash);

        try
        {
            var existing = await _repository.GetByFileHashAsync(fileHash, cancellationToken);

            if (existing != null)
            {
                // Update existing state
                existing.State = state;
                existing.StateUpdatedAt = DateTime.UtcNow;
                existing.MarkAsModified();
                _repository.Update(existing);
            }
            else
            {
                // Create new state
                var newState = new FileOrganizationStateEntity
                {
                    FileHash = fileHash,
                    State = state,
                    StateUpdatedAt = DateTime.UtcNow
                };
                _repository.Add(newState);
            }

            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Set organization state for {FileHash} to {State}", fileHash, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set organization state for {FileHash}", fileHash);
            throw;
        }
    }

    public async Task<OrganizationState> GetStateAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileHash);

        try
        {
            var state = await _repository.GetByFileHashAsync(fileHash, cancellationToken);
            return state?.State ?? OrganizationState.Pending;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get organization state for {FileHash}", fileHash);
            return OrganizationState.Pending;
        }
    }

    public async Task ClearStateAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileHash);

        try
        {
            var deleted = await _repository.DeleteByFileHashAsync(fileHash, cancellationToken);

            if (deleted > 0)
            {
                _logger.LogDebug("Cleared organization state for {FileHash}", fileHash);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear organization state for {FileHash}", fileHash);
            throw;
        }
    }

    public async Task<List<string>> GetFilesByStateAsync(OrganizationState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var states = await _repository.GetByStateAsync(state, cancellationToken);
            return states.Select(s => s.FileHash).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files by state {State}", state);
            return new List<string>();
        }
    }

    public async Task<int> ClearStaleStatesAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        try
        {
            var threshold = DateTime.UtcNow - olderThan;
            var staleStates = await _repository.GetStaleStatesAsync(threshold, cancellationToken);

            if (staleStates.Count == 0)
            {
                return 0;
            }

            foreach (var state in staleStates)
            {
                _repository.HardDelete(state);
            }

            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cleared {Count} stale organization states older than {Threshold}",
                staleStates.Count, olderThan);

            return staleStates.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear stale organization states");
            return 0;
        }
    }
}
