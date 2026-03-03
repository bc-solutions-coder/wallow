using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Configuration.Infrastructure.Persistence.Repositories;

public sealed class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly ConfigurationDbContext _context;

    public FeatureFlagRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public Task<FeatureFlag?> GetByIdAsync(FeatureFlagId id, CancellationToken ct = default)
    {
        return _context.FeatureFlags
            .AsTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return _context.FeatureFlags
            .AsTracking()
            .Include(f => f.Overrides)
            .FirstOrDefaultAsync(f => f.Key == key, ct);
    }

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.FeatureFlags
            .OrderBy(f => f.Key)
            .ToListAsync(ct);
    }

    public async Task AddAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        _context.FeatureFlags.Add(flag);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        _context.FeatureFlags.Update(flag);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        _context.FeatureFlags.Remove(flag);
        await _context.SaveChangesAsync(ct);
    }
}
