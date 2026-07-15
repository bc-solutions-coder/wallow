using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Wallow.Tests.Common.Helpers;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow tests to opt-out of authentication via header
        if (Request.Headers.TryGetValue("X-Test-Auth-Skip", out StringValues skipHeader) && skipHeader == "true")
        {
            return Task.FromResult(AuthenticateResult.Fail("Authentication skipped by test"));
        }

        // Check for Authorization header or SignalR access_token query param
        bool hasAuthHeader = Request.Headers.ContainsKey("Authorization");
        bool hasAccessToken = Request.Query.ContainsKey("access_token");

        // If no auth credentials provided, fail authentication
        if (!hasAuthHeader && !hasAccessToken)
        {
            return Task.FromResult(AuthenticateResult.Fail("No authorization token provided"));
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
            new("org_id", tenantId),
        };

        foreach (string role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Test");
        ClaimsPrincipal principal = new ClaimsPrincipal(identity);
        AuthenticationTicket ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
