using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Bases;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// Shared plumbing for back-channel logout delivery tests: seeds a user, organization, and
/// relying-party client whose back-channel URI points at the factory's WireMock server, walks
/// the authorization-code flow to a signed-in session with tokens, and triggers logout.
/// </summary>
public abstract class BackchannelLogoutDeliveryTestBase(BackchannelLogoutTestFactory factory)
    : WallowIntegrationTestBase(factory)
{
    protected const string Password = "Backchannel1234!";
    protected const string ClientSecret = "backchannel-secret";
    protected const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    protected BackchannelLogoutTestFactory LogoutFactory { get; } = factory;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    /// <summary>The WireMock requests aimed at this seed's unique relying-party path.</summary>
    protected IReadOnlyList<ILogEntry> RpRequests(Seed seed) =>
        [.. LogoutFactory.WireMock.LogEntries.Where(e => e.RequestMessage?.Path == seed.RpPath)];

    protected static async Task LogoutAsync(AuthorizationCodeFlowHarness harness)
    {
        using HttpResponseMessage logout = await harness.Client.GetAsync(
            new Uri("/connect/logout", UriKind.Relative));
        ((int)logout.StatusCode).Should().BeLessThan(500, await logout.Content.ReadAsStringAsync());
    }

    protected async Task<AuthorizationCodeFlowHarness> SignedInWithTokensAsync(Seed seed)
    {
        AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(seed.ClientId, Scope);
        if (authorize.Code is null)
        {
            authorize = await harness.ConsentAsync(authorize, grant: true);
        }

        authorize.Code.Should().NotBeNull(authorize.Location?.ToString() ?? authorize.Body);
        TokenOutcome tokens = await harness.ExchangeCodeAsync(
            seed.ClientId, ClientSecret, authorize.Code!, authorize.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        return harness;
    }

    protected async Task<Seed> SeedAsync(
        Action<IRespondWithAProvider> rpBehaviour,
        string? frontchannelLogoutUri = null)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"backchannel-{suffix}@wallow.dev";
        string clientId = $"backchannel-app-{suffix}";
        string rpPath = $"/backchannel-logout/{suffix}";

        rpBehaviour(LogoutFactory.WireMock.Given(
            Request.Create().WithPath(rpPath).UsingPost()));

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Backchannel {suffix}", userId);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices,
            clientId,
            ClientSecret,
            organizationId,
            _clientScopes,
            frontchannelLogoutUri: frontchannelLogoutUri,
            backchannelLogoutUri: LogoutFactory.WireMock.Url + rpPath);

        return new Seed(email, clientId, userId, rpPath);
    }

    protected sealed record Seed(string Email, string ClientId, Guid UserId, string RpPath);
}
