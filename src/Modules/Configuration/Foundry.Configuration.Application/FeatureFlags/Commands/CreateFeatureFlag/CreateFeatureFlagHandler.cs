using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Mappings;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Application.FeatureFlags.Commands.CreateFeatureFlag;

public sealed class CreateFeatureFlagHandler(
    IFeatureFlagRepository repository,
    IDistributedCache cache,
    TimeProvider timeProvider)
{
    public async Task<Result<FeatureFlagDto>> Handle(CreateFeatureFlagCommand cmd, CancellationToken ct)
    {
        FeatureFlag? existing = await repository.GetByKeyAsync(cmd.Key, ct);
        if (existing is not null)
        {
            return Result.Failure<FeatureFlagDto>(Error.Conflict($"Flag with key '{cmd.Key}' already exists"));
        }

        FeatureFlag flag = cmd.FlagType switch
        {
            FlagType.Boolean => FeatureFlag.CreateBoolean(cmd.Key, cmd.Name, cmd.DefaultEnabled, timeProvider, cmd.Description),
            FlagType.Percentage => FeatureFlag.CreatePercentage(cmd.Key, cmd.Name, cmd.RolloutPercentage ?? 0, timeProvider, cmd.Description),
            FlagType.Variant => FeatureFlag.CreateVariant(
                cmd.Key, cmd.Name,
                cmd.Variants?.Select(v => new VariantWeight(v.Name, v.Weight)).ToList() ?? [],
                cmd.DefaultVariant ?? "", timeProvider, cmd.Description),
            _ => throw new ArgumentOutOfRangeException(nameof(cmd), cmd.FlagType, $"Unsupported flag type: {cmd.FlagType}")
        };

        await repository.AddAsync(flag, ct);

        await cache.RemoveAsync($"ff:{flag.Key}", ct);

        return Result.Success(flag.ToDto());
    }
}
