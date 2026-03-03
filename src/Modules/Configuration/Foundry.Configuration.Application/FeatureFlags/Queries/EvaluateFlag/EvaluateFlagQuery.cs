namespace Foundry.Configuration.Application.FeatureFlags.Queries.EvaluateFlag;

public sealed record EvaluateFlagQuery(string Key, Guid TenantId, Guid? UserId);
