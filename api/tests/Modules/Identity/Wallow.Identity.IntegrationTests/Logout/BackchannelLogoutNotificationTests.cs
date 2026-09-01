using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Wallow.Identity.IntegrationTests.OAuth2;
using WireMock.Logging;
using WireMock.ResponseBuilders;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// OIDC back-channel logout against a WireMock relying party: ending the session POSTs a signed
/// logout token to every participating client's registered back-channel URI, the token validates
/// against the server's own JWKS, and a failed delivery gets exactly one retry. The slow-relying-
/// party bound lives in <see cref="BackchannelLogoutSlowRelyingPartyTests"/>, whose tight
/// delivery budgets must not leak into these exact-count assertions.
/// </summary>
[Collection(BackchannelLogoutTestCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BackchannelLogoutNotificationTests(BackchannelLogoutTestFactory factory)
    : BackchannelLogoutDeliveryTestBase(factory)
{
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
}
