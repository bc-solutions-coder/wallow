using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Covers what the authorize endpoint does with a scope the caller's roles do not reach. It
/// narrows the grant rather than refusing the request, so the assertion is on the issued token's
/// scope claim and on what PermissionExpansionMiddleware can still make of it — an HTTP 200 says
/// nothing, because refusing and narrowing both leave the rest of the flow working.
/// </summary>
public sealed class ScopeNarrowingTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "narrowing-client-secret";

    /// <summary>Maps to StorageRead, which the "user" role holds.</summary>
    private const string ReachableScope = "storage.read";

    /// <summary>Maps to UsersDelete, which only "admin" holds.</summary>
    private const string PrivilegedScope = "users.manage";

    private static readonly string[] _clientScopes =
        ["openid", "profile", "email", ReachableScope, PrivilegedScope];

    [Fact]
    public async Task AcquireTokens_RequestingAScopeBeyondTheCallersRoles_IssuesTheRestAnyway()
    {
        (string email, string clientId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            clientId,
            ClientSecret,
            $"openid profile email {ReachableScope} {PrivilegedScope}");

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        IReadOnlyList<string> issued = ReadScopes(tokens.RequireAccessToken());
        issued.Should().Contain(ReachableScope);
        issued.Should().NotContain(PrivilegedScope);
    }

    [Fact]
    public async Task AcquireTokens_RequestingAScopeBeyondTheCallersRoles_LeavesItsPermissionUnexpandable()
    {
        (string email, string clientId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            clientId,
            ClientSecret,
            $"openid profile email {ReachableScope} {PrivilegedScope}");

        IReadOnlyList<string> permissions = await ExpandPermissionsAsync(tokens.RequireAccessToken());

        permissions.Should().Contain(ScopePermissionMapper.MapScopeToPermission(ReachableScope));
        permissions.Should().NotContain(ScopePermissionMapper.MapScopeToPermission(PrivilegedScope));
    }

    [Fact]
    public async Task Authorize_RequestingAScopeTheClientIsNotRegisteredFor_RefusesOutright()
    {
        (string email, string clientId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(clientId, "openid roles.manage");

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
    }

    /// <summary>
    /// Runs the real middleware over the real token, which is the only way to say the refused
    /// scope grants nothing: the permission claim is minted downstream, never carried in the token.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ExpandPermissionsAsync(string accessToken)
    {
        List<Claim> claims = [];
        foreach (string claimType in new[] { "scope", "org_id", "role", ClaimTypes.Role })
        {
            foreach (string value in AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, claimType))
            {
                claims.Add(new Claim(claimType, value));
            }
        }

        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        return context.User.GetPermissions();
    }

    private static List<string> ReadScopes(string accessToken) =>
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "scope")
            .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    private async Task<(string Email, string ClientId)> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"narrowing-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices,
            email,
            Password,
            ["user"]);

        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices,
            $"Narrowing Org {suffix}",
            userId);

        string clientId = $"wallow-narrowing-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices,
            clientId,
            ClientSecret,
            organizationId,
            _clientScopes);

        return (email, clientId);
    }
}
