using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Application.FeatureFlags.Commands.CreateOverride;

public sealed class CreateOverrideHandler(
    IFeatureFlagRepository flagRepo,
    IFeatureFlagOverrideRepository overrideRepo,
    IDistributedCache cache,
    TimeProvider timeProvider)
{
    public async Task<Result<Guid>> Handle(CreateOverrideCommand cmd, CancellationToken ct)
    {
        FeatureFlagId flagId = FeatureFlagId.Create(cmd.FlagId);
        FeatureFlag? flag = await flagRepo.GetByIdAsync(flagId, ct);

        if (flag is null)
        {
            return Result.Failure<Guid>(Error.NotFound("FeatureFlag", cmd.FlagId));
        }

        if (!cmd.TenantId.HasValue && !cmd.UserId.HasValue)
        {
            return Result.Failure<Guid>(Error.Validation("Must specify either TenantId or UserId"));
        }

        FeatureFlagOverride? existing = await overrideRepo.GetOverrideAsync(flagId, cmd.TenantId, cmd.UserId, ct);
        if (existing is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("Override already exists for this scope"));
        }

        FeatureFlagOverride over;

        if (cmd.TenantId.HasValue && cmd.UserId.HasValue)
        {
            over = FeatureFlagOverride.CreateForTenantUser(
                flagId, cmd.TenantId.Value, cmd.UserId.Value,
                cmd.IsEnabled, timeProvider, cmd.Variant, cmd.ExpiresAt);
        }
        else if (cmd.UserId.HasValue)
        {
            over = FeatureFlagOverride.CreateForUser(
                flagId, cmd.UserId.Value,
                cmd.IsEnabled, timeProvider, cmd.Variant, cmd.ExpiresAt);
        }
        else
        {
            over = FeatureFlagOverride.CreateForTenant(
                flagId, cmd.TenantId!.Value,
                cmd.IsEnabled, timeProvider, cmd.Variant, cmd.ExpiresAt);
        }

        await overrideRepo.AddAsync(over, ct);

        await cache.RemoveAsync($"ff:{flag.Key}", ct);

        return Result.Success(over.Id.Value);
    }
}
