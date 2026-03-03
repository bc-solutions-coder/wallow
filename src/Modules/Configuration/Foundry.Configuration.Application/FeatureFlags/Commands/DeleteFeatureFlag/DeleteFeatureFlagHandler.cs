using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Application.FeatureFlags.Commands.DeleteFeatureFlag;

public sealed class DeleteFeatureFlagHandler(
    IFeatureFlagRepository repository,
    IDistributedCache cache)
{
    public async Task<Result> Handle(DeleteFeatureFlagCommand cmd, CancellationToken ct)
    {
        FeatureFlagId flagId = FeatureFlagId.Create(cmd.Id);
        FeatureFlag? flag = await repository.GetByIdAsync(flagId, ct);

        if (flag is null)
        {
            return Result.Failure(Error.NotFound("FeatureFlag", cmd.Id));
        }

        flag.MarkDeleted();

        await repository.DeleteAsync(flag, ct);

        await cache.RemoveAsync($"ff:{flag.Key}", ct);

        return Result.Success();
    }
}
