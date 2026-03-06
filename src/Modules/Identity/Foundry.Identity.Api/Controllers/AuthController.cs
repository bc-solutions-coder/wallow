using Asp.Versioning;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Foundry.Identity.Api.Controllers;

/// <summary>
/// Authentication endpoints for obtaining and refreshing tokens.
/// These endpoints proxy to Keycloak, hiding the IdP configuration from clients.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;

    public AuthController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    /// <summary>
    /// Obtain an access token using email and password.
    /// </summary>
    /// <remarks>
    /// This endpoint proxies to Keycloak's token endpoint using the resource owner
    /// password credentials grant. The client doesn't need to know any Keycloak
    /// configuration details.
    ///
    /// Example request:
    /// ```json
    /// {
    ///   "email": "user@example.com",
    ///   "password": "password123"
    /// }
    /// ```
    /// </remarks>
    /// <param name="request">The login credentials</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>OAuth2 token response</returns>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetToken([FromBody] TokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Email and password are required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        TokenResult result = await _tokenService.GetTokenAsync(request.Email, request.Password, ct);

        if (!result.Success)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication failed",
                Detail = result.ErrorDescription ?? result.Error ?? "Invalid credentials",
                Status = StatusCodes.Status401Unauthorized,
                Extensions = { ["error"] = result.Error }
            });
        }

        return Ok(new TokenResponse(
            AccessToken: result.AccessToken!,
            RefreshToken: result.RefreshToken,
            TokenType: result.TokenType ?? "Bearer",
            ExpiresIn: result.ExpiresIn ?? 300,
            RefreshExpiresIn: result.RefreshExpiresIn,
            Scope: result.Scope));
    }

    /// <summary>
    /// Refresh an access token using a refresh token.
    /// </summary>
    /// <remarks>
    /// Use this endpoint to obtain a new access token when the current one expires.
    /// The refresh token is typically valid for a longer period than the access token.
    ///
    /// Example request:
    /// ```json
    /// {
    ///   "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    /// }
    /// ```
    /// </remarks>
    /// <param name="request">The refresh token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>New OAuth2 token response</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Refresh token is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        TokenResult result = await _tokenService.RefreshTokenAsync(request.RefreshToken, ct);

        if (!result.Success)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Token refresh failed",
                Detail = result.ErrorDescription ?? result.Error ?? "Invalid or expired refresh token",
                Status = StatusCodes.Status401Unauthorized,
                Extensions = { ["error"] = result.Error }
            });
        }

        return Ok(new TokenResponse(
            AccessToken: result.AccessToken!,
            RefreshToken: result.RefreshToken,
            TokenType: result.TokenType ?? "Bearer",
            ExpiresIn: result.ExpiresIn ?? 300,
            RefreshExpiresIn: result.RefreshExpiresIn,
            Scope: result.Scope));
    }

    /// <summary>
    /// Revoke a refresh token, effectively logging the user out.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Refresh token is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        bool revoked = await _tokenService.RevokeTokenAsync(request.RefreshToken, ct);

        if (!revoked)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Logout failed",
                Detail = "Failed to revoke the token",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return NoContent();
    }
}
