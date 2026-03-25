namespace Wallow.ApiKeys.Api.Contracts.Requests;

/// <summary>
/// Request to create a new API key.
/// </summary>
public sealed record CreateApiKeyRequest(
    string Name,
    IReadOnlyList<string>? Scopes = null,
    DateTimeOffset? ExpiresAt = null);
