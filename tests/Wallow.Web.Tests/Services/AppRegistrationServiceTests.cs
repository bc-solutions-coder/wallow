using System.Net;
using Microsoft.AspNetCore.Http;
using RichardSzalay.MockHttp;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Tests.Services;

public sealed class AppRegistrationServiceTests : IDisposable
{
    private const string BaseUrl = "http://localhost:5000";
    private const string BasePath = "api/v1/identity/apps";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly AppRegistrationService _sut;

    public AppRegistrationServiceTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(BaseUrl);

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("WallowApi").Returns(httpClient);

        TokenProvider tokenProvider = new(Substitute.For<IHttpContextAccessor>()) { AccessToken = "test-token" };

        _sut = new AppRegistrationService(factory, tokenProvider);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }

    // --- GetAppsAsync ---

    [Fact]
    public async Task GetAppsAsync_SuccessfulResponse_ReturnsAppList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{BasePath}")
            .Respond("application/json", """
                [{"clientId":"app1","displayName":"App One","clientType":"public","redirectUris":["http://localhost"],"createdAt":"2026-01-01T00:00:00+00:00"}]
                """);

        List<AppModel> result = await _sut.GetAppsAsync();

        result.Should().HaveCount(1);
        result[0].ClientId.Should().Be("app1");
        result[0].DisplayName.Should().Be("App One");
    }

    [Fact]
    public async Task GetAppsAsync_EmptyResponse_ReturnsEmptyList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{BasePath}")
            .Respond("application/json", "[]");

        List<AppModel> result = await _sut.GetAppsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAppsAsync_ErrorResponse_ReturnsEmptyList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{BasePath}")
            .Respond(HttpStatusCode.InternalServerError);

        List<AppModel> result = await _sut.GetAppsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAppsAsync_NullDeserialization_ReturnsEmptyList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{BasePath}")
            .Respond("application/json", "null");

        List<AppModel> result = await _sut.GetAppsAsync();

        result.Should().BeEmpty();
    }

    // --- GetAppAsync ---

    [Fact]
    public async Task GetAppAsync_ExistingApp_ReturnsApp()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{BasePath}/app1")
            .Respond("application/json", """
                {"clientId":"app1","displayName":"App One","clientType":"public","redirectUris":[],"createdAt":null}
                """);

        AppModel? result = await _sut.GetAppAsync("app1");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("app1");
        result.DisplayName.Should().Be("App One");
    }

    [Fact]
    public async Task GetAppAsync_NotFound_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{BasePath}/nonexistent")
            .Respond(HttpStatusCode.NotFound);

        AppModel? result = await _sut.GetAppAsync("nonexistent");

        result.Should().BeNull();
    }

    // --- RegisterAppAsync ---

    [Fact]
    public async Task RegisterAppAsync_SuccessfulResponse_ReturnsSuccessResult()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}/register")
            .Respond("application/json", """
                {"clientId":"new-app","clientSecret":"secret123","registrationAccessToken":"rat-token"}
                """);

        RegisterAppModel model = new("My App", "confidential", ["http://localhost/callback"], ["openid", "profile"]);

        RegisterAppResult result = await _sut.RegisterAppAsync(model);

        result.Success.Should().BeTrue();
        result.ClientId.Should().Be("new-app");
        result.ClientSecret.Should().Be("secret123");
        result.RegistrationAccessToken.Should().Be("rat-token");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAppAsync_NullDeserialization_ReturnsFailureResult()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}/register")
            .Respond("application/json", "null");

        RegisterAppModel model = new("My App", "confidential", ["http://localhost/callback"], ["openid"]);

        RegisterAppResult result = await _sut.RegisterAppAsync(model);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failed to deserialize response");
    }

    [Fact]
    public async Task RegisterAppAsync_ErrorResponse_ReturnsFailureWithErrorBody()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}/register")
            .Respond(HttpStatusCode.BadRequest, "application/json", """{"error":"invalid_request"}""");

        RegisterAppModel model = new("My App", "confidential", [], []);

        RegisterAppResult result = await _sut.RegisterAppAsync(model);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("invalid_request");
    }

    // --- UpsertBrandingAsync ---

    [Fact]
    public async Task UpsertBrandingAsync_SuccessfulResponse_ReturnsTrue()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}/app1/branding")
            .Respond(HttpStatusCode.OK);

        bool result = await _sut.UpsertBrandingAsync("app1", "My App", "A tagline", null, null, null, null);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertBrandingAsync_WithLogo_ReturnsTrue()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}/app1/branding")
            .Respond(HttpStatusCode.OK);

        using MemoryStream logoStream = new([0x89, 0x50, 0x4E, 0x47]);

        bool result = await _sut.UpsertBrandingAsync(
            "app1", "My App", null, """{"primary":"#000"}""",
            logoStream, "logo.png", "image/png");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertBrandingAsync_ErrorResponse_ReturnsFalse()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}/app1/branding")
            .Respond(HttpStatusCode.InternalServerError);

        bool result = await _sut.UpsertBrandingAsync("app1", "My App", null, null, null, null, null);

        result.Should().BeFalse();
    }

}
