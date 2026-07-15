using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AppsControllerTests
{
    private readonly IDeveloperAppService _developerAppService;
    private readonly AppsController _controller;

    public AppsControllerTests()
    {
        _developerAppService = Substitute.For<IDeveloperAppService>();
        _controller = new AppsController(_developerAppService);

        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    #region Register

    [Fact]
    public async Task Register_WithoutAppPrefix_ReturnsValidationProblem()
    {
        RegisterAppRequest request = new("my-client", ["inquiries.read"]);

        ActionResult<AppRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task Register_WithInvalidScopes_ReturnsValidationProblem()
    {
        RegisterAppRequest request = new("app-test", ["invalid.scope"]);

        ActionResult<AppRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task Register_WithValidRequest_Returns201Created()
    {
        RegisterAppRequest request = new("app-test", ["inquiries.read"]);
        DeveloperAppRegistrationResult registrationResult = new("client-id", "client-secret", "reg-token");
        _developerAppService.RegisterClientAsync(
                "app-test", "app-test", Arg.Any<IReadOnlyCollection<string>>(),
                null, null, "user-123", Arg.Any<CancellationToken>())
            .Returns(registrationResult);

        ActionResult<AppRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        AppRegistrationResponse response = objectResult.Value.Should().BeOfType<AppRegistrationResponse>().Subject;
        response.ClientId.Should().Be("client-id");
        response.ClientSecret.Should().Be("client-secret");
        response.RegistrationAccessToken.Should().Be("reg-token");
    }

    [Fact]
    public async Task Register_WithValidScopes_PassesAllScopesToService()
    {
        List<string> scopes = ["inquiries.read", "inquiries.write", "storage.read"];
        RegisterAppRequest request = new("app-test", scopes);
        _developerAppService.RegisterClientAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<string?>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new DeveloperAppRegistrationResult("id", "secret", "token"));

        await _controller.Register(request, CancellationToken.None);

        await _developerAppService.Received(1).RegisterClientAsync(
            "app-test", "app-test", Arg.Is<IReadOnlyList<string>>(s => s.Count == 3),
            null, null, "user-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithMixedValidAndInvalidScopes_ReturnsValidationProblem()
    {
        RegisterAppRequest request = new("app-test", ["inquiries.read", "billing.read"]);

        ActionResult<AppRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    #endregion

    #region GetUserApps

    [Fact]
    public async Task GetUserApps_WithNoUserId_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        ActionResult<IReadOnlyList<DeveloperAppResponse>> result = await _controller.GetUserApps(CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUserApps_WithValidUser_ReturnsApps()
    {
        List<DeveloperAppInfo> apps =
        [
            new("app-one", "App One", "public", ["http://localhost"], DateTimeOffset.UtcNow),
            new("app-two", "App Two", "confidential", [], DateTimeOffset.UtcNow)
        ];
        _developerAppService.GetUserAppsAsync("user-123", Arg.Any<CancellationToken>())
            .Returns(apps);

        ActionResult<IReadOnlyList<DeveloperAppResponse>> result = await _controller.GetUserApps(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<DeveloperAppResponse> response = ok.Value.Should().BeOfType<List<DeveloperAppResponse>>().Subject;
        response.Count.Should().Be(2);
        response[0].ClientId.Should().Be("app-one");
        response[1].ClientId.Should().Be("app-two");
    }

    [Fact]
    public async Task GetUserApps_WhenEmpty_ReturnsEmptyList()
    {
        _developerAppService.GetUserAppsAsync("user-123", Arg.Any<CancellationToken>())
            .Returns(new List<DeveloperAppInfo>());

        ActionResult<IReadOnlyList<DeveloperAppResponse>> result = await _controller.GetUserApps(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<DeveloperAppResponse> response = ok.Value.Should().BeOfType<List<DeveloperAppResponse>>().Subject;
        response.Should().BeEmpty();
    }

    #endregion

    #region GetUserApp

    [Fact]
    public async Task GetUserApp_WithNoUserId_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        ActionResult<DeveloperAppResponse> result = await _controller.GetUserApp("app-one", CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUserApp_WhenNotFound_ReturnsNotFound()
    {
        _developerAppService.GetUserAppAsync("user-123", "app-nonexistent", Arg.Any<CancellationToken>())
            .Returns((DeveloperAppInfo?)null);

        ActionResult<DeveloperAppResponse> result = await _controller.GetUserApp("app-nonexistent", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetUserApp_WhenFound_ReturnsApp()
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        DeveloperAppInfo app = new("app-one", "App One", "public", ["http://localhost"], createdAt);
        _developerAppService.GetUserAppAsync("user-123", "app-one", Arg.Any<CancellationToken>())
            .Returns(app);

        ActionResult<DeveloperAppResponse> result = await _controller.GetUserApp("app-one", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        DeveloperAppResponse response = ok.Value.Should().BeOfType<DeveloperAppResponse>().Subject;
        response.ClientId.Should().Be("app-one");
        response.DisplayName.Should().Be("App One");
        response.ClientType.Should().Be("public");
        response.RedirectUris.Should().ContainSingle().Which.Should().Be("http://localhost");
        response.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region GetConsentInfo

    [Fact]
    public async Task GetConsentInfo_WithKnownClientId_Returns200WithDisplayNameAndScopes()
    {
        // Arrange
        ConsentInfoDto dto = new(
            "third-party-app",
            "My Third Party App",
            "https://example.com/logo.png",
            [new ConsentScopeDto("inquiries.read", "Read inquiries"), new ConsentScopeDto("storage.read", "Read storage")]);
        _developerAppService.GetConsentInfoAsync(
                "third-party-app",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        ActionResult<ConsentInfoResponse> result = await _controller.GetConsentInfo(
            "third-party-app", "inquiries.read storage.read", CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ConsentInfoResponse response = ok.Value.Should().BeOfType<ConsentInfoResponse>().Subject;
        response.ClientId.Should().Be("third-party-app");
        response.DisplayName.Should().Be("My Third Party App");
        response.LogoUrl.Should().Be("https://example.com/logo.png");
        response.RequestedScopes.Should().HaveCount(2);
        response.RequestedScopes[0].Name.Should().Be("inquiries.read");
        response.RequestedScopes[0].Description.Should().Be("Read inquiries");
        response.RequestedScopes[1].Name.Should().Be("storage.read");
        response.RequestedScopes[1].Description.Should().Be("Read storage");
    }

    [Fact]
    public async Task GetConsentInfo_WithKnownClientIdAndEmptyScopes_Returns200WithEmptyScopesList()
    {
        // Arrange
        ConsentInfoDto dto = new(
            "third-party-app",
            "My Third Party App",
            null,
            []);
        _developerAppService.GetConsentInfoAsync(
                "third-party-app",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        ActionResult<ConsentInfoResponse> result = await _controller.GetConsentInfo(
            "third-party-app", null, CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ConsentInfoResponse response = ok.Value.Should().BeOfType<ConsentInfoResponse>().Subject;
        response.ClientId.Should().Be("third-party-app");
        response.DisplayName.Should().Be("My Third Party App");
        response.LogoUrl.Should().BeNull();
        response.RequestedScopes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConsentInfo_WithUnknownClientId_Returns404()
    {
        // Arrange
        _developerAppService.GetConsentInfoAsync(
                "unknown-client",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns((ConsentInfoDto?)null);

        // Act
        ActionResult<ConsentInfoResponse> result = await _controller.GetConsentInfo(
            "unknown-client", null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetConsentInfo_WithFirstPartyWallowClient_StillReturnsInfo()
    {
        // Arrange - first-party clients (wallow-*) should still return info from this endpoint;
        // filtering is enforced by AuthorizationController, not here
        ConsentInfoDto dto = new(
            "wallow-web",
            "Wallow Web App",
            null,
            [new ConsentScopeDto("openid", "OpenID Connect")]);
        _developerAppService.GetConsentInfoAsync(
                "wallow-web",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        ActionResult<ConsentInfoResponse> result = await _controller.GetConsentInfo(
            "wallow-web", "openid", CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ConsentInfoResponse response = ok.Value.Should().BeOfType<ConsentInfoResponse>().Subject;
        response.ClientId.Should().Be("wallow-web");
        response.DisplayName.Should().Be("Wallow Web App");
    }

    [Fact]
    public async Task GetConsentInfo_MapsServiceDtoToResponseCorrectly()
    {
        // Arrange
        ConsentInfoDto dto = new(
            "app-mapped",
            "Mapped App",
            "https://cdn.example.com/icon.svg",
            [new ConsentScopeDto("messaging.write", "Send messages")]);
        _developerAppService.GetConsentInfoAsync(
                "app-mapped",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        ActionResult<ConsentInfoResponse> result = await _controller.GetConsentInfo(
            "app-mapped", "messaging.write", CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ConsentInfoResponse response = ok.Value.Should().BeOfType<ConsentInfoResponse>().Subject;
        response.ClientId.Should().Be(dto.ClientId);
        response.DisplayName.Should().Be(dto.DisplayName);
        response.LogoUrl.Should().Be(dto.LogoUrl);
        response.RequestedScopes.Should().HaveCount(1);
        response.RequestedScopes[0].Name.Should().Be(dto.RequestedScopes[0].Name);
        response.RequestedScopes[0].Description.Should().Be(dto.RequestedScopes[0].Description);
    }

    #endregion
}
