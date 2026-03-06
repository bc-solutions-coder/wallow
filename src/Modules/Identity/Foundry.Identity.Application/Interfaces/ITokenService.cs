namespace Foundry.Identity.Application.Interfaces;

/// <summary>
/// Service for obtaining and refreshing OAuth2 tokens from Keycloak.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Obtains an access token using the resource owner password credentials grant.
    /// </summary>
    Task<TokenResult> GetTokenAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    Task<TokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes a refresh token, effectively logging the user out.
    /// </summary>
    Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
}

/// <summary>
/// Result of a token request.
/// </summary>
public sealed record TokenResult(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    string? TokenType,
    int? ExpiresIn,
    int? RefreshExpiresIn,
    string? Scope,
    string? Error,
    string? ErrorDescription);
