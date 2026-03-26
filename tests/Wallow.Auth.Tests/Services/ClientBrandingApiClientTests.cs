using System.Net;
using RichardSzalay.MockHttp;
using Wallow.Auth.Services;

namespace Wallow.Auth.Tests.Services;

public sealed class ClientBrandingApiClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly ClientBrandingApiClient _sut;

    public ClientBrandingApiClientTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AuthApi").Returns(httpClient);

        _sut = new ClientBrandingApiClient(factory);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task GetBrandingAsync_ValidClientId_ReturnsBrandingResponse()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/test-client/branding")
            .Respond("application/json",
                """{"clientId":"test-client","displayName":"Test App","tagline":"A tagline","logoUrl":"https://example.com/logo.png","themeJson":"{\"primary\":\"#000\"}"}""");

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("test-client");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("test-client");
        result.DisplayName.Should().Be("Test App");
        result.Tagline.Should().Be("A tagline");
        result.LogoUrl.Should().Be("https://example.com/logo.png");
        result.ThemeJson.Should().Be("{\"primary\":\"#000\"}");
    }

    [Fact]
    public async Task GetBrandingAsync_NotFound_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/unknown-client/branding")
            .Respond(HttpStatusCode.NotFound);

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("unknown-client");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBrandingAsync_ServerError_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/error-client/branding")
            .Respond(HttpStatusCode.InternalServerError);

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("error-client");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBrandingAsync_NullOptionalFields_ReturnsResponseWithNulls()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/minimal-client/branding")
            .Respond("application/json",
                """{"clientId":"minimal-client","displayName":"Minimal","tagline":null,"logoUrl":null,"themeJson":null}""");

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("minimal-client");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("minimal-client");
        result.DisplayName.Should().Be("Minimal");
        result.Tagline.Should().BeNull();
        result.LogoUrl.Should().BeNull();
        result.ThemeJson.Should().BeNull();
    }

    [Fact]
    public async Task GetBrandingAsync_ClientIdWithSpecialCharacters_EscapesInUrl()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/client%20id%2Fslash/branding")
            .Respond("application/json",
                """{"clientId":"client id/slash","displayName":"Special","tagline":null,"logoUrl":null,"themeJson":null}""");

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("client id/slash");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("client id/slash");
    }
}
