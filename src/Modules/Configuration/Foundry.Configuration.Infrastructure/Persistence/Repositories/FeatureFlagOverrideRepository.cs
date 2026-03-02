using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Configuration.Infrastructure.Persistence.Repositories;

public sealed class FeatureFlagOverrideRepository : IFeatureFlagOverrideRepository
{
    private readonly ConfigurationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public FeatureFlagOverrideRepository(ConfigurationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task<FeatureFlagOverride?> GetByIdAsync(FeatureFlagOverrideId id, CancellationToken ct = default)
    {
        return ActiveOverrides()
            .AsTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<IReadOnlyList<FeatureFlagOverride>> GetOverridesForFlagAsync(
        FeatureFlagId flagId,
        CancellationToken ct = default)
    {
        return await ActiveOverrides()
            .Where(o => o.FlagId == flagId)
            .ToListAsync(ct);
    }

    public Task<FeatureFlagOverride?> GetOverrideAsync(
        FeatureFlagId flagId,
        Guid? tenantId,
        Guid? userId,
        CancellationToken ct = default)
    {
        return ActiveOverrides()
            .AsTracking()
            .Where(o => o.FlagId == flagId
                        && o.TenantId == tenantId
                        && o.UserId == userId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<FeatureFlagOverride>> GetOverridesForContextAsync(
        Guid tenantId,
        Guid? userId,
        CancellationToken ct = default)
    {
        IQueryable<FeatureFlagOverride> query = ActiveOverrides();

        query = userId.HasValue
            ? query.Where(o => o.TenantId == tenantId || o.UserId == userId)
            : query.Where(o => o.TenantId == tenantId);

        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(FeatureFlagOverride over, CancellationToken ct = default)
    {
        _context.FeatureFlagOverrides.Add(over);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(FeatureFlagOverride over, CancellationToken ct = default)
    {
        _context.FeatureFlagOverrides.Remove(over);
        await _context.SaveChangesAsync(ct);
    }

    // ExpiresAt == null means no expiration (always active)
    private IQueryable<FeatureFlagOverride> ActiveOverrides()
    {
        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        return _context.FeatureFlagOverrides.Where(o => o.ExpiresAt == null || o.ExpiresAt > utcNow);
    }
}
