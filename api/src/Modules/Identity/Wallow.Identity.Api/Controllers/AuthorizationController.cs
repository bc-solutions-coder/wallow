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
    ILogger<AuthorizationController> logger) : Controller
{
    /// <summary>
    /// The authorize parameter naming the organization a first-party login should run under
    /// (an organization identifier). A bound client may only restate its own organization.
    /// </summary>
    public const string OrganizationParameter = "organization";

    [HttpGet]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        LogAuthorizeRequest(request.ClientId, request.RedirectUri, request.ResponseType, request.Scope);

        // A client the platform will not serve — suspended by its organization or the platform,
        // or bound to an organization that is archived or platform-suspended — is told so before
        // anyone is asked to sign in, and told on the auth host rather than at its own redirect
        // URI: a client out of service gets no traffic back.
        ClientAccessRefusal? accessRefusal = await clientAccessPolicy.EvaluateAsync(
            request.ClientId, HttpContext.RequestAborted);
        if (accessRefusal is not null)
        {
            LogClientRefused(request.ClientId, accessRefusal.Reason);
            return Redirect($"{GetRequiredAuthUrl()}/error?reason={accessRefusal.Reason}");
        }

        if (User.Identity is not { IsAuthenticated: true })
        {
            string authUrl = GetRequiredAuthUrl();

            // Rebuilt from the OpenIddict request, not the URL: a consent decision that arrives
            // after the identity cookie lapsed is a POST carrying the request in its body, and
            // the decision itself must not ride along to the login that replays it.
            string returnUrl = Request.PathBase + Request.Path + QueryString.Create(AuthorizeParameters(request));

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

        // First-party is whatever the seed registered as such, carried on the application as
        // OpenIddict's consent type. Nothing about the client id decides it: a lookalike id
        // registered through the organization surface is explicit-consent like any other.
        bool isFirstParty = string.Equals(
            await applicationManager.GetConsentTypeAsync(application),
            ConsentTypes.Implicit,
            StringComparison.Ordinal);

        LogApplicationResolved(clientId, isFirstParty);

        // The organization has to be settled before anything else, because everything after it
        // is org-scoped: which roles the caller holds, which scopes those roles reach, and
        // whether they may sign in here at all. It also means a non-member is told so before
        // being walked through a consent screen for an app they cannot use.
        ClientTenantInfo? tenantInfo = request.ClientId is null
            ? null
            : await clientTenantResolver.ResolveAsync(request.ClientId);

        if (tenantInfo is not null && tenantInfo.TenantId == Guid.Empty)
        {
            tenantInfo = null;
        }

        // A third-party client is bound to exactly one organization by registration, so one
        // bound to none is a registration defect: its token would carry scopes with nowhere to
        // spend them, and a principal naming no organization is exactly what
        // PermissionExpansionMiddleware treats as cross-tenant. A first-party client is bound
        // to no organization by design; its login is legal with no organization context at all.
        if (tenantInfo is null && !isFirstParty)
        {
            LogClientHasNoOrganization(clientId);
            return Redirect($"{GetRequiredAuthUrl()}/error?reason=client_not_bound_to_organization");
        }

        // One code path for organization context: a bound client is "hint fixed by registration",
        // and a first-party client supplies the hint itself. Either way the transaction runs the
        // named organization's enrollment policy below. A bound client restating its own
        // organization is not a contradiction; naming any other one is a malformed request.
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
                // The name is for the org_name claim only; whether the user may sign in here is
                // the enrollment policy's answer, and it treats an unknown organization as
                // refusing a stranger.
                OrganizationDto? hintedOrganization = await organizations.GetOrganizationByIdAsync(hintedOrganizationId);
                tenantInfo = new ClientTenantInfo(hintedOrganizationId, hintedOrganization?.Name);
            }
        }

        // Without a hint, a first-party client's user decides by their own memberships: exactly
        // one active membership is unambiguous and becomes the session's organization; none or
        // several leave the token org-less rather than guessing.
        if (tenantInfo is null)
        {
            IReadOnlyList<MyOrganizationDto> memberships =
                await organizations.GetMyOrganizationsAsync(Guid.Parse(userId));
            if (memberships.Count == 1)
            {
                tenantInfo = new ClientTenantInfo(memberships[0].OrganizationId, memberships[0].Name);
            }
        }

        // A membership row is not permission to sign in here; only an Active one is. Whether one
        // can be minted on the spot is the organization's enrollment policy to answer, and that
        // answer — along with the email-verification precondition and the three refusal reasons —
        // lives in the enrollment service, because AccountController has to reach the same one.
        // Global admin governs across organizations, so it is not gated by a membership at all:
        // it is the one authority an organization does not grant.
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

        // Roles are granted by an organization and carry no authority outside it, so this is the
        // only role set that may decide anything here: the scopes granted below and the role
        // claims stamped into the token both read it. No organization, no roles.
        IReadOnlyList<string> roles = tenantInfo is null
            ? []
            : await membershipRoleResolver.GetRoleNamesAsync(Guid.Parse(userId), tenantInfo.TenantId);

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

        string applicationId = (await applicationManager.GetIdAsync(application))!;

        // The authorization every token of this sign-in chains to. OpenIddict would mint an
        // anonymous ad-hoc one itself; Wallow records its own so the organization the sign-in
        // ran in is written on it, which is the only place revocation can find a first-party
        // token's organization (a bound client's tokens name it through the client as well).
        string? authorizationId = null;

        if (!isFirstParty)
        {
            // Stored consent is the user's Valid PERMANENT authorizations for this client — the
            // ad-hoc records first-party sign-ins mint are per-login bookkeeping and never count.
            // The union of their scopes is what the user has already agreed to; the newest record
            // is the one widened on scope growth, so consent accumulates on one row.
            (object? permanentAuthorization, HashSet<string> consentedScopes) =
                await FindPermanentConsentAsync(userId, applicationId);

            // The delta the user has not yet agreed to — the only thing a consent screen may ask.
            ImmutableArray<string> missingScopes =
                [.. grantedScopes.Where(s => !consentedScopes.Contains(s))];
            ImmutableArray<string> consentScreenScopes =
                missingScopes.IsEmpty ? grantedScopes : missingScopes;

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

                // Granted. A redeemed decision settles the transaction even when the replayed
                // request still carries prompt=consent — honouring the prompt here would bounce
                // the answer straight back to the screen forever.
                authorizationId = await StoreConsentAsync(
                    permanentAuthorization, applicationId, userId, grantedScopes, tenantInfo);
            }
            else if (request.HasPromptValue(PromptValues.None))
            {
                // The relying party forbade UI. Missing consent is a protocol error it handles,
                // never a screen.
                if (!missingScopes.IsEmpty)
                {
                    return ConsentRequiredForbid(
                        "The request needs consent the user has not given, and 'prompt=none' forbids asking for it.");
                }

                authorizationId = await authorizationManager.GetIdAsync(permanentAuthorization!);
            }
            else if (!missingScopes.IsEmpty || request.HasPromptValue(PromptValues.Consent))
            {
                return RedirectToConsent(request, userId, clientId, consentScreenScopes, fingerprint);
            }
            else
            {
                authorizationId = await authorizationManager.GetIdAsync(permanentAuthorization!);
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

        if (isFirstParty && tenantInfo is not null)
        {
            authorizationId = await CreateAuthorizationAsync(
                AuthorizationTypes.AdHoc, applicationId, userId, grantedScopes, tenantInfo);
        }

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
    /// The user's stored consent for one application: the newest Valid permanent authorization
    /// (the record scope growth widens) and the union of scopes across all of them (what the
    /// user has already agreed to, even if legacy rows split it).
    /// </summary>
    private async Task<(object? Newest, HashSet<string> ConsentedScopes)> FindPermanentConsentAsync(
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
    /// Records a granted consent: widens the one permanent authorization's scope set to cover
    /// the granted scopes (creating the record on first consent) and returns its id — the id
    /// every token of this and later sign-ins chains to.
    /// </summary>
    private async Task<string?> StoreConsentAsync(
        object? permanentAuthorization,
        string applicationId,
        string userId,
        ImmutableArray<string> grantedScopes,
        ClientTenantInfo? tenantInfo)
    {
        if (permanentAuthorization is null)
        {
            return await CreateAuthorizationAsync(
                AuthorizationTypes.Permanent, applicationId, userId, grantedScopes, tenantInfo);
        }

        OpenIddictAuthorizationDescriptor descriptor = new();
        await authorizationManager.PopulateAsync(descriptor, permanentAuthorization);
        int scopeCountBefore = descriptor.Scopes.Count;
        descriptor.Scopes.UnionWith(grantedScopes);
        if (descriptor.Scopes.Count != scopeCountBefore)
        {
            await authorizationManager.UpdateAsync(permanentAuthorization, descriptor);
        }

        return await authorizationManager.GetIdAsync(permanentAuthorization);
    }

    /// <summary>Refuses the relying party with <c>consent_required</c>.</summary>
    private ForbidResult ConsentRequiredForbid(string description) =>
        Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(
                new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
                }));

    private async Task<string?> CreateAuthorizationAsync(
        string type,
        string applicationId,
        string userId,
        ImmutableArray<string> grantedScopes,
        ClientTenantInfo? tenantInfo)
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

        object authorization = await authorizationManager.CreateAsync(descriptor);
        return await authorizationManager.GetIdAsync(authorization);
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

    /// <summary>
    /// Turns an enrollment outcome into the answer the user gets, or null when they are enrolled.
    /// Where the answer lands depends on whose problem it is. A first-party client is the
    /// platform's own UI, so its user stays on the auth host: the request-submitted screen for a
    /// pending request, the error page for a refusal. A third-party user is the relying party's
    /// to handle, so an organization's answer — pending, suspended, denied, not a member — goes
    /// back to its redirect URI as <c>access_denied</c> with the reason as the description. The
    /// one precondition that is not an organization's answer, an unverified email, keeps the
    /// auth host's error page for every client: verifying it is the auth host's job.
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
    /// Refuses the transaction the OAuth way: OpenIddict delivers the error to the relying
    /// party's redirect URI, so the client — not the auth host — decides what to show.
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

            // sid exists solely so the RP can match a front-channel logout notification to
            // the session it belongs to — an id_token concern with no access-token consumer.
            ClaimsPrincipalExtensions.SessionIdClaimType => [Destinations.IdentityToken],

            _ => [Destinations.AccessToken]
        };
    }
}
