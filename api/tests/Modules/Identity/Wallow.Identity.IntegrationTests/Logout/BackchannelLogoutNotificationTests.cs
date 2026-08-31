using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Bases;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// OIDC back-channel logout against a WireMock relying party: ending the session POSTs a signed
/// logout token to every participating client's registered back-channel URI, the token validates
/// against the server's own JWKS, a failed delivery gets exactly one retry, and a slow relying
/// party cannot hold the user's sign-out hostage.
/// </summary>
[Collection(BackchannelLogoutTestCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BackchannelLogoutNotificationTests(BackchannelLogoutTestFactory factory)
    : WallowIntegrationTestBase(factory)
{
    private const string Password = "Backchannel1234!";
    private const string ClientSecret = "backchannel-secret";
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    private BackchannelLogoutTestFactory LogoutFactory => (BackchannelLogoutTestFactory)Factory;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task Logout_PostsAJwksVerifiableLogoutTokenToTheRegisteredBackchannelUri()
    {
        Seed seed = await SeedAsync(rpBehaviour: rp => rp.RespondWith(Response.Create().WithStatusCode(200)));
        using AuthorizationCodeFlowHarness harness = await SignedInWithTokensAsync(seed);

        await LogoutAsync(harness);

        ILogEntry entry = RpRequests(seed).Should().ContainSingle().Subject;
        entry.RequestMessage!.Method.Should().Be("POST");

        string body = entry.RequestMessage.Body ?? string.Empty;
        body.Should().StartWith("logout_token=");
        string token = Uri.UnescapeDataString(body["logout_token=".Length..]);

        (string issuer, ICollection<SecurityKey> keys) = await FetchJwksAsync(harness);
        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = seed.ClientId,
                IssuerSigningKeys = keys,
                ValidTypes = ["logout+jwt"],
            });
        result.IsValid.Should().BeTrue(because: result.Exception?.Message ?? "the token should validate against JWKS");

        JsonWebToken jwt = new(token);
        jwt.Typ.Should().Be("logout+jwt");
        jwt.Subject.Should().Be(seed.UserId.ToString());
        (jwt.ValidTo - jwt.IssuedAt).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(2));

        using JsonDocument payload = JsonDocument.Parse(Base64UrlEncoder.Decode(jwt.EncodedPayload));
        payload.RootElement.GetProperty("sid").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("jti").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("events")
            .TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out JsonElement _)
            .Should().BeTrue();
        payload.RootElement.TryGetProperty("nonce", out JsonElement _).Should().BeFalse();
    }

    [Fact]
    public async Task Logout_RetriesExactlyOnceWhenTheRelyingPartyKeepsFailing()
    {
        Seed seed = await SeedAsync(rpBehaviour: rp => rp.RespondWith(Response.Create().WithStatusCode(500)));
        using AuthorizationCodeFlowHarness harness = await SignedInWithTokensAsync(seed);

        await LogoutAsync(harness);

        RpRequests(seed).Should().HaveCount(2);
    }

    [Fact]
    public async Task Logout_StaysBoundedWhenTheRelyingPartyIsSlow()
    {
        // The RP answers far outside the 1s per-attempt budget; two timed-out attempts plus the
        // retry pause is the worst case, well under the 5s overall bound.
        Seed seed = await SeedAsync(rpBehaviour: rp => rp.RespondWith(
            Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromSeconds(20))));
        using AuthorizationCodeFlowHarness harness = await SignedInWithTokensAsync(seed);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await LogoutAsync(harness);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Logout_StillRendersTheFrontchannelPageAfterBackchannelDelivery()
    {
        string frontchannelUri = "https://rp.example.com/bff/frontchannel-logout";
        Seed seed = await SeedAsync(
            rpBehaviour: rp => rp.RespondWith(Response.Create().WithStatusCode(200)),
            frontchannelLogoutUri: frontchannelUri);
        using AuthorizationCodeFlowHarness harness = await SignedInWithTokensAsync(seed);

        using HttpResponseMessage logout = await harness.Client.GetAsync(
            new Uri("/connect/logout", UriKind.Relative));
        string page = await logout.Content.ReadAsStringAsync();

        // The back channel is additive: the server-side POST landed, and the browser still gets
        // the front-channel iframe page.
        RpRequests(seed).Should().ContainSingle();
        page.Should().Contain("<iframe").And.Contain(frontchannelUri);
    }

    /// <summary>The WireMock requests aimed at this seed's unique relying-party path.</summary>
    private IReadOnlyList<ILogEntry> RpRequests(Seed seed) =>
        [.. LogoutFactory.WireMock.LogEntries.Where(e => e.RequestMessage?.Path == seed.RpPath)];

    private static async Task LogoutAsync(AuthorizationCodeFlowHarness harness)
    {
        using HttpResponseMessage logout = await harness.Client.GetAsync(
            new Uri("/connect/logout", UriKind.Relative));
        ((int)logout.StatusCode).Should().BeLessThan(500, await logout.Content.ReadAsStringAsync());
    }

    /// <summary>Reads the issuer and signing keys the discovery document actually advertises.</summary>
    private static async Task<(string Issuer, ICollection<SecurityKey> Keys)> FetchJwksAsync(
        AuthorizationCodeFlowHarness harness)
    {
        string discoveryJson = await harness.Client.GetStringAsync(
            new Uri("/.well-known/openid-configuration", UriKind.Relative));
        using JsonDocument discovery = JsonDocument.Parse(discoveryJson);
        string issuer = discovery.RootElement.GetProperty("issuer").GetString()!;
        Uri jwksUri = new(discovery.RootElement.GetProperty("jwks_uri").GetString()!);

        string jwksJson = await harness.Client.GetStringAsync(
            new Uri(jwksUri.PathAndQuery, UriKind.Relative));
        return (issuer, new JsonWebKeySet(jwksJson).GetSigningKeys());
    }

    private async Task<AuthorizationCodeFlowHarness> SignedInWithTokensAsync(Seed seed)
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

    private async Task<Seed> SeedAsync(
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

    private sealed record Seed(string Email, string ClientId, Guid UserId, string RpPath);
}
