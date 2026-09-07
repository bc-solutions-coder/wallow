using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using OpenIddict.Validation.AspNetCore;
using WallowClaims = Wallow.Shared.Kernel.Extensions.ClaimsPrincipalExtensions;

namespace Wallow.Tests.Common.Helpers;

/// <summary>
/// Uses synthetic test credentials when an authorization header or access-token query is present.
/// Otherwise delegates to the Identity cookie; X-Test-Auth-Skip selects real bearer validation.
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
        // Let tests opt into the configured OpenIddict validation handler.
        if (Request.Headers.TryGetValue("X-Test-Auth-Skip", out StringValues skipHeader) && skipHeader == "true")
        {
            return await Context.AuthenticateAsync(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }


        bool hasAuthHeader = Request.Headers.ContainsKey("Authorization");
        bool hasAccessToken = Request.Query.ContainsKey("access_token");

        if (!hasAuthHeader && !hasAccessToken)
        {
            return await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        }


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


        (string UserId, string[] Roles)? parsedToken = JwtTokenHelper.ParseToken(token);

        // Explicit test headers take precedence over the encoded identity.
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

            userId = parsedToken.Value.UserId;
            roles = parsedToken.Value.Roles;
        }
        else
        {

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

        // Model a principal without an organization claim.
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

        // Global administration is a separate claim from organization roles.
        if (Request.Headers.TryGetValue("X-Test-Global-Admin", out StringValues globalAdminHeader)
            && globalAdminHeader == "true")
        {
            claims.Add(new Claim(WallowClaims.GlobalAdminClaimType, "true"));
        }

        // Permission expansion reads space-separated scopes from this claim.
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
