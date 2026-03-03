namespace Foundry.Configuration.Api.Contracts.Responses;

public sealed record FlagEvaluationResponse(
    string Key,
    bool IsEnabled,
    string? Variant);
