namespace Wallow.ApiKeys.Api.Contracts.Requests;

public sealed record CreateApiKeyRequest(
    string Name,
    IReadOnlyList<string>? Scopes = null,
    DateTimeOffset? ExpiresAt = null);
