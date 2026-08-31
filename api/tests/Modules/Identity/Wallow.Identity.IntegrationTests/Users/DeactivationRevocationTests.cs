using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Users;

/// <summary>
/// Deactivating a user ends their access now, not at token expiry: every token they hold is
/// revoked, so a refresh answers <c>invalid_grant</c> and the old access token is refused on its
/// next bearer request. The lockout alone only blocks the next interactive sign-in.
/// </summary>
public sealed class DeactivationRevocationTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Deactivate1234!";
    private const string ClientSecret = "deactivation-secret";
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    /// <summary>
    /// Built by hand: the test host registers a no-op fake for
    /// <see cref="Wallow.Identity.Application.Interfaces.IUserManagementService"/>, so resolving
    /// the interface would assert nothing.
    /// </summary>
    private UserManagementService UserManagement =>
        ActivatorUtilities.CreateInstance<UserManagementService>(ScopedServices);

    [Fact]
    public async Task Deactivation_KillsRefreshAndBearerAccess()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"deactivate-{suffix}@wallow.dev";
        string clientId = $"deactivate-console-{suffix}";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Deactivation {suffix}", userId);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, tenantId: null, _clientScopes, firstParty: true);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);
        TokenOutcome tokens = await harness.AcquireTokensAsync(clientId, ClientSecret, Scope);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        await UserManagement.DeactivateUserAsync(userId);

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
}
