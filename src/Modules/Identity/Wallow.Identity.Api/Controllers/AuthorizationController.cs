using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
public sealed partial class AuthorizationController(
    UserManager<WallowUser> userManager,
    IConfiguration configuration,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IClientTenantResolver clientTenantResolver,
    IOrganizationService organizationService,
    ILogger<AuthorizationController> logger) : Controller
{
    private const string FirstPartyClientPrefix = "wallow-";

    [HttpGet]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        LogAuthorizeRequest(request.ClientId, request.RedirectUri, request.ResponseType, request.Scope);

        if (User.Identity is not { IsAuthenticated: true })
        {
            string authUrl = GetRequiredAuthUrl();
            string returnUrl = Request.PathBase + Request.Path + Request.QueryString;

            int cookieCount = Request.Cookies.Count;
            string pathBase = Request.PathBase;
            LogUserNotAuthenticated(returnUrl, pathBase, cookieCount);

            // Reject non-local URLs to prevent open-redirect attacks.
            // Note: Uri.TryCreate with UriKind.Absolute treats Unix paths (starting with /)
            // as absolute file:// URIs on macOS/Linux, so we use Url.IsLocalUrl instead.
            if (!Url.IsLocalUrl(returnUrl))
            {
                LogInvalidReturnUrl(returnUrl);
                return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
            }

            string loginRedirect = $"{authUrl}/login?returnUrl={Uri.EscapeDataString(returnUrl)}" +
                $"&client_id={Uri.EscapeDataString(request.ClientId ?? string.Empty)}";
            LogRedirectingToLogin(loginRedirect);
            return Redirect(loginRedirect);
        }

        string userId = userManager.GetUserId(User)
            ?? throw new InvalidOperationException("The user identifier cannot be retrieved.");

        LogUserAuthenticated(userId, User.Identity.AuthenticationType);

        WallowUser user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        object application = await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("The application details cannot be retrieved.");

        string? clientId = await applicationManager.GetClientIdAsync(application);
        bool isFirstParty = clientId?.StartsWith(FirstPartyClientPrefix, StringComparison.OrdinalIgnoreCase) is true;
        bool hasValidAuthorization = false;

        LogApplicationResolved(clientId, isFirstParty);

        if (!isFirstParty)
        {
            ImmutableArray<string> requestedScopes = request.GetScopes();
            string applicationId = (await applicationManager.GetIdAsync(application))!;

            // Check for an existing valid authorization for this user+client+scopes combination
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

            // Handle consent denial — must be checked before consent grant
            if (string.Equals(request["consent_denied"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(
                        new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                                "The user denied the consent request."
                        }));
            }

            // Handle consent grant — create a permanent authorization if none exists
            if (string.Equals(request["consent_granted"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase)
                && !hasValidAuthorization)
            {
                OpenIddictAuthorizationDescriptor descriptor = new()
                {
                    ApplicationId = applicationId,
                    CreationDate = DateTimeOffset.UtcNow,
                    Status = Statuses.Valid,
                    Subject = userId,
                    Type = AuthorizationTypes.Permanent
                };

                foreach (string scope in requestedScopes)
                {
                    descriptor.Scopes.Add(scope);
                }

                await authorizationManager.CreateAsync(descriptor);
                hasValidAuthorization = true;
            }

            if (!hasValidAuthorization)
            {
                // No existing consent — redirect to consent screen.
                // The consent UI will POST back to accept/deny.
                string authUrl = GetRequiredAuthUrl();
                string returnUrl = Request.PathBase + Request.Path + Request.QueryString;
                LogRedirectingToConsent(clientId, returnUrl);
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
            LogTenantMembershipCheck(userId, tenantInfo.TenantId, isMember);
            if (!isMember)
            {
                string authUrl = GetRequiredAuthUrl();
                return Redirect($"{authUrl}/error?reason=not_a_member");
            }
        }

        ClaimsIdentity identity = await BuildClaimsIdentityAsync(user, userId, request, tenantInfo);

        string allScopes = string.Join(" ", request.GetScopes());
        LogIssuingAuthorizationCode(userId, clientId, allScopes);

        if (!isFirstParty && !hasValidAuthorization)
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

#pragma warning disable CA5391
    [HttpPost]
    public Task<IActionResult> AuthorizePost() => Authorize();
#pragma warning restore CA5391

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC authorize request: client_id={ClientId}, redirect_uri={RedirectUri}, response_type={ResponseType}, scope={Scope}")]
    private partial void LogAuthorizeRequest(string? clientId, string? redirectUri, string? responseType, string? scope);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC user not authenticated. returnUrl={ReturnUrl}, pathBase={PathBase}, cookieCount={CookieCount}")]
    private partial void LogUserNotAuthenticated(string returnUrl, string? pathBase, int cookieCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC rejected non-local returnUrl: {ReturnUrl}")]
    private partial void LogInvalidReturnUrl(string returnUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC redirecting unauthenticated user to login: {LoginUrl}")]
    private partial void LogRedirectingToLogin(string loginUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC user authenticated: userId={UserId}, authType={AuthenticationType}")]
    private partial void LogUserAuthenticated(string userId, string? authenticationType);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC application resolved: clientId={ClientId}, isFirstParty={IsFirstParty}")]
    private partial void LogApplicationResolved(string? clientId, bool isFirstParty);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC redirecting to consent: clientId={ClientId}, returnUrl={ReturnUrl}")]
    private partial void LogRedirectingToConsent(string? clientId, string returnUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC tenant membership check: userId={UserId}, tenantId={TenantId}, isMember={IsMember}")]
    private partial void LogTenantMembershipCheck(string userId, Guid tenantId, bool isMember);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC issuing authorization code: userId={UserId}, clientId={ClientId}, scopes={Scopes}")]
    private partial void LogIssuingAuthorizationCode(string userId, string? clientId, string scopes);

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
