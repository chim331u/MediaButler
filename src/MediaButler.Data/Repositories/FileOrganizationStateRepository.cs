using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Repository implementation for FileOrganizationStateEntity.
/// Provides specialized queries for organization state management.
/// </summary>
public class FileOrganizationStateRepository : Repository<FileOrganizationStateEntity>, IFileOrganizationStateRepository
{
    public FileOrganizationStateRepository(MediaButlerDbContext context) : base(context)
    {
    }

    public async Task<FileOrganizationStateEntity?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileHash);

        return await DbSet
            .FirstOrDefaultAsync(s => s.FileHash == fileHash, cancellationToken);
    }

    public async Task<List<FileOrganizationStateEntity>> GetByStateAsync(OrganizationState state, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.State == state)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FileOrganizationStateEntity>> GetStaleStatesAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.State == OrganizationState.InProgress && s.StateUpdatedAt < olderThan)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileHash);

        var entity = await GetByFileHashAsync(fileHash, cancellationToken);
        if (entity != null)
        {
            HardDelete(entity);
            return await SaveChangesAsync(cancellationToken);
        }

        return 0;
    }
}
