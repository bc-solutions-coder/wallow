using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
using Wallow.Identity.Api.Extensions;
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
    IOrganizationService organizations,
    ISsoClientSessionService ssoClientSessionService,
    IConsentTokenService consentTokenService,
    IClientAccessPolicy clientAccessPolicy,
    ISessionService sessionService,
    ILogger<AuthorizationController> logger) : Controller
{
    /// <summary>
    /// Organization hint for first-party authorization. Bound clients may only restate their own organization.
    /// </summary>
    public const string OrganizationParameter = "organization";

    [HttpGet]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        LogAuthorizeRequest(request.ClientId, request.RedirectUri, request.ResponseType, request.Scope);

        // Refuse unavailable clients on the auth host before asking the user to sign in.
        ClientAccessRefusal? accessRefusal = await clientAccessPolicy.EvaluateAsync(
            request.ClientId, HttpContext.RequestAborted);
        if (accessRefusal is not null)
        {
            LogClientRefused(request.ClientId, accessRefusal.Reason);
            return Redirect($"{GetRequiredAuthUrl()}/error?reason={accessRefusal.Reason}");
        }

        if (User.Identity is not { IsAuthenticated: true })
        {
            // prompt=none requires a protocol error instead of interactive sign-in.
            if (request.HasPromptValue(PromptValues.None))
            {
                return OidcErrorForbid(
                    Errors.LoginRequired,
                    "The user is not signed in, and 'prompt=none' forbids showing a login page.");
            }

            string authUrl = GetRequiredAuthUrl();

            // Preserve POSTed authorize parameters without replaying the consent decision.
            string returnUrl = Request.PathBase + Request.Path + QueryString.Create(AuthorizeParameters(request));

            int cookieCount = Request.Cookies.Count;
            string pathBase = Request.PathBase;
            LogUserNotAuthenticated(returnUrl, pathBase, cookieCount);

            // IsLocalUrl tests web-local paths without treating Unix paths as file URIs.
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

        // Implicit consent marks a first-party application; client-id spelling does not.
        bool isFirstParty = string.Equals(
            await applicationManager.GetConsentTypeAsync(application),
            ConsentTypes.Implicit,
            StringComparison.Ordinal);

        LogApplicationResolved(clientId, isFirstParty);

        // Resolve enrollment and organization roles before consent.
        ClientTenantInfo? tenantInfo = request.ClientId is null
            ? null
            : await clientTenantResolver.ResolveAsync(request.ClientId);

        if (tenantInfo is not null && tenantInfo.TenantId == Guid.Empty)
        {
            tenantInfo = null;
        }

        // Only first-party clients may authorize without an organization binding.
        if (tenantInfo is null && !isFirstParty)
        {
            LogClientHasNoOrganization(clientId);
            return Redirect($"{GetRequiredAuthUrl()}/error?reason=client_not_bound_to_organization");
        }

        // A bound client may confirm its organization, but cannot select another one.
        string? organizationHint = request[OrganizationParameter]?.ToString();
        if (!string.IsNullOrEmpty(organizationHint))
        {
            if (!Guid.TryParse(organizationHint, out Guid hintedOrganizationId) || hintedOrganizationId == Guid.Empty)
            {
                return InvalidRequest($"The '{OrganizationParameter}' parameter must be an organization identifier.");
            }

            if (tenantInfo is not null && tenantInfo.TenantId != hintedOrganizationId)
            {
                LogOrganizationHintContradictsBinding(clientId, hintedOrganizationId, tenantInfo.TenantId);
                return InvalidRequest(
                    $"The '{OrganizationParameter}' parameter names an organization other than the one this client is bound to.");
            }

            if (tenantInfo is null)
            {
                // Enrollment checks access; this lookup supplies the display name.
                OrganizationDto? hintedOrganization = await organizations.GetOrganizationByIdAsync(hintedOrganizationId);
                tenantInfo = new ClientTenantInfo(hintedOrganizationId, hintedOrganization?.Name);
            }
        }

        // Choose a sole active membership; otherwise leave organization context unresolved.
        if (tenantInfo is null)
        {
            IReadOnlyList<MyOrganizationDto> memberships =
                await organizations.GetMyOrganizationsAsync(Guid.Parse(userId));
            if (memberships.Count == 1)
            {
                tenantInfo = new ClientTenantInfo(memberships[0].OrganizationId, memberships[0].Name);
            }
        }

        // Enrollment policy governs organization admission, except for global administrators.
        bool isGlobalAdmin = GlobalAdminClaims.IsGranted(await userManager.GetClaimsAsync(user));

        if (tenantInfo is not null && !isGlobalAdmin)
        {
            EnrollmentOutcome outcome = await enrollment.EnrollAsync(Guid.Parse(userId), tenantInfo.TenantId);
            LogEnrollmentOutcome(userId, tenantInfo.TenantId, outcome.GetType().Name);

            IActionResult? refusal = RefuseEnrollment(outcome, isFirstParty);
            if (refusal is not null)
            {
                return refusal;
            }
        }

        // Roles and scope permissions come from the selected organization.
        IReadOnlyList<string> roles = tenantInfo is null
            ? []
            : await membershipRoleResolver.GetRoleNamesAsync(Guid.Parse(userId), tenantInfo.TenantId);

        // Use the validated, role-narrowed scope set for consent and token issuance.
        (IActionResult? scopeRejection, ImmutableArray<string> grantedScopes) =
            await ResolveGrantedScopesAsync(request, roles, userId, clientId);
        if (scopeRejection is not null)
        {
            return scopeRejection;
        }

        string applicationId = (await applicationManager.GetIdAsync(application))!;

        if (!isFirstParty)
        {
            // Only valid permanent authorizations count as stored consent.
            (object? permanentAuthorization, HashSet<string> consentedScopes) =
                await FindPermanentConsentAsync(userId, applicationId);

            // Ask for missing scopes, or the full granted set when prompting again.
            ImmutableArray<string> missingScopes =
                [.. grantedScopes.Where(s => !consentedScopes.Contains(s))];
            ImmutableArray<string> consentScreenScopes =
                missingScopes.IsEmpty ? grantedScopes : missingScopes;

            // Posted decisions must redeem a token bound to this user and authorize request.
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
                    return RedirectToConsent(request, userId, clientId, consentScreenScopes, fingerprint);
                }

                if (string.Equals(decision, ConsentDenied, StringComparison.Ordinal))
                {
                    return ConsentRequiredForbid("The user denied the consent request.");
                }

                if (!string.Equals(decision, ConsentGranted, StringComparison.Ordinal))
                {
                    return RedirectToConsent(request, userId, clientId, consentScreenScopes, fingerprint);
                }

                // A valid answer satisfies prompt=consent rather than reopening the screen.
                await StoreConsentAsync(
                    permanentAuthorization, applicationId, userId, grantedScopes, tenantInfo);
            }
            else if (request.HasPromptValue(PromptValues.None))
            {
                // prompt=none cannot request missing consent interactively.
                if (permanentAuthorization is null || !missingScopes.IsEmpty)
                {
                    return ConsentRequiredForbid(
                        "The request needs consent the user has not given, and 'prompt=none' forbids asking for it.");
                }

                await StoreConsentAsync(
                    permanentAuthorization, applicationId, userId, grantedScopes, tenantInfo);
            }
            else if (permanentAuthorization is null || !missingScopes.IsEmpty
                || request.HasPromptValue(PromptValues.Consent))
            {
                return RedirectToConsent(request, userId, clientId, consentScreenScopes, fingerprint);
            }
            else
            {
                // Widen the selected record if consent scopes were spread across multiple records.
                await StoreConsentAsync(
                    permanentAuthorization, applicationId, userId, grantedScopes, tenantInfo);
            }
        }

        // Record RP participation under the sid used for logout notifications.
        string sid = await EnsureSessionIdAsync(userId, tenantInfo);
        if (clientId is not null)
        {
            await ssoClientSessionService.RecordAsync(
                sid, clientId, Guid.Parse(userId), HttpContext.RequestAborted);
        }

        // Session and organization metadata let revocation target sign-in tokens without deleting consent.
        string? authorizationId = await CreateAuthorizationAsync(
            AuthorizationTypes.AdHoc, applicationId, userId, grantedScopes, tenantInfo, sid);

        ClaimsIdentity identity = await BuildClaimsIdentityAsync(user, userId, roles, grantedScopes, tenantInfo, sid);
        if (authorizationId is not null)
        {
            identity.SetAuthorizationId(authorizationId);
        }

        string allScopes = string.Join(" ", grantedScopes);
        LogIssuingAuthorizationCode(userId, clientId, allScopes);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Returns the newest valid permanent authorization for this user/client and the union of their scopes.
    /// </summary>
    private async Task<(object? NewestPermanent, HashSet<string> ConsentedScopes)> FindPermanentConsentAsync(
        string userId,
        string applicationId)
    {
        object? newest = null;
        DateTimeOffset? newestCreated = null;
        HashSet<string> consentedScopes = new(StringComparer.Ordinal);

        await foreach (object authorization in authorizationManager.FindBySubjectAsync(userId))
        {
            if (await authorizationManager.GetApplicationIdAsync(authorization) != applicationId
                || await authorizationManager.GetStatusAsync(authorization) != Statuses.Valid
                || await authorizationManager.GetTypeAsync(authorization) != AuthorizationTypes.Permanent)
            {
                continue;
            }

            consentedScopes.UnionWith(await authorizationManager.GetScopesAsync(authorization));

            DateTimeOffset? created = await authorizationManager.GetCreationDateAsync(authorization);
            if (newest is null || (created is not null && (newestCreated is null || created > newestCreated)))
            {
                newest = authorization;
                newestCreated = created;
            }
        }

        return (newest, consentedScopes);
    }

    /// <summary>
    /// Creates permanent consent or widens the selected record. Sign-in tokens use a separate ad-hoc authorization.
    /// </summary>
    private async Task StoreConsentAsync(
        object? permanentAuthorization,
        string applicationId,
        string userId,
        ImmutableArray<string> grantedScopes,
        ClientTenantInfo? tenantInfo)
    {
        if (permanentAuthorization is null)
        {
            await CreateAuthorizationAsync(
                AuthorizationTypes.Permanent, applicationId, userId, grantedScopes, tenantInfo, sessionId: null);
            return;
        }

        OpenIddictAuthorizationDescriptor descriptor = new();
        await authorizationManager.PopulateAsync(descriptor, permanentAuthorization);
        int scopeCountBefore = descriptor.Scopes.Count;
        descriptor.Scopes.UnionWith(grantedScopes);
        if (descriptor.Scopes.Count != scopeCountBefore)
        {
            await authorizationManager.UpdateAsync(permanentAuthorization, descriptor);
        }
    }

    /// <summary>Refuses the relying party with <c>consent_required</c>.</summary>
    private ForbidResult ConsentRequiredForbid(string description) =>
        OidcErrorForbid(Errors.ConsentRequired, description);

    /// <summary>Refuses the relying party with the named OIDC error, at its redirect URI.</summary>
    private ForbidResult OidcErrorForbid(string error, string description) =>
        Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(
                new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
                }));

    private async Task<string?> CreateAuthorizationAsync(
        string type,
        string applicationId,
        string userId,
        ImmutableArray<string> grantedScopes,
        ClientTenantInfo? tenantInfo,
        string? sessionId)
    {
        OpenIddictAuthorizationDescriptor descriptor = new()
        {
            ApplicationId = applicationId,
            CreationDate = DateTimeOffset.UtcNow,
            Status = Statuses.Valid,
            Subject = userId,
            Type = type
        };

        foreach (string scope in grantedScopes)
        {
            descriptor.Scopes.Add(scope);
        }

        if (tenantInfo is not null)
        {
            descriptor.SetOrganizationId(tenantInfo.TenantId);
        }

        if (sessionId is not null)
        {
            descriptor.SetSessionId(sessionId);
        }

        object authorization = await authorizationManager.CreateAsync(descriptor);
        return await authorizationManager.GetIdAsync(authorization);
    }

    /// <summary>
    /// Reuses a live session id or creates an <see cref="ActiveSession"/>.
    /// After creating a session, updates and reissues the Identity cookie if it authenticates.
    /// </summary>
    private async Task<string> EnsureSessionIdAsync(string userId, ClientTenantInfo? tenantInfo)
    {
        Guid userGuid = Guid.Parse(userId);
        string? cookieSid = User.GetSessionId();
        if (cookieSid is not null && await SessionIsLiveAsync(userGuid, cookieSid))
        {
            return cookieSid;
        }

        ActiveSession session = await sessionService.CreateSessionAsync(
            userGuid, tenantInfo?.TenantId ?? Guid.Empty, HttpContext.RequestAborted);
        string sid = session.Sid;

        AuthenticateResult cookie = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (cookie.Succeeded && cookie.Principal.Identity is ClaimsIdentity cookieIdentity)
        {
            // Replace stale sid claims so the cookie carries only the current session id.
            foreach (Claim stale in cookieIdentity.Claims
                .Where(c => c.Type == ClaimsPrincipalExtensions.SessionIdClaimType).ToList())
            {
                cookieIdentity.RemoveClaim(stale);
            }

            cookieIdentity.AddClaim(new Claim(ClaimsPrincipalExtensions.SessionIdClaimType, sid));
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, cookie.Principal, cookie.Properties);
        }

        return sid;
    }

    private async Task<bool> SessionIsLiveAsync(Guid userId, string sid)
    {
        List<ActiveSession> live = await sessionService.GetActiveSessionsAsync(
            userId, HttpContext.RequestAborted);
        return live.Exists(s => string.Equals(s.Sid, sid, StringComparison.Ordinal));
    }

    private async Task<ClaimsIdentity> BuildClaimsIdentityAsync(
        WallowUser user,
        string userId,
        IReadOnlyList<string> roles,
        ImmutableArray<string> grantedScopes,
        ClientTenantInfo? tenantInfo,
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

        if (tenantInfo is not null)
        {
            identity.AddClaim("org_id", tenantInfo.TenantId.ToString());
            if (tenantInfo.TenantName is not null)
            {
                identity.AddClaim("org_name", tenantInfo.TenantName);
            }
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
    /// Rejects scopes outside the client registration, then drops mapped API scopes whose
    /// permissions are absent from the organization roles. Unmapped scopes are retained.
    /// </summary>
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

    /// <summary>
    /// Returns null for enrollment success. First-party refusals and unverified email use
    /// the auth host; other third-party refusals return access_denied to the relying party.
    /// </summary>
    private IActionResult? RefuseEnrollment(EnrollmentOutcome outcome, bool isFirstParty)
    {
        switch (outcome)
        {
            case Enrolled:
                return null;

            case PendingApproval when isFirstParty:
                return Redirect($"{GetRequiredAuthUrl()}/access-request");

            case PendingApproval:
                return AccessDenied(EnrollmentReasons.MembershipPending);

            case Rejected rejected when isFirstParty || rejected.Reason == EnrollmentReasons.EmailUnverified:
                return Redirect($"{GetRequiredAuthUrl()}/error?reason={rejected.Reason}");

            case Rejected rejected:
                return AccessDenied(rejected.Reason);

            default:
                throw new InvalidOperationException(
                    $"Unhandled enrollment outcome '{outcome.GetType().Name}'.");
        }
    }

    private ForbidResult InvalidScope(string description) =>
        ForbidToRelyingParty(Errors.InvalidScope, description);

    private ForbidResult InvalidRequest(string description) =>
        ForbidToRelyingParty(Errors.InvalidRequest, description);

    private ForbidResult AccessDenied(string reason) =>
        ForbidToRelyingParty(Errors.AccessDenied, reason);

    /// <summary>
    /// Delegates an OAuth error response to the OpenIddict server scheme.
    /// </summary>
    private ForbidResult ForbidToRelyingParty(string error, string description) =>
        Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(
                new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorize refused for client {ClientId}: {Reason}")]
    private partial void LogClientRefused(string? clientId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC refused authorize for client {ClientId}: organization hint {HintedOrganizationId} contradicts the bound organization {BoundOrganizationId}")]
    private partial void LogOrganizationHintContradictsBinding(string? clientId, Guid hintedOrganizationId, Guid boundOrganizationId);

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

            // Relying parties receive sid in the ID token for logout correlation.
            ClaimsPrincipalExtensions.SessionIdClaimType => [Destinations.IdentityToken],

            _ => [Destinations.AccessToken]
        };
    }
}
