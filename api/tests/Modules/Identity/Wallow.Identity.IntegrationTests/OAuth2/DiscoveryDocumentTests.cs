using System.Net;
using System.Text.Json;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Checks advertised logout capabilities and revocation endpoint in the served discovery document.
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

        // Advertise issuer and session-ID support for front-channel notifications.
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

        // Advertise session-ID support in back-channel logout tokens.
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
