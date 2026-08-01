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
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
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
    IScopeSubsetValidator scopeSubsetValidator,
    IClientTenantResolver clientTenantResolver,
    IOrganizationService organizationService,
    IMembershipRoleResolver membershipRoleResolver,
    ILogger<AuthorizationController> logger) : Controller
{
    private const string FirstPartyClientPrefix = "wallow-";

    // Clients explicitly listed here skip the consent screen, just like wallow-* clients.
    // Read once at construction time; overridable via Identity__FirstPartyClients__0=... env vars.
    private readonly HashSet<string> _firstPartyClientIds =
        new(
            configuration.GetSection("Identity:FirstPartyClients").Get<string[]>() ?? [],
            StringComparer.OrdinalIgnoreCase);

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
        bool isFirstParty =
            clientId is not null &&
            (clientId.StartsWith(FirstPartyClientPrefix, StringComparison.OrdinalIgnoreCase)
             || _firstPartyClientIds.Contains(clientId));
        bool hasValidAuthorization = false;

        LogApplicationResolved(clientId, isFirstParty);

        // The organization has to be settled before anything else, because everything after it
        // is org-scoped: which roles the caller holds, which scopes those roles reach, and
        // whether they may sign in here at all. It also means a non-member is told so before
        // being walked through a consent screen for an app they cannot use.
        ClientTenantInfo? tenantInfo = request.ClientId is null
            ? null
            : await clientTenantResolver.ResolveAsync(request.ClientId);

        // A client bound to no organization would otherwise yield an org-free token, and a
        // principal naming no organization is exactly what PermissionExpansionMiddleware treats
        // as cross-tenant — so the token would carry scopes with nowhere to spend them, until
        // some downstream tenant resolution supplied a home for them.
        if (tenantInfo is null || tenantInfo.TenantId == Guid.Empty)
        {
            LogClientHasNoOrganization(clientId);
            return Redirect($"{GetRequiredAuthUrl()}/error?reason=client_not_bound_to_organization");
        }

        IReadOnlyList<OrganizationDto> userOrgs = await organizationService.GetUserOrganizationsAsync(Guid.Parse(userId));
        bool isMember = userOrgs.Any(o => o.Id == tenantInfo.TenantId);
        LogTenantMembershipCheck(userId, tenantInfo.TenantId, isMember);
        if (!isMember)
        {
            return Redirect($"{GetRequiredAuthUrl()}/error?reason=not_a_member");
        }

        // Roles are granted by an organization and carry no authority outside it, so this is the
        // only role set that may decide anything here: the scopes granted below and the role
        // claims stamped into the token both read it.
        IReadOnlyList<string> roles =
            await membershipRoleResolver.GetRoleNamesAsync(Guid.Parse(userId), tenantInfo.TenantId);

        // Two independent scope gates, both before any ticket is issued or authorization
        // persisted. Without them a signed-in user can append privileged scopes to their own
        // authorize request and PermissionExpansionMiddleware expands them into permissions.
        // Everything downstream — consent, the stored authorization, the token — runs on the
        // granted set, never on what was asked for.
        (IActionResult? scopeRejection, ImmutableArray<string> grantedScopes) =
            await ResolveGrantedScopesAsync(request, roles, userId, clientId);
        if (scopeRejection is not null)
        {
            return scopeRejection;
        }

        if (!isFirstParty)
        {
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
                if (grantedScopes.All(s => authorizedScopes.Contains(s)))
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

                foreach (string scope in grantedScopes)
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
                // The granted scopes ride along space-delimited (OAuth's own
                // delimiter, and what the consent-info endpoint splits on): they are
                // the substance of the decision the screen asks the user to make, and
                // asking to consent to a scope that will never be issued is a lie.
                string authUrl = GetRequiredAuthUrl();
                string returnUrl = Request.PathBase + Request.Path + Request.QueryString;
                string consentScopes = string.Join(" ", grantedScopes);
                LogRedirectingToConsent(clientId, returnUrl);
                return Redirect($"{authUrl}/consent?returnUrl={Uri.EscapeDataString(returnUrl)}" +
                    $"&client_id={Uri.EscapeDataString(clientId ?? string.Empty)}" +
                    $"&scope={Uri.EscapeDataString(consentScopes)}");
            }
        }

        ClaimsIdentity identity = await BuildClaimsIdentityAsync(user, userId, roles, grantedScopes, tenantInfo);

        string allScopes = string.Join(" ", grantedScopes);
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

            foreach (string scope in grantedScopes)
            {
                authorizationDescriptor.Scopes.Add(scope);
            }

            await authorizationManager.CreateAsync(authorizationDescriptor);
        }

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsIdentity> BuildClaimsIdentityAsync(
        WallowUser user,
        string userId,
        IReadOnlyList<string> roles,
        ImmutableArray<string> grantedScopes,
        ClientTenantInfo tenantInfo)
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

        identity.AddClaim("org_id", tenantInfo.TenantId.ToString());
        if (tenantInfo.TenantName is not null)
        {
            identity.AddClaim("org_name", tenantInfo.TenantName);
        }

        identity.SetScopes(grantedScopes);

        foreach (Claim claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        return identity;
    }

    /// <summary>
    /// Resolves the scopes this caller may actually be granted. The two gates answer different
    /// questions and so fail differently.
    /// <para>
    /// Gate one asks whether the scopes are registered for the OIDC client. A scope the client
    /// was never configured for is a client misconfiguration, not a user-privilege question, so
    /// this one refuses the whole request loudly rather than quietly dropping the scope.
    /// </para>
    /// <para>
    /// Gate two asks whether the roles the caller holds IN THIS ORGANIZATION carry each scope's
    /// permission, and narrows instead of refusing: OAuth already lets a server issue fewer
    /// scopes than were asked for, and an app requesting the superset it supports for any user
    /// must still work for the users who only qualify for part of it. Scopes that map to no
    /// permission (openid, profile, email, offline_access, roles) are never role-gated.
    /// </para>
    /// </summary>
    /// <returns>
    /// A rejection result and an empty set when gate one fails; otherwise null and the narrowed
    /// set of scopes, which is what every downstream consumer must use in place of the request's.
    /// </returns>
    private async Task<(IActionResult? Rejection, ImmutableArray<string> GrantedScopes)> ResolveGrantedScopesAsync(
        OpenIddictRequest request, IReadOnlyList<string> roles, string userId, string? clientId)
    {
        ImmutableArray<string> requestedScopes = request.GetScopes();

        ScopeValidationResult clientScopes = await scopeSubsetValidator.ValidateAsync(
            clientId ?? string.Empty, requestedScopes, HttpContext.RequestAborted);

        if (!clientScopes.IsSuccess)
        {
            LogScopesNotRegisteredForClient(clientId, clientScopes.ErrorMessage);
            return (
                InvalidScope(clientScopes.ErrorMessage ?? "The requested scopes are not permitted for this client."),
                []);
        }

        HashSet<string> grantedPermissions =
            new(RolePermissionMapping.GetPermissions(roles), StringComparer.OrdinalIgnoreCase);

        List<string> refusedScopes = [];
        List<string> granted = [];
        foreach (string scope in requestedScopes)
        {
            string? requiredPermission = ScopePermissionMapper.MapScopeToPermission(scope);
            if (requiredPermission is not null && !grantedPermissions.Contains(requiredPermission))
            {
                refusedScopes.Add(scope);
                continue;
            }

            granted.Add(scope);
        }

        if (refusedScopes.Count > 0)
        {
            LogScopesNarrowed(userId, clientId, string.Join(", ", refusedScopes), string.Join(", ", granted));
        }

        return (null, [.. granted]);
    }

    private ForbidResult InvalidScope(string description) =>
        Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(
                new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidScope,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
                }));

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC rejected scopes not registered for client {ClientId}: {Reason}")]
    private partial void LogScopesNotRegisteredForClient(string? clientId, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC refused authorize for client {ClientId}: the client is bound to no organization")]
    private partial void LogClientHasNoOrganization(string? clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC narrowed scopes beyond caller role: userId={UserId}, clientId={ClientId}, dropped={DroppedScopes}, granted={GrantedScopes}")]
    private partial void LogScopesNarrowed(string userId, string? clientId, string droppedScopes, string grantedScopes);

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
