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
/// Checks refresh and bearer rejection after deactivation through the admin endpoint.
/// A derived host replaces the shared fake <see cref="IUserManagementService"/> with the real service.
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

        // Use the real service with a synthetic administrator of the target organization.
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

        // Match the HTTPS token issuer and disable synthetic authentication below.
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
