using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using WallowClaims = Wallow.Shared.Kernel.Extensions.ClaimsPrincipalExtensions;

namespace Wallow.Tests.Common.Helpers;

/// <summary>
/// Stands in for the production "SmartScheme" policy scheme, and selects the same way it does:
/// a bearer credential is honoured here, and everything else falls through to the real ASP.NET
/// Identity cookie. The fall-through is what lets a test drive a browser flow — the authorize
/// endpoint reads the cookie and never a bearer token — and it costs nothing when no cookie is
/// present, because the cookie handler then authenticates nobody.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow tests to opt-out of authentication via header
        if (Request.Headers.TryGetValue("X-Test-Auth-Skip", out StringValues skipHeader) && skipHeader == "true")
        {
            return AuthenticateResult.Fail("Authentication skipped by test");
        }

        // Check for Authorization header or SignalR access_token query param
        bool hasAuthHeader = Request.Headers.ContainsKey("Authorization");
        bool hasAccessToken = Request.Query.ContainsKey("access_token");

        if (!hasAuthHeader && !hasAccessToken)
        {
            return await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        }

        // Extract token from either Authorization header or query param
        string? token = null;
        if (hasAccessToken)
        {
            token = Request.Query["access_token"].ToString();
        }
        else if (hasAuthHeader)
        {
            string authHeader = Request.Headers["Authorization"].ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..];
            }
        }

        // Try to parse user ID and roles from test token (for SignalR tests)
        (string UserId, string[] Roles)? parsedToken = JwtTokenHelper.ParseToken(token);

        // Allow tests to specify custom user ID via header (takes precedence)
        string userId;
        string[] roles;

        if (Request.Headers.TryGetValue("X-Test-User-Id", out StringValues userIdHeader))
        {
            userId = userIdHeader.ToString();
            roles = Request.Headers.TryGetValue("X-Test-Roles", out StringValues rolesHeader)
                ? rolesHeader.ToString().Split(',')
                : new[] { "admin" };
        }
        else if (parsedToken.HasValue)
        {
            // Use user ID and roles from parsed token
            userId = parsedToken.Value.UserId;
            roles = parsedToken.Value.Roles;
        }
        else
        {
            // Default to admin user
            userId = TestConstants.AdminUserId.ToString();
            roles = new[] { "admin" };
        }

        string tenantId = Request.Headers.TryGetValue("X-Test-Tenant-Id", out StringValues tenantHeader)
            ? tenantHeader.ToString()
            : TestConstants.TestOrgId.ToString();

        List<Claim> claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, $"{userId}@test.com"),
        };

        // An organization-less principal: the first-party token a user with several (or no)
        // memberships receives when no organization hint was sent.
        bool withoutOrganization = Request.Headers.TryGetValue("X-Test-No-Organization", out StringValues noOrgHeader)
            && noOrgHeader == "true";
        if (!withoutOrganization)
        {
            claims.Add(new Claim("org_id", tenantId));
        }

        foreach (string role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        // The platform operator's own claim, minted at sign-in for users granted global
        // administration; never derived from roles, which are organization-scoped.
        if (Request.Headers.TryGetValue("X-Test-Global-Admin", out StringValues globalAdminHeader)
            && globalAdminHeader == "true")
        {
            claims.Add(new Claim(WallowClaims.GlobalAdminClaimType, "true"));
        }

        // Space-separated, matching the "scope" claim of a real token, so a test principal can
        // carry granted scopes for PermissionExpansionMiddleware to expand into permissions.
        if (Request.Headers.TryGetValue("X-Test-Scopes", out StringValues scopesHeader))
        {
            claims.Add(new Claim("scope", scopesHeader.ToString()));
        }

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Test");
        ClaimsPrincipal principal = new ClaimsPrincipal(identity);
        AuthenticationTicket ticket = new AuthenticationTicket(principal, "Test");

        return AuthenticateResult.Success(ticket);
    }
}
