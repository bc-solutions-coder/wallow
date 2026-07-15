namespace Wallow.Identity.Api.Contracts.Responses;

public sealed record InitialAccessTokenResponse(
    string Id,
    string DisplayName,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked);

/// <summary>
/// Returned only once at creation time; includes the raw token value.
/// </summary>
public sealed record InitialAccessTokenCreatedResponse(
    string Id,
    string Token,
    string DisplayName,
    DateTimeOffset? ExpiresAt);
