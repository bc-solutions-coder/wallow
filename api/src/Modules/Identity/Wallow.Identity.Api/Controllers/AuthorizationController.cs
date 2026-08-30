using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Extensions;
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
    IUserEnrollmentService enrollment,
    IMembershipRoleResolver membershipRoleResolver,
    ISsoClientSessionService ssoClientSessionService,
    IConsentTokenService consentTokenService,
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

        // A membership row is not permission to sign in here; only an Active one is. Whether one
        // can be minted on the spot is the organization's enrollment policy to answer, and that
        // answer — along with the email-verification precondition and the three refusal reasons —
        // lives in the enrollment service, because AccountController has to reach the same one.
        // Global admin governs across organizations, so it is not gated by a membership at all:
        // it is the one authority an organization does not grant.
        bool isGlobalAdmin = GlobalAdminClaims.IsGranted(await userManager.GetClaimsAsync(user));

        if (!isGlobalAdmin)
        {
            EnrollmentOutcome outcome = await enrollment.EnrollAsync(Guid.Parse(userId), tenantInfo.TenantId);
            LogEnrollmentOutcome(userId, tenantInfo.TenantId, outcome.GetType().Name);

            switch (outcome)
            {
                case Enrolled:
                    break;

                // Not a refusal: the request was accepted and the pending membership recorded,
                // so this goes to the screen that says so rather than to the error page.
                case PendingApproval:
                    return Redirect($"{GetRequiredAuthUrl()}/access-request");

                case Rejected rejected:
                    return Redirect($"{GetRequiredAuthUrl()}/error?reason={rejected.Reason}");

                default:
                    throw new InvalidOperationException(
                        $"Unhandled enrollment outcome '{outcome.GetType().Name}'.");
            }
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

            // A consent decision counts only when it is POSTed with the token this endpoint minted
            // for this user and this request, and only once. Anything else — a flag on the GET,
            // no token, a replayed token, a token minted for someone else or for another request —
            // leaves the user on the consent screen with a fresh token: the relying party is told
            // neither yes nor no, and nothing is recorded.
            string fingerprint = ConsentRequestFingerprint(request);
            string? decision = HttpMethods.IsPost(Request.Method)
                ? request[ConsentDecisionParameter]?.ToString()
                : null;

            if (decision is not null)
            {
                ConsentTokenOutcome tokenOutcome = await consentTokenService.RedeemAsync(
                    request[ConsentTokenParameter]?.ToString(), userId, fingerprint, HttpContext.RequestAborted);
                if (tokenOutcome != ConsentTokenOutcome.Redeemed)
                {
                    LogConsentDecisionRefused(clientId, tokenOutcome);
                    return RedirectToConsent(request, userId, clientId, grantedScopes, fingerprint);
                }

                if (string.Equals(decision, ConsentDenied, StringComparison.Ordinal))
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

                if (string.Equals(decision, ConsentGranted, StringComparison.Ordinal) && !hasValidAuthorization)
                {
                    // Store a permanent authorization so consent is not re-prompted.
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
            }

            if (!hasValidAuthorization)
            {
                return RedirectToConsent(request, userId, clientId, grantedScopes, fingerprint);
            }
        }

        // The sid ties every RP that completes authorize to one SSO session, so logout can
        // tell each of them which session ended. It has to exist before the id_token that
        // carries it is built.
        string sid = await EnsureSessionIdAsync();
        if (clientId is not null)
        {
            await ssoClientSessionService.RecordAsync(
                sid, clientId, Guid.Parse(userId), HttpContext.RequestAborted);
        }

        ClaimsIdentity identity = await BuildClaimsIdentityAsync(user, userId, roles, grantedScopes, tenantInfo, sid);

        string allScopes = string.Join(" ", grantedScopes);
        LogIssuingAuthorizationCode(userId, clientId, allScopes);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Returns the SSO session identifier from the caller's identity cookie, minting one and
    /// re-issuing the cookie when a session predates front-channel logout and carries none.
    /// The sid deliberately lives on the cookie rather than per-request state: it must stay
    /// identical across every authorize the session performs, or logout notifications would
    /// name a session no RP ever recorded.
    /// </summary>
    private async Task<string> EnsureSessionIdAsync()
    {
        string? sid = User.GetSessionId();
        if (sid is not null)
        {
            return sid;
        }

        sid = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        AuthenticateResult cookie = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (cookie.Succeeded && cookie.Principal.Identity is ClaimsIdentity cookieIdentity)
        {
            cookieIdentity.AddClaim(new Claim(ClaimsPrincipalExtensions.SessionIdClaimType, sid));
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, cookie.Principal, cookie.Properties);
        }

        return sid;
    }

    private async Task<ClaimsIdentity> BuildClaimsIdentityAsync(
        WallowUser user,
        string userId,
        IReadOnlyList<string> roles,
        ImmutableArray<string> grantedScopes,
        ClientTenantInfo tenantInfo,
        string sid)
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

        identity.AddClaim(ClaimsPrincipalExtensions.SessionIdClaimType, sid);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consent decision for client {ClientId} refused ({Outcome}); asking again")]
    private partial void LogConsentDecisionRefused(string? clientId, ConsentTokenOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC enrollment outcome: userId={UserId}, organizationId={OrganizationId}, outcome={Outcome}")]
    private partial void LogEnrollmentOutcome(string userId, Guid organizationId, string outcome);

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

            // sid exists solely so the RP can match a front-channel logout notification to
            // the session it belongs to — an id_token concern with no access-token consumer.
            ClaimsPrincipalExtensions.SessionIdClaimType => [Destinations.IdentityToken],

            _ => [Destinations.AccessToken]
        };
    }
}
