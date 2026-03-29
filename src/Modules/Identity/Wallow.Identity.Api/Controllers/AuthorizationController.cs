using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Controllers;

[ExcludeFromCodeCoverage]
[Controller]
[Route("connect/authorize")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class AuthorizationController(
    UserManager<WallowUser> userManager,
    IConfiguration configuration,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IClientTenantResolver clientTenantResolver,
    IOrganizationService organizationService) : Controller
{
    private const string FirstPartyClientPrefix = "wallow-";

    [HttpGet]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (User.Identity is not { IsAuthenticated: true })
        {
            string authUrl = GetRequiredAuthUrl();
            string returnUrl = Request.PathBase + Request.Path + Request.QueryString;

            // Reject non-local URLs to prevent open-redirect attacks.
            // Note: Uri.TryCreate with UriKind.Absolute treats Unix paths (starting with /)
            // as absolute file:// URIs on macOS/Linux, so we use Url.IsLocalUrl instead.
            if (!Url.IsLocalUrl(returnUrl))
            {
                return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
            }

            return Redirect($"{authUrl}/login?returnUrl={Uri.EscapeDataString(returnUrl)}" +
                $"&client_id={Uri.EscapeDataString(request.ClientId ?? string.Empty)}");
        }

        string userId = userManager.GetUserId(User)
            ?? throw new InvalidOperationException("The user identifier cannot be retrieved.");

        WallowUser user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        object application = await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("The application details cannot be retrieved.");

        string? clientId = await applicationManager.GetClientIdAsync(application);
        bool isFirstParty = clientId?.StartsWith(FirstPartyClientPrefix, StringComparison.OrdinalIgnoreCase) is true;

        if (!isFirstParty)
        {
            ImmutableArray<string> requestedScopes = request.GetScopes();
            string applicationId = (await applicationManager.GetIdAsync(application))!;

            // Check for an existing valid authorization for this user+client+scopes combination
            bool hasValidAuthorization = false;
            await foreach (object authorization in authorizationManager.FindBySubjectAsync(userId))
            {
                string? authAppId = await authorizationManager.GetApplicationIdAsync(authorization);
                if (authAppId != applicationId)
                {
                    continue;
                }

                string? status = await authorizationManager.GetStatusAsync(authorization);
                if (status != Statuses.Valid)
                {
                    continue;
                }

                ImmutableArray<string> authorizedScopes = await authorizationManager.GetScopesAsync(authorization);
                if (requestedScopes.All(s => authorizedScopes.Contains(s)))
                {
                    hasValidAuthorization = true;
                    break;
                }
            }

            if (!hasValidAuthorization)
            {
                // No existing consent — redirect to consent screen.
                // The consent UI will POST back to accept/deny.
                string authUrl = GetRequiredAuthUrl();
                string returnUrl = Request.PathBase + Request.Path + Request.QueryString;
                return Redirect($"{authUrl}/consent?returnUrl={Uri.EscapeDataString(returnUrl)}" +
                    $"&client_id={Uri.EscapeDataString(clientId ?? string.Empty)}");
            }
        }

        // Resolve tenant from client_id for org-scoped claims
        ClientTenantInfo? tenantInfo = null;
        if (request.ClientId is not null)
        {
            tenantInfo = await clientTenantResolver.ResolveAsync(request.ClientId);
        }

        // Verify user is a member of the resolved tenant before issuing tokens
        if (tenantInfo is not null)
        {
            IReadOnlyList<OrganizationDto> userOrgs = await organizationService.GetUserOrganizationsAsync(Guid.Parse(userId));
            bool isMember = userOrgs.Any(o => o.Id == tenantInfo.TenantId);
            if (!isMember)
            {
                string authUrl = GetRequiredAuthUrl();
                return Redirect($"{authUrl}/error?reason=not_a_member");
            }
        }

        ClaimsIdentity identity = await BuildClaimsIdentityAsync(user, userId, request, tenantInfo);

        if (!isFirstParty)
        {
            // Store a permanent authorization so consent is not re-prompted
            string applicationId = (await applicationManager.GetIdAsync(application))!;
            OpenIddictAuthorizationDescriptor authorizationDescriptor = new()
            {
                ApplicationId = applicationId,
                CreationDate = DateTimeOffset.UtcNow,
                Principal = new ClaimsPrincipal(identity),
                Status = Statuses.Valid,
                Subject = userId,
                Type = AuthorizationTypes.Permanent
            };

            foreach (string scope in request.GetScopes())
            {
                authorizationDescriptor.Scopes.Add(scope);
            }

            await authorizationManager.CreateAsync(authorizationDescriptor);
        }

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsIdentity> BuildClaimsIdentityAsync(
        WallowUser user, string userId, OpenIddictRequest request, ClientTenantInfo? tenantInfo)
    {
        ClaimsIdentity identity = new(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        identity.AddClaim(Claims.Subject, userId);

        string? userName = await userManager.GetUserNameAsync(user);
        if (userName is not null)
        {
            identity.AddClaim(Claims.Name, userName);
        }

        string? email = await userManager.GetEmailAsync(user);
        if (email is not null)
        {
            identity.AddClaim(Claims.Email, email);
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        foreach (string role in roles)
        {
            identity.AddClaim(Claims.Role, role);
        }

        IList<Claim> existingClaims = await userManager.GetClaimsAsync(user);

        Claim? givenName = existingClaims.FirstOrDefault(c => c.Type == Claims.GivenName);
        if (givenName is not null)
        {
            identity.AddClaim(givenName);
        }

        Claim? familyName = existingClaims.FirstOrDefault(c => c.Type == Claims.FamilyName);
        if (familyName is not null)
        {
            identity.AddClaim(familyName);
        }

        if (tenantInfo is not null)
        {
            identity.AddClaim("org_id", tenantInfo.TenantId.ToString());
            if (tenantInfo.TenantName is not null)
            {
                identity.AddClaim("org_name", tenantInfo.TenantName);
            }
        }

        identity.SetScopes(request.GetScopes());

        foreach (Claim claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        return identity;
    }

    private string GetRequiredAuthUrl() =>
        configuration["AuthUrl"] ?? throw new InvalidOperationException(
            "AuthUrl must be configured in appsettings.json. " +
            "Example: \"AuthUrl\": \"https://auth.yourdomain.com\"");

    // OAuth authorization endpoint — antiforgery tokens are not applicable for OAuth flows
#pragma warning disable CA5391
    [HttpPost]
    public Task<IActionResult> AuthorizePost() => Authorize();
#pragma warning restore CA5391

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
