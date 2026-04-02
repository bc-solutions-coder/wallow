using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Domain.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("~/connect/token")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
[IgnoreAntiforgeryToken]
public sealed partial class TokenController(
    UserManager<WallowUser> userManager,
    ILogger<TokenController> logger) : Controller
{
    // OAuth token endpoint — antiforgery tokens are not applicable for machine-to-machine OAuth flows
#pragma warning disable CA5391
    [HttpPost, Produces("application/json")]
    public async Task<IActionResult> Exchange()
#pragma warning restore CA5391
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        LogTokenRequest(request.GrantType, request.ClientId);

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            return await HandleAuthorizationCodeOrRefreshAsync();
        }

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsAsync();
        }

        LogUnsupportedGrantType(request.GrantType);
        return Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedGrantType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The specified grant type is not supported."
            }));
    }

    private async Task<IActionResult> HandleAuthorizationCodeOrRefreshAsync()
    {
        AuthenticateResult result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = result.Principal
            ?? throw new InvalidOperationException("The authenticated principal cannot be retrieved.");

        string? subject = principal.GetClaim(Claims.Subject);
        LogTokenCodeExchange(subject);
        WallowUser? user = await userManager.FindByIdAsync(subject!);

        if (user is null)
        {
            LogTokenUserNotFound(subject);
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user associated with this token no longer exists."
                }));
        }

        ClaimsIdentity identity = new(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(Claims.Email, user.Email);
        identity.SetClaim(Claims.Name, user.UserName);
        identity.SetClaim(Claims.GivenName, user.FirstName);
        identity.SetClaim(Claims.FamilyName, user.LastName);

        IList<string> roles = await userManager.GetRolesAsync(user);
        foreach (string role in roles)
        {
            identity.AddClaim(Claims.Role, role);
        }

        // Carry forward tenant claims from the original principal
        string? orgId = principal.GetClaim("org_id");
        if (orgId is not null)
        {
            identity.SetClaim("org_id", orgId);
        }

        string? orgName = principal.GetClaim("org_name");
        if (orgName is not null)
        {
            identity.SetClaim("org_name", orgName);
        }

        foreach (Claim claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        ClaimsPrincipal claimsPrincipal = new(identity);
        claimsPrincipal.SetScopes(principal.GetScopes());

        string tokenScopes = string.Join(" ", principal.GetScopes());
        LogTokenIssued(subject, tokenScopes);
        return SignIn(claimsPrincipal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleClientCredentialsAsync()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        ClaimsIdentity identity = new(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        string? clientId = request.ClientId;
        identity.SetClaim(Claims.Subject, clientId);
        identity.SetClaim(Claims.AuthorizedParty, clientId);

        // Extract tenant_id from service account client_id pattern: sa-{tenantId}-{name}
        if (clientId is not null)
        {
            string[] parts = clientId.Split('-');
            if (parts.Length >= 3 && parts[0] == "sa")
            {
                identity.SetClaim("tenant_id", parts[1]);
            }
        }

        foreach (Claim claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        ClaimsPrincipal claimsPrincipal = new(identity);
        claimsPrincipal.SetScopes(request.GetScopes());

        return SignIn(claimsPrincipal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC token request: grant_type={GrantType}, client_id={ClientId}")]
    private partial void LogTokenRequest(string? grantType, string? clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC token unsupported grant type: {GrantType}")]
    private partial void LogUnsupportedGrantType(string? grantType);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC token code/refresh exchange for subject={Subject}")]
    private partial void LogTokenCodeExchange(string? subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC token user not found for subject={Subject}")]
    private partial void LogTokenUserNotFound(string? subject);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC token issued for subject={Subject}, scopes={Scopes}")]
    private partial void LogTokenIssued(string? subject, string scopes);

    private static ImmutableArray<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            Claims.Name or Claims.Email or Claims.GivenName or Claims.FamilyName
                when claim.Subject?.HasScope(Scopes.Profile) == true || claim.Subject?.HasScope(Scopes.Email) == true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Role
                when claim.Subject?.HasScope(Scopes.Roles) == true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            "tenant_id" or "org_id" or "org_name" => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],

            _ => [Destinations.AccessToken]
        };
    }
}
