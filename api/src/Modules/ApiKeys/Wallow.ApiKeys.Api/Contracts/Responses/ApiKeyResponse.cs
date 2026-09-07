namespace Wallow.ApiKeys.Api.Contracts.Responses;

/// <summary>
/// API key metadata without the plaintext secret.
/// </summary>
public sealed record ApiKeyResponse(
    string KeyId,
    string Name,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt);

/// <summary>
/// Creation response containing the plaintext key, returned only once.
/// </summary>
public sealed record ApiKeyCreatedResponse(
    string KeyId,
    string ApiKey,
    string Prefix,
    string Name,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt);
