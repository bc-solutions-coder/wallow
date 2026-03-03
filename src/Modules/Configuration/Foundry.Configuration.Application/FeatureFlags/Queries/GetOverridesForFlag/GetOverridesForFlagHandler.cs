using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Mappings;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Configuration.Application.FeatureFlags.Queries.GetOverridesForFlag;

public sealed class GetOverridesForFlagHandler(
    IFeatureFlagOverrideRepository repository,
    ITenantContext tenantContext)
{
    public async Task<Result<IReadOnlyList<FeatureFlagOverrideDto>>> Handle(
        GetOverridesForFlagQuery query,
        CancellationToken ct)
    {
        FeatureFlagId flagId = FeatureFlagId.Create(query.FlagId);
        IReadOnlyList<FeatureFlagOverride> overrides = await repository.GetOverridesForFlagAsync(flagId, ct);
        Guid callerTenantId = tenantContext.TenantId.Value;
        List<FeatureFlagOverrideDto> dtos = overrides
            .Where(o => o.TenantId == callerTenantId)
            .Select(o => o.ToDto())
            .ToList();
        return Result.Success<IReadOnlyList<FeatureFlagOverrideDto>>(dtos);
    }
}
