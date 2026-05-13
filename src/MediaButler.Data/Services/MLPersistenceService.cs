using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaButler.Core.Entities;
using MediaButler.Core.Interfaces;

namespace MediaButler.Data.Services;

/// <summary>
/// Implementation of ML persistence using EF Core.
/// </summary>
public class MLPersistenceService : IMLPersistenceService
{
    private readonly MediaButlerDbContext _context;

    public MLPersistenceService(MediaButlerDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task SaveTrainingSessionAsync(TrainingSession session)
    {
        var existing = await _context.TrainingSessions.FindAsync(session.Id);
        if (existing == null)
        {
            _context.TrainingSessions.Add(session);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(session);
        }
        await _context.SaveChangesAsync();
    }

    public async Task SaveModelVersionAsync(ModelVersion version)
    {
        var existing = await _context.ModelVersions.FindAsync(version.Id);
        if (existing == null)
        {
            _context.ModelVersions.Add(version);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(version);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<ModelVersion?> GetLatestModelVersionAsync()
    {
        return await _context.ModelVersions
            .Where(mv => mv.IsActive && mv.IsCurrent)
            .OrderByDescending(mv => mv.CreatedDate)
            .FirstOrDefaultAsync();
    }

    public async Task SetActiveModelVersionAsync(Guid modelVersionId)
    {
        var versions = await _context.ModelVersions.Where(v => v.IsCurrent).ToListAsync();
        foreach (var v in versions)
        {
            v.IsCurrent = false;
        }

        var newCurrent = await _context.ModelVersions.FindAsync(modelVersionId);
        if (newCurrent != null)
        {
            newCurrent.IsCurrent = true;
        }

        await _context.SaveChangesAsync();
    }
}
