namespace Foundry.Configuration.Application.FeatureFlags.DTOs;

public sealed record FlagEvaluationResultDto(
    string Key,
    bool IsEnabled,
    string? Variant);
