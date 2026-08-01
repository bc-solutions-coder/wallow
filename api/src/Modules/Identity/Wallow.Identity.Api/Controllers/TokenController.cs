using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using static OpenIddict.Abstractions.OpenIddictConstants;
// Aliased rather than imported: the namespace's GetScopes extension collides with OpenIddict's.
using WallowClaims = Wallow.Shared.Kernel.Extensions.ClaimsPrincipalExtensions;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("~/connect/token")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed partial class TokenController(
    UserManager<WallowUser> userManager,
    IOpenIddictApplicationManager applicationManager,
    IMembershipRepository memberships,
    IMembershipRoleResolver membershipRoleResolver,
    ILogger<TokenController> logger) : Controller
{
    /// <summary>
    /// The resource every issued access token is restricted to; OpenIddict turns the principal's
    /// resources into the token's aud claim. Deliberately spelled out here and again in the
    /// validation handler's AddAudiences call rather than shared as a constant — the two sides are
    /// a contract, and a shared symbol would let them agree without the value reaching a token.
    /// </summary>
    private const string ApiAudience = "wallow-api";


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

        // Read from the user's own claim store rather than carrying the flag forward from the
        // incoming principal: a refresh token must never be able to keep a revoked global admin
        // alive, and nothing a tenant controls may introduce it.
        bool isGlobalAdmin = GlobalAdminClaims.IsGranted(await userManager.GetClaimsAsync(user));
        if (isGlobalAdmin)
        {
            identity.SetClaim(WallowClaims.GlobalAdminClaimType, "true");
        }

        // Carry forward tenant claims from the original principal. The organization has to be
        // settled before the roles, because it is what decides them.
        string? orgId = principal.GetClaim("org_id");
        if (orgId is not null)
        {
            identity.SetClaim("org_id", orgId);
        }

        Guid? organizationId = orgId is not null && Guid.TryParse(orgId, out Guid parsedOrganizationId)
            ? parsedOrganizationId
            : null;

        // The membership is re-read, never trusted from the incoming principal: a refresh token
        // must not outlive the organization's decision about the person holding it. Global admin
        // governs across organizations, so no organization gates it.
        if (!isGlobalAdmin && organizationId is not null)
        {
            Membership? membership = await memberships.GetAsync(
                user.Id, organizationId.Value, HttpContext.RequestAborted);

            if (membership is not { Status: MembershipStatus.Active })
            {
                string membershipStatus = membership is null ? "none" : membership.Status.ToString();
                LogMembershipNotActive(subject, organizationId.Value, membershipStatus);

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer an active member of this organization."
                    }));
            }
        }

        // Re-resolved from the membership rather than carried forward, for the same reason: a
        // refresh token must not keep a role alive after the organization has taken it away. A
        // token naming no organization earns no roles at all.
        if (organizationId is not null)
        {
            IReadOnlyList<string> roles =
                await membershipRoleResolver.GetRoleNamesAsync(user.Id, organizationId.Value);

            foreach (string role in roles)
            {
                identity.AddClaim(Claims.Role, role);
            }
        }

        string? orgName = principal.GetClaim("org_name");
        if (orgName is not null)
        {
            identity.SetClaim("org_name", orgName);
        }

        ClaimsPrincipal claimsPrincipal = new(identity);
        claimsPrincipal.SetScopes(principal.GetScopes());
        claimsPrincipal.SetResources(ApiAudience);

        foreach (Claim claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

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

        if (clientId is not null)
        {
            ImmutableDictionary<string, JsonElement> properties = await GetApplicationPropertiesAsync(clientId);

            if (properties.TryGetValue(ClientApplicationProperties.TenantId, out JsonElement tenant)
                && tenant.ValueKind == JsonValueKind.String
                && tenant.GetString() is { Length: > 0 } tenantId)
            {
                // "org_id" is the one spelling ClaimsPrincipalExtensions.GetTenantId reads, so
                // it is the one spelling that makes a service account resolve to a tenant.
                identity.SetClaim("org_id", tenantId);
            }

            if (properties.TryGetValue(ClientApplicationProperties.IsOperator, out JsonElement isOperator)
                && isOperator.ValueKind == JsonValueKind.True)
            {
                identity.SetClaim(WallowClaims.OperatorClaimType, "true");
            }
        }

        ClaimsPrincipal claimsPrincipal = new(identity);
        claimsPrincipal.SetScopes(request.GetScopes());
        claimsPrincipal.SetResources(ApiAudience);

        foreach (Claim claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        return SignIn(claimsPrincipal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ImmutableDictionary<string, JsonElement>> GetApplicationPropertiesAsync(string clientId)
    {
        object? application = await applicationManager.FindByClientIdAsync(clientId, HttpContext.RequestAborted);
        if (application is null)
        {
            return ImmutableDictionary<string, JsonElement>.Empty;
        }

        return await applicationManager.GetPropertiesAsync(application, HttpContext.RequestAborted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC token request: grant_type={GrantType}, client_id={ClientId}")]
    private partial void LogTokenRequest(string? grantType, string? clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC token unsupported grant type: {GrantType}")]
    private partial void LogUnsupportedGrantType(string? grantType);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC token code/refresh exchange for subject={Subject}")]
    private partial void LogTokenCodeExchange(string? subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC token user not found for subject={Subject}")]
    private partial void LogTokenUserNotFound(string? subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC token refused for subject={Subject}: membership in organizationId={OrganizationId} is {Status}")]
    private partial void LogMembershipNotActive(string? subject, Guid organizationId, string status);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC token issued for subject={Subject}, scopes={Scopes}")]
    private partial void LogTokenIssued(string? subject, string scopes);

    private static ImmutableArray<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Name
                when claim.Subject?.HasScope(Scopes.Profile) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Email
                when claim.Subject?.HasScope(Scopes.Email) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.GivenName or Claims.FamilyName
                when claim.Subject?.HasScope(Scopes.Profile) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            Claims.Role
                when claim.Subject?.HasScope(Scopes.Roles) is true
                => [Destinations.AccessToken, Destinations.IdentityToken],

            "org_id" or "org_name" => [Destinations.AccessToken, Destinations.IdentityToken],

            _ => [Destinations.AccessToken]
        };
    }
}
