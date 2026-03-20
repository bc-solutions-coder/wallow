namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Response containing API key metadata.
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
/// Response when creating a new API key (includes the full key).
/// </summary>
public sealed record ApiKeyCreatedResponse(
    string KeyId,
    string ApiKey,
    string Prefix,
    string Name,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt);
