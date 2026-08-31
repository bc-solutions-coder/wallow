using System.Net;
using System.Text.Json;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Covers the OIDC discovery document. Relying parties decide whether to register a
/// front-channel logout URI by reading these flags, so they are asserted against the real
/// document OpenIddict serves rather than any handler in isolation.
/// </summary>
[Trait("Category", "Integration")]
public class DiscoveryDocumentTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task Discovery_AdvertisesFrontchannelLogoutSupport()
    {
        HttpResponseMessage response = await Client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.TryGetProperty("frontchannel_logout_supported", out JsonElement supported)
            .Should().BeTrue("the discovery document must advertise front-channel logout");
        supported.GetBoolean().Should().BeTrue();

        // Session-supported says every notification carries iss + sid — the pair the SDK's
        // handler validates before it destroys a session.
        document.RootElement.TryGetProperty("frontchannel_logout_session_supported", out JsonElement sessionSupported)
            .Should().BeTrue("the discovery document must advertise sid support");
        sessionSupported.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Discovery_AdvertisesBackchannelLogoutSupport()
    {
        HttpResponseMessage response = await Client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.TryGetProperty("backchannel_logout_supported", out JsonElement supported)
            .Should().BeTrue("the discovery document must advertise back-channel logout");
        supported.GetBoolean().Should().BeTrue();

        // Session-supported says every logout token carries a sid claim — what lets an RP end
        // the one session that logged out rather than every session for the user.
        document.RootElement.TryGetProperty("backchannel_logout_session_supported", out JsonElement sessionSupported)
            .Should().BeTrue("the discovery document must advertise sid support in logout tokens");
        sessionSupported.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Discovery_AdvertisesRevocationEndpoint()
    {
        HttpResponseMessage response = await Client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.TryGetProperty("revocation_endpoint", out JsonElement revocation)
            .Should().BeTrue("clients discover RFC 7009 revocation from this document, not from our docs");
        revocation.GetString().Should().EndWith("/connect/revocation");
    }
}
