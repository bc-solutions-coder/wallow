using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Checks granted token scopes and derived permissions after role-based narrowing,
/// and refusal of a scope not registered for the client.
/// </summary>
public sealed class ScopeNarrowingTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "narrowing-client-secret";

    /// <summary>Maps to StorageRead, which the "user" role holds.</summary>
    private const string ReachableScope = "storage.read";

    /// <summary>
    /// Maps to UsersDelete, outside the baseline user role.
    /// </summary>
    private const string PrivilegedScope = "users.manage";

    private static readonly string[] _clientScopes =
        ["openid", "profile", "email", ReachableScope, PrivilegedScope];

    [Fact]
    public async Task AcquireTokens_RequestingAScopeBeyondTheCallersRoles_IssuesTheRestAnyway()
    {
        (string email, string clientId, Guid organizationId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            clientId,
            ClientSecret,
            $"openid profile email {ReachableScope} {PrivilegedScope}",
            organization: organizationId.ToString());

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        IReadOnlyList<string> issued = ReadScopes(tokens.RequireAccessToken());
        issued.Should().Contain(ReachableScope);
        issued.Should().NotContain(PrivilegedScope);
    }

    [Fact]
    public async Task AcquireTokens_RequestingAScopeBeyondTheCallersRoles_LeavesItsPermissionUnexpandable()
    {
        (string email, string clientId, Guid organizationId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            clientId,
            ClientSecret,
            $"openid profile email {ReachableScope} {PrivilegedScope}",
            organization: organizationId.ToString());

        IReadOnlyList<string> permissions = await ExpandPermissionsAsync(tokens.RequireAccessToken());

        permissions.Should().Contain(ScopePermissionMapper.MapScopeToPermission(ReachableScope));
        permissions.Should().NotContain(ScopePermissionMapper.MapScopeToPermission(PrivilegedScope));
    }

    [Fact]
    public async Task Authorize_RequestingAScopeTheClientIsNotRegisteredFor_RefusesOutright()
    {
        (string email, string clientId, Guid organizationId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(
            clientId, "openid roles.manage", organization: organizationId.ToString());

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
    }

    /// <summary>
    /// Reconstructs a principal from selected issued-token claims and runs permission expansion.
    /// This helper does not validate the JWT signature.
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

    /// <summary>
    /// Uses another owner so the caller has only the baseline membership role.
    /// </summary>
    private async Task<(string Email, string ClientId, Guid OrganizationId)> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"narrowing-{suffix}@wallow.dev";

        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices,
            $"narrowing-owner-{suffix}@wallow.dev",
            Password);

        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices,
            $"Narrowing Org {suffix}",
            ownerId);

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices,
            email,
            Password);

        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices,
            organizationId,
            userId,
            "user");

        string clientId = $"wallow-narrowing-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices,
            clientId,
            ClientSecret,
            tenantId: null,
            _clientScopes,
            firstParty: true);

        return (email, clientId, organizationId);
    }
}
