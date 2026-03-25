namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record CreateInitialAccessTokenRequest(
    string DisplayName,
    DateTimeOffset? ExpiresAt = null);
