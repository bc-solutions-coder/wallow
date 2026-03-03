using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;

namespace Foundry.Configuration.Application.FeatureFlags.Contracts;

public interface IFeatureFlagOverrideRepository
{
    Task<FeatureFlagOverride?> GetByIdAsync(FeatureFlagOverrideId id, CancellationToken ct = default);

    Task<IReadOnlyList<FeatureFlagOverride>> GetOverridesForFlagAsync(
        FeatureFlagId flagId,
        CancellationToken ct = default);

    Task<FeatureFlagOverride?> GetOverrideAsync(
        FeatureFlagId flagId,
        Guid? tenantId,
        Guid? userId,
        CancellationToken ct = default);

    Task AddAsync(FeatureFlagOverride over, CancellationToken ct = default);
    Task DeleteAsync(FeatureFlagOverride over, CancellationToken ct = default);
}
