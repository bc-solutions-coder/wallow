using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// One person, admin in one organization and a plain member of another. Every path a token can
/// take through the second organization's client — authorize, refresh, and a privileged scope
/// request — must carry only what that membership grants. The assertions read the issued token
/// and run the real permission expansion over it, because an HTTP 200 says nothing about either.
/// </summary>
[Trait("Category", "CrossTenant")]
public sealed class CrossOrgRoleIsolationTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "cross-org-client-secret";

    /// <summary>Maps to UsersDelete, which only "admin" holds.</summary>
    private const string PrivilegedScope = "users.manage";

    private static readonly string[] _clientScopes =
        ["openid", "profile", "email", "roles", "offline_access", PrivilegedScope];

    private static readonly string[] _memberRoles = ["user"];

    [Fact]
    public async Task AcquireTokens_ThroughAClientBoundToTheOtherOrganization_IssuesNoAdminRole()
    {
        Seed seed = await SeedAsync();

        IReadOnlyList<string> rolesWhereAdmin =
            await ResolveRolesAsync(seed.UserId, seed.AdminOrganizationId);
        rolesWhereAdmin.Should().Contain("admin");

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            seed.ClientId, ClientSecret, "openid profile email roles");

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        string accessToken = tokens.RequireAccessToken();
        ReadRoles(accessToken).Should().BeEquivalentTo(_memberRoles);
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "org_id")
            .Should().BeEquivalentTo([seed.MemberOrganizationId.ToString()]);

        IReadOnlyList<string> permissions = await ExpandPermissionsAsync(accessToken);
        permissions.Should().NotContain(PermissionType.AdminAccess);
        permissions.Should().NotContain(PermissionType.UsersDelete);
    }

    [Fact]
    public async Task Refresh_ThroughAClientBoundToTheOtherOrganization_DoesNotReintroduceTheAdminRole()
    {
        Seed seed = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            seed.ClientId, ClientSecret, "openid profile email roles offline_access");

        tokens.RefreshToken.Should().NotBeNullOrEmpty(tokens.Body);

        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);

        string accessToken = refreshed.RequireAccessToken();
        ReadRoles(accessToken).Should().BeEquivalentTo(_memberRoles);

        IReadOnlyList<string> permissions = await ExpandPermissionsAsync(accessToken);
        permissions.Should().NotContain(PermissionType.AdminAccess);
        permissions.Should().NotContain(PermissionType.UsersDelete);
    }

    [Fact]
    public async Task AcquireTokens_RequestingAScopeOnlyTheOtherOrganizationsRoleReaches_NarrowsItAway()
    {
        Seed seed = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            seed.ClientId, ClientSecret, $"openid profile email roles {PrivilegedScope}");

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        string accessToken = tokens.RequireAccessToken();
        ReadScopes(accessToken).Should().NotContain(PrivilegedScope);

        IReadOnlyList<string> permissions = await ExpandPermissionsAsync(accessToken);
        permissions.Should().NotContain(ScopePermissionMapper.MapScopeToPermission(PrivilegedScope));
    }

    /// <summary>
    /// Runs the real middleware over the real token: the permission claim is minted downstream,
    /// so what a role is worth is only visible after expansion, never in the token itself.
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

    private static IReadOnlyList<string> ReadRoles(string accessToken) =>
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "role");

    private static List<string> ReadScopes(string accessToken) =>
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "scope")
            .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    private async Task<IReadOnlyList<string>> ResolveRolesAsync(Guid userId, Guid organizationId)
    {
        IMembershipRoleResolver resolver = ScopedServices.GetRequiredService<IMembershipRoleResolver>();
        return await resolver.GetRoleNamesAsync(userId, organizationId);
    }

    /// <summary>
    /// Creating an organization enrolls its creator as an admin, so the organization the caller is
    /// only a member of has to be owned by someone else.
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"cross-org-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, email, Password);

        Guid adminOrganizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Cross Org Admin {suffix}", userId);

        Guid outsiderId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"cross-org-owner-{suffix}@wallow.dev", Password);

        Guid memberOrganizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Cross Org Member {suffix}", outsiderId);

        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, memberOrganizationId, userId, "user");

        string clientId = $"wallow-cross-org-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, memberOrganizationId, _clientScopes);

        return new Seed(email, clientId, userId, adminOrganizationId, memberOrganizationId);
    }

    private sealed record Seed(
        string Email,
        string ClientId,
        Guid UserId,
        Guid AdminOrganizationId,
        Guid MemberOrganizationId);
}
