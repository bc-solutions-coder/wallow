using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Api.Controllers;

public class ClientRegistrationControllerTests
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IApiScopeRepository _apiScopeRepository;
    private readonly IInitialAccessTokenRepository _initialAccessTokenRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IHostEnvironment _environment;
    private readonly ClientRegistrationController _controller;

    public ClientRegistrationControllerTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _apiScopeRepository = Substitute.For<IApiScopeRepository>();
        _initialAccessTokenRepository = Substitute.For<IInitialAccessTokenRepository>();
        _organizationService = Substitute.For<IOrganizationService>();
        _environment = Substitute.For<IHostEnvironment>();
        _environment.EnvironmentName.Returns("Production");

        _controller = new ClientRegistrationController(
            _applicationManager,
            _apiScopeRepository,
            _initialAccessTokenRepository,
            _organizationService,
            _environment);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private void SetBearerToken(string token)
    {
        _controller.HttpContext.Request.Headers.Authorization = $"Bearer {token}";
    }

    private void SetDevelopmentEnvironment()
    {
        _environment.EnvironmentName.Returns("Development");
    }

    private void SetupValidToken(string rawToken)
    {
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken));
        string tokenHash = Convert.ToBase64String(hashBytes);

        InitialAccessToken token = InitialAccessToken.Create(tokenHash, "Valid Token", DateTimeOffset.UtcNow.AddDays(7));
        _initialAccessTokenRepository.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(token);
    }

    private void SetupExpiredToken(string rawToken)
    {
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken));
        string tokenHash = Convert.ToBase64String(hashBytes);

        InitialAccessToken token = InitialAccessToken.Create(tokenHash, "Expired Token", DateTimeOffset.UtcNow.AddDays(-1));
        _initialAccessTokenRepository.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(token);
    }

    private void SetupRevokedToken(string rawToken)
    {
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken));
        string tokenHash = Convert.ToBase64String(hashBytes);

        InitialAccessToken token = InitialAccessToken.Create(tokenHash, "Revoked Token", DateTimeOffset.UtcNow.AddDays(7));
        token.Revoke();
        _initialAccessTokenRepository.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(token);
    }

    private void SetupScopesExist(params string[] codes)
    {
        List<ApiScope> scopes = codes.Select(c => ApiScope.Create(c, c, "Test")).ToList();
        _apiScopeRepository.GetByCodesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(scopes);
    }

    private void SetUserClaims(Guid userId)
    {
        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "TestAuth");
        _controller.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private static RegisterClientRequest MakeSaRequest(
        string clientId = "sa-test",
        IReadOnlyList<string>? scopes = null,
        IReadOnlyList<string>? redirectUris = null,
        Guid? tenantId = null)
    {
        return new RegisterClientRequest(
            clientId,
            "Test SA Client",
            [GrantTypes.ClientCredentials],
            scopes ?? [],
            redirectUris,
            tenantId);
    }

    private static RegisterClientRequest MakeAppRequest(
        string clientId = "app-test",
        IReadOnlyList<string>? scopes = null,
        IReadOnlyList<string>? redirectUris = null,
        Guid? tenantId = null)
    {
        return new RegisterClientRequest(
            clientId,
            "Test App Client",
            [GrantTypes.AuthorizationCode],
            scopes ?? [],
            redirectUris,
            tenantId);
    }

    [Fact]
    public async Task Register_InDev_SkipsTokenValidation_ReturnsCreated()
    {
        SetDevelopmentEnvironment();
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(MakeSaRequest(), CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        await _applicationManager.Received(1).CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_InvalidToken_Returns401()
    {
        SetBearerToken("invalid-token");
        _initialAccessTokenRepository.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((InitialAccessToken?)null);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(MakeSaRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Register_ExpiredToken_Returns401()
    {
        string rawToken = "expired-token";
        SetBearerToken(rawToken);
        SetupExpiredToken(rawToken);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(MakeSaRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Register_RevokedToken_Returns401()
    {
        string rawToken = "revoked-token";
        SetBearerToken(rawToken);
        SetupRevokedToken(rawToken);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(MakeSaRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Register_ValidToken_ReturnsCreated()
    {
        string rawToken = "valid-token";
        SetBearerToken(rawToken);
        SetupValidToken(rawToken);
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(MakeSaRequest(), CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Register_UnknownScope_Returns400()
    {
        SetDevelopmentEnvironment();
        _apiScopeRepository.GetByCodesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ApiScope>());
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        RegisterClientRequest request = MakeSaRequest(scopes: ["unknown.scope"]);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("Unknown scopes");
    }

    [Fact]
    public async Task Register_ClientCredentials_WithoutSaPrefix_Returns400()
    {
        SetDevelopmentEnvironment();

        RegisterClientRequest request = MakeSaRequest(clientId: "bad-prefix");

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("sa-");
    }

    [Fact]
    public async Task Register_AuthorizationCode_WithoutAppPrefix_Returns400()
    {
        SetDevelopmentEnvironment();

        RegisterClientRequest request = MakeAppRequest(clientId: "bad-prefix");

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("app-");
    }

    [Fact]
    public async Task Register_MixedGrantTypes_Returns400()
    {
        SetDevelopmentEnvironment();

        RegisterClientRequest request = new(
            "sa-mixed",
            "Mixed Client",
            [GrantTypes.ClientCredentials, GrantTypes.AuthorizationCode],
            [],
            null);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("Cannot mix");
    }

    [Fact]
    public async Task Register_NewSaClient_Returns201_WithSecret()
    {
        SetDevelopmentEnvironment();
        _applicationManager.FindByClientIdAsync("sa-new", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        RegisterClientRequest request = MakeSaRequest(clientId: "sa-new");

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        ClientRegistrationResponse response = objectResult.Value.Should().BeOfType<ClientRegistrationResponse>().Subject;
        response.ClientId.Should().Be("sa-new");
        response.ClientSecret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_NewAppClient_Returns201_WithSecret()
    {
        SetDevelopmentEnvironment();
        _applicationManager.FindByClientIdAsync("app-new", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        RegisterClientRequest request = MakeAppRequest(clientId: "app-new");

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        ClientRegistrationResponse response = objectResult.Value.Should().BeOfType<ClientRegistrationResponse>().Subject;
        response.ClientId.Should().Be("app-new");
        response.ClientSecret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_ExistingClient_RotatesSecret_Returns200()
    {
        SetDevelopmentEnvironment();
        object existingApp = new object();
        _applicationManager.FindByClientIdAsync("sa-existing", Arg.Any<CancellationToken>())
            .Returns(existingApp);

        RegisterClientRequest request = MakeSaRequest(clientId: "sa-existing");

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        OkObjectResult okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientRegistrationResponse response = okResult.Value.Should().BeOfType<ClientRegistrationResponse>().Subject;
        response.ClientId.Should().Be("sa-existing");
        response.ClientSecret.Should().NotBeNullOrEmpty();
        await _applicationManager.Received(1).UpdateAsync(existingApp, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ExistingClient_DoesNotUpdateScopes()
    {
        SetDevelopmentEnvironment();
        object existingApp = new object();
        _applicationManager.FindByClientIdAsync("sa-existing", Arg.Any<CancellationToken>())
            .Returns(existingApp);
        SetupScopesExist("billing.read");

        RegisterClientRequest request = MakeSaRequest(clientId: "sa-existing", scopes: ["billing.read"]);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        await _applicationManager.Received(1).PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(), existingApp, Arg.Any<CancellationToken>());
        await _applicationManager.Received(1).UpdateAsync(
            existingApp,
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ClientSecret != null),
            Arg.Any<CancellationToken>());
        // Scopes should not be re-applied — only secret rotation happens for existing clients
        await _applicationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithTenantId_CallerNotMember_Returns403()
    {
        SetDevelopmentEnvironment();
        Guid tenantId = Guid.NewGuid();
        Guid callerId = Guid.NewGuid();
        SetUserClaims(callerId);

        _organizationService.GetMembersAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>());

        RegisterClientRequest request = MakeSaRequest(tenantId: tenantId);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Register_WithTenantId_NoCallerIdentity_Returns403()
    {
        SetDevelopmentEnvironment();
        Guid tenantId = Guid.NewGuid();

        RegisterClientRequest request = MakeSaRequest(tenantId: tenantId);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Register_WithTenantId_CallerIsMember_Returns201_WithTenantBound()
    {
        SetDevelopmentEnvironment();
        Guid tenantId = Guid.NewGuid();
        Guid callerId = Guid.NewGuid();
        SetUserClaims(callerId);

        _organizationService.GetMembersAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>
            {
                new(callerId, "user@test.com", "Test", "User", true, [])
            });

        _applicationManager.FindByClientIdAsync("sa-tenant", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        RegisterClientRequest request = MakeSaRequest(clientId: "sa-tenant", tenantId: tenantId);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);

        await _applicationManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.Properties.ContainsKey("tenant_id")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithTenantId_ExistingClient_RotatesSecretAndPreservesTenant()
    {
        SetDevelopmentEnvironment();
        Guid tenantId = Guid.NewGuid();
        Guid callerId = Guid.NewGuid();
        SetUserClaims(callerId);

        _organizationService.GetMembersAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>
            {
                new(callerId, "user@test.com", "Test", "User", true, [])
            });

        object existingApp = new object();
        _applicationManager.FindByClientIdAsync("sa-existing", Arg.Any<CancellationToken>())
            .Returns(existingApp);

        RegisterClientRequest request = MakeSaRequest(clientId: "sa-existing", tenantId: tenantId);

        ActionResult<ClientRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        await _applicationManager.Received(1).UpdateAsync(
            existingApp,
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.Properties.ContainsKey("tenant_id")),
            Arg.Any<CancellationToken>());
    }
}
