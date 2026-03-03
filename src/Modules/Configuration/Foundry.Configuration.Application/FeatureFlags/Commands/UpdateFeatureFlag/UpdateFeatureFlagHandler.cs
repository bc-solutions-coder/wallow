using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Mappings;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.Events;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;
using Wolverine;

namespace Foundry.Configuration.Application.FeatureFlags.Commands.UpdateFeatureFlag;

public sealed class UpdateFeatureFlagHandler(
    IFeatureFlagRepository repository,
    IDistributedCache cache,
    IMessageBus bus,
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

        List<string> changedProperties = new();

        if (flag.Name != cmd.Name)
        {
            changedProperties.Add(nameof(FeatureFlag.Name));
        }

        if (flag.Description != cmd.Description)
        {
            changedProperties.Add(nameof(FeatureFlag.Description));
        }

        if (flag.DefaultEnabled != cmd.DefaultEnabled)
        {
            changedProperties.Add(nameof(FeatureFlag.DefaultEnabled));
        }

        flag.Update(cmd.Name, cmd.Description, cmd.DefaultEnabled, timeProvider);

        if (flag.FlagType == FlagType.Percentage && cmd.RolloutPercentage.HasValue)
        {
            if (flag.RolloutPercentage != cmd.RolloutPercentage.Value)
            {
                changedProperties.Add(nameof(FeatureFlag.RolloutPercentage));
            }

            flag.UpdatePercentage(cmd.RolloutPercentage.Value, timeProvider);
        }

        await repository.UpdateAsync(flag, ct);

        await cache.RemoveAsync($"ff:{flag.Key}", ct);

        string changedProps = string.Join(",", changedProperties);
        await bus.PublishAsync(new FeatureFlagUpdatedEvent(flag.Id.Value, flag.Key, changedProps));

        return Result.Success(flag.ToDto());
    }
}
