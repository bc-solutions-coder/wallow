namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Request for refreshing an access token.
/// </summary>
public sealed record RefreshTokenRequest(
    string RefreshToken);
