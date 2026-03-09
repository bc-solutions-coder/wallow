using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Configuration.Application.FeatureFlags.Queries.EvaluateFlag;

public sealed class EvaluateFlagHandler(IFeatureFlagService featureFlagService)
{
    public async Task<Result<FlagEvaluationResultDto>> Handle(
        EvaluateFlagQuery query,
        CancellationToken ct)
    {
        bool isEnabled = await featureFlagService.IsEnabledAsync(query.Key, query.TenantId, query.UserId, ct);
        string? variant = await featureFlagService.GetVariantAsync(query.Key, query.TenantId, query.UserId, ct);

        FlagEvaluationResultDto result = new(query.Key, isEnabled, variant);
        return Result.Success(result);
    }
}
