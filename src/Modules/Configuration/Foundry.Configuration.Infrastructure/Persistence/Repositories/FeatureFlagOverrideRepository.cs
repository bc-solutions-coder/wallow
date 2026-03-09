using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Configuration.Infrastructure.Persistence.Repositories;

public sealed class FeatureFlagOverrideRepository(
    ConfigurationDbContext context,
    TimeProvider timeProvider) : IFeatureFlagOverrideRepository
{

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

    public async Task AddAsync(FeatureFlagOverride over, CancellationToken ct = default)
    {
        context.FeatureFlagOverrides.Add(over);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(FeatureFlagOverride over, CancellationToken ct = default)
    {
        context.FeatureFlagOverrides.Remove(over);
        await context.SaveChangesAsync(ct);
    }

    // ExpiresAt == null means no expiration (always active)
    private IQueryable<FeatureFlagOverride> ActiveOverrides()
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        return context.FeatureFlagOverrides.Where(o => o.ExpiresAt == null || o.ExpiresAt > utcNow);
    }
}
