using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Configuration.Infrastructure.Persistence.Repositories;

public sealed class FeatureFlagRepository : IFeatureFlagRepository
{
    private static readonly Func<ConfigurationDbContext, FeatureFlagId, CancellationToken, Task<FeatureFlag?>> _getByIdQuery =
        EF.CompileAsyncQuery(
            (ConfigurationDbContext ctx, FeatureFlagId id, CancellationToken _) =>
                ctx.FeatureFlags.AsTracking().FirstOrDefault(f => f.Id == id));

    private static readonly Func<ConfigurationDbContext, string, CancellationToken, Task<FeatureFlag?>> _getByKeyQuery =
        EF.CompileAsyncQuery(
            (ConfigurationDbContext ctx, string key, CancellationToken _) =>
                ctx.FeatureFlags.AsTracking().Include(f => f.Overrides).FirstOrDefault(f => f.Key == key));

    private readonly ConfigurationDbContext _context;

    public FeatureFlagRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public Task<FeatureFlag?> GetByIdAsync(FeatureFlagId id, CancellationToken ct = default)
    {
        return _getByIdQuery(_context, id, ct);
    }

    public Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return _getByKeyQuery(_context, key, ct);
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
