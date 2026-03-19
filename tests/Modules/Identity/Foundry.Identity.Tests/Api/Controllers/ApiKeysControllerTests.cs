using System.Security.Claims;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Foundry.Identity.Tests.Api.Controllers;

public class ApiKeysControllerTests
{
    private static readonly string[] _billingReadScope = ["invoices.read"];
    private static readonly string[] _twoScopes = ["invoices.read", "payments.write"];
    private readonly IApiKeyService _apiKeyService;
    private readonly ITenantContext _tenantContext;
    private readonly Foundry.Shared.Kernel.Services.ICurrentUserService _currentUserService;
    private readonly ApiKeysController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public ApiKeysControllerTests()
    {
        _apiKeyService = Substitute.For<IApiKeyService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(new Shared.Kernel.Identity.TenantId(_tenantId));
        _currentUserService = Substitute.For<Foundry.Shared.Kernel.Services.ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "ApiKeys:MaxPerUser", "10" } })
            .Build();

        _controller = new ApiKeysController(_apiKeyService, _tenantContext, _currentUserService, configuration);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim("permission", PermissionType.InvoicesRead),
            new Claim("permission", PermissionType.PaymentsWrite)
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region CreateApiKey

    [Fact]
    public async Task CreateApiKey_WithValidRequest_ReturnsCreated()
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        CreateApiKeyRequest request = new("Production Key", _billingReadScope, expiresAt);
        ApiKeyCreateResult createResult = new(true, "key-id-1", "fnd_full-api-key", "fnd_full", null);
        _apiKeyService.CreateApiKeyAsync(
            "Production Key", _userId, _tenantId,
            Arg.Any<IEnumerable<string>?>(), expiresAt, Arg.Any<CancellationToken>())
            .Returns(createResult);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        CreatedAtActionResult created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(ApiKeysController.ListApiKeys));
        ApiKeyCreatedResponse response = created.Value.Should().BeOfType<ApiKeyCreatedResponse>().Subject;
        response.KeyId.Should().Be("key-id-1");
        response.ApiKey.Should().Be("fnd_full-api-key");
        response.Prefix.Should().Be("fnd_full");
        response.Name.Should().Be("Production Key");
    }

    [Fact]
    public async Task CreateApiKey_WithEmptyName_ReturnsBadRequest()
    {
        CreateApiKeyRequest request = new("");

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("API key name is required");
    }

    [Fact]
    public async Task CreateApiKey_WithWhitespaceName_ReturnsBadRequest()
    {
        CreateApiKeyRequest request = new("   ");

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateApiKey_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        CreateApiKeyRequest request = new("Key");

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task CreateApiKey_WithEmptyTenantId_ReturnsBadRequest()
    {
        _tenantContext.TenantId.Returns(new Shared.Kernel.Identity.TenantId(Guid.Empty));
        CreateApiKeyRequest request = new("Key");

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("organization");
    }

    [Fact]
    public async Task CreateApiKey_WhenServiceFails_ReturnsBadRequest()
    {
        CreateApiKeyRequest request = new("Key");
        ApiKeyCreateResult createResult = new(false, null, null, null, "Duplicate key name");
        _apiKeyService.CreateApiKeyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<IEnumerable<string>?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(createResult);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Duplicate key name");
    }

    #endregion

    #region ListApiKeys

    [Fact]
    public async Task ListApiKeys_ReturnsOkWithApiKeys()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ApiKeyMetadata> keys =
        [
            new ApiKeyMetadata("key-1", "Prod Key", "fnd_prod", _userId, _tenantId,
                _billingReadScope, now, now.AddDays(30), now.AddHours(-1))
        ];
        _apiKeyService.ListApiKeysAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(keys);

        IActionResult result = await _controller.ListApiKeys(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        List<ApiKeyResponse> response = ok.Value.Should().BeOfType<List<ApiKeyResponse>>().Subject;
        response.Should().HaveCount(1);
        response[0].KeyId.Should().Be("key-1");
        response[0].Name.Should().Be("Prod Key");
        response[0].Prefix.Should().Be("fnd_prod");
    }

    [Fact]
    public async Task ListApiKeys_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.ListApiKeys(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ListApiKeys_MapsAllMetadataFields()
    {
        DateTimeOffset created = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddDays(23);
        DateTimeOffset lastUsed = DateTimeOffset.UtcNow.AddHours(-2);
        List<ApiKeyMetadata> keys =
        [
            new ApiKeyMetadata("k1", "Key", "pfx", _userId, _tenantId,
                _twoScopes, created, expires, lastUsed)
        ];
        _apiKeyService.ListApiKeysAsync(_userId, Arg.Any<CancellationToken>()).Returns(keys);

        IActionResult result = await _controller.ListApiKeys(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        List<ApiKeyResponse> response = ok.Value.Should().BeOfType<List<ApiKeyResponse>>().Subject;
        response[0].CreatedAt.Should().Be(created);
        response[0].ExpiresAt.Should().Be(expires);
        response[0].LastUsedAt.Should().Be(lastUsed);
        response[0].Scopes.Should().HaveCount(2);
    }

    #endregion

    #region RevokeApiKey

    [Fact]
    public async Task RevokeApiKey_WhenRevoked_ReturnsNoContent()
    {
        _apiKeyService.RevokeApiKeyAsync("key-1", _userId, Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.RevokeApiKey("key-1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RevokeApiKey_WhenNotFound_ReturnsNotFound()
    {
        _apiKeyService.RevokeApiKeyAsync("nonexistent", _userId, Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.RevokeApiKey("nonexistent", CancellationToken.None);

        NotFoundObjectResult notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ProblemDetails problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task RevokeApiKey_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.RevokeApiKey("key-1", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task RevokeApiKey_WithSubClaim_UsesSubClaimAsUserId()
    {
        Guid subUserId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", subUserId.ToString())
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns(subUserId);
        _apiKeyService.RevokeApiKeyAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _controller.RevokeApiKey("key-1", CancellationToken.None);

        await _apiKeyService.Received(1).RevokeApiKeyAsync("key-1", subUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateApiKey_WithSubClaim_UsesSubClaimAsUserId()
    {
        Guid subUserId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", subUserId.ToString())
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns(subUserId);
        CreateApiKeyRequest request = new("Key");
        ApiKeyCreateResult createResult = new(true, "id", "key", "pfx", null);
        _apiKeyService.CreateApiKeyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<IEnumerable<string>?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(createResult);

        await _controller.CreateApiKey(request, CancellationToken.None);

        await _apiKeyService.Received(1).CreateApiKeyAsync(
            "Key", subUserId, _tenantId,
            Arg.Any<IEnumerable<string>?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateApiKey_WithNonGuidUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        CreateApiKeyRequest request = new("Key");

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion
}
