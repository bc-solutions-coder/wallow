using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Mappings;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Application.FeatureFlags.Commands.UpdateFeatureFlag;

public sealed class UpdateFeatureFlagHandler(
    IFeatureFlagRepository repository,
    IDistributedCache cache,
    TimeProvider timeProvider)
{
    public async Task<Result<FeatureFlagDto>> Handle(UpdateFeatureFlagCommand cmd, CancellationToken ct)
    {
        FeatureFlagId flagId = FeatureFlagId.Create(cmd.Id);
        FeatureFlag? flag = await repository.GetByIdAsync(flagId, ct);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>(Error.NotFound("FeatureFlag", cmd.Id));
        }

        flag.Update(cmd.Name, cmd.Description, cmd.DefaultEnabled, timeProvider);

        if (flag.FlagType == FlagType.Percentage && cmd.RolloutPercentage.HasValue)
        {
            flag.UpdatePercentage(cmd.RolloutPercentage.Value, timeProvider);
        }

        await repository.UpdateAsync(flag, ct);

        await cache.RemoveAsync($"ff:{flag.Key}", ct);

        return Result.Success(flag.ToDto());
    }
}
