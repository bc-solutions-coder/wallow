using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Users;

/// <summary>
/// Deactivating a user through the admin API ends their access now, not at token expiry: every
/// token they hold is revoked, so a refresh answers <c>invalid_grant</c> and the old access
/// token is refused on its next bearer request. The lockout alone only blocks the next
/// interactive sign-in. The shared test host registers a no-op fake for
/// <see cref="IUserManagementService"/>, so the deactivation call runs on a derived host that
/// restores the real service — same database, deployed controller surface.
/// </summary>
public sealed class DeactivationRevocationTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Deactivate1234!";
    private const string ClientSecret = "deactivation-secret";
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task Deactivation_KillsRefreshAndBearerAccess()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"deactivate-{suffix}@wallow.dev";
        string clientId = $"deactivate-console-{suffix}";
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"deactivate-owner-{suffix}@wallow.dev", Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Deactivation {suffix}", ownerId);
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, organizationId, userId, "user");
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, tenantId: null, _clientScopes, firstParty: true);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);
        TokenOutcome tokens = await harness.AcquireTokensAsync(clientId, ClientSecret, Scope);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        // Deactivate through the deployed admin endpoint, on a host where the real service backs
        // the interface, acting as an org admin of the target's organization.
        using WebApplicationFactory<Program> host = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserManagementService>();
                services.AddScoped<IUserManagementService, UserManagementService>();
            }));
        using HttpClient admin = ActingClient(host, ownerId, "admin", organizationId);
        using HttpResponseMessage deactivated = await admin.PostAsync(
            new Uri($"/v1/identity/users/{userId}/deactivate", UriKind.Relative), content: null);
        deactivated.StatusCode.Should().Be(
            HttpStatusCode.NoContent, await deactivated.Content.ReadAsStringAsync());

        TokenOutcome refreshed = await harness.RefreshAsync(clientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");

        // Over https, because the real validation handler refuses plain-http requests outright.
        HttpClient bearer = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        bearer.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.RequireAccessToken()}");
        bearer.DefaultRequestHeaders.Add("X-Test-Auth-Skip", "true");
        HttpResponseMessage bearerCall = await bearer.GetAsync(
            new Uri("/identity/me/organizations", UriKind.Relative));
        bearerCall.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static HttpClient ActingClient(
        WebApplicationFactory<Program> host, Guid userId, string roles, Guid tenantId)
    {
        HttpClient client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", tenantId.ToString("D"));
        return client;
    }
}
