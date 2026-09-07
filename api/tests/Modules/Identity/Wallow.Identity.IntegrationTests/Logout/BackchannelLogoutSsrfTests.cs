using System.Net;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Factories;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// Checks that default back-channel policy refuses loopback targets without a server error.
/// </summary>
public sealed class BackchannelLogoutSsrfTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "SsrfGate1234!";
    private const string ClientSecret = "ssrf-gate-secret";
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task Logout_RefusesToNotifyAPrivateNetworkBackchannelTarget()
    {
        using WireMockServer internalService = WireMockServer.Start();
        internalService
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200));

        string suffix = Guid.NewGuid().ToString("N");
        string email = $"ssrf-gate-{suffix}@wallow.dev";
        string clientId = $"ssrf-gate-app-{suffix}";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Ssrf Gate {suffix}", userId);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices,
            clientId,
            ClientSecret,
            organizationId,
            _clientScopes,
            backchannelLogoutUri: internalService.Url + "/backchannel-logout");

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);
        AuthorizeOutcome authorize = await harness.AuthorizeAsync(clientId, Scope);
        if (authorize.Code is null)
        {
            authorize = await harness.ConsentAsync(authorize, grant: true);
        }

        authorize.Code.Should().NotBeNull(authorize.Location?.ToString() ?? authorize.Body);
        TokenOutcome tokens = await harness.ExchangeCodeAsync(
            clientId, ClientSecret, authorize.Code!, authorize.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        using HttpResponseMessage logout = await harness.Client.GetAsync(
            new Uri("/connect/logout", UriKind.Relative));

        // Refusing the target must avoid a server error and send no request to the loopback service.
        ((int)logout.StatusCode).Should().BeLessThan(500, await logout.Content.ReadAsStringAsync());
        internalService.LogEntries.Should().BeEmpty();
    }
}
