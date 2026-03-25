using System.Security.Claims;
using Wallow.ApiKeys.Api.Contracts.Requests;
using Wallow.ApiKeys.Api.Controllers;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Wallow.ApiKeys.Tests.Controllers;

public class ApiKeysControllerScopeValidationTests
{
    private readonly ApiKeysController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public ApiKeysControllerScopeValidationTests()
    {
        IApiKeyService apiKeyService = Substitute.For<IApiKeyService>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(new Shared.Kernel.Identity.TenantId(_tenantId));
        Wallow.Shared.Kernel.Services.ICurrentUserService currentUserService = Substitute.For<Wallow.Shared.Kernel.Services.ICurrentUserService>();
        currentUserService.GetCurrentUserId().Returns(_userId);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "ApiKeys:MaxPerUser", "10" } })
            .Build();

        IScopeSubsetValidator scopeSubsetValidator = Substitute.For<IScopeSubsetValidator>();
        _controller = new ApiKeysController(apiKeyService, scopeSubsetValidator, tenantContext, currentUserService, configuration);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim("permission", PermissionType.InvoicesRead),
            new Claim("permission", PermissionType.InvoicesWrite),
            new Claim("permission", PermissionType.PaymentsRead),
            new Claim("permission", PermissionType.PaymentsWrite),
            new Claim("permission", PermissionType.SubscriptionsRead),
            new Claim("permission", PermissionType.SubscriptionsWrite),
            new Claim("permission", PermissionType.UsersRead),
            new Claim("permission", PermissionType.UsersUpdate),
            new Claim("permission", PermissionType.NotificationRead),
            new Claim("permission", PermissionType.NotificationsWrite),
            new Claim("permission", PermissionType.WebhooksManage)
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        apiKeyService.CreateApiKeyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<IEnumerable<string>?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(new ApiKeyCreateResult(true, "key-id", "fnd_key", "fnd_", null));
    }

    [Fact]
    public async Task CreateApiKey_WithInvalidScopes_ReturnsBadRequest()
    {
        string[] invalidScopes = ["invalid.scope", "also.bad"];
        CreateApiKeyRequest request = new("Test Key", invalidScopes);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Invalid scopes");
        problem.Detail.Should().Contain("invalid.scope");
        problem.Detail.Should().Contain("also.bad");
    }

    [Fact]
    public async Task CreateApiKey_WithMixOfValidAndInvalidScopes_ReturnsBadRequest()
    {
        string[] scopes = ["invoices.read", "bad.scope"];
        CreateApiKeyRequest request = new("Test Key", scopes);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("bad.scope");
        problem.Detail.Should().NotContain("invoices.read");
    }

    [Fact]
    public async Task CreateApiKey_WithAllValidScopes_DoesNotReturnBadRequestForScopes()
    {
        string[] allValid =
        [
            "invoices.read", "invoices.write",
            "payments.read", "payments.write",
            "subscriptions.read", "subscriptions.write",
            "users.read", "users.write",
            "notifications.read", "notifications.write",
            "webhooks.manage"
        ];
        CreateApiKeyRequest request = new("Test Key", allValid);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateApiKey_WithNullScopes_Succeeds()
    {
        CreateApiKeyRequest request = new("Test Key");

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateApiKey_WithEmptyScopes_Succeeds()
    {
        CreateApiKeyRequest request = new("Test Key", []);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateApiKey_ScopeExceedsUserPermissions_Returns403()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim("permission", PermissionType.StorageRead)
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        CreateApiKeyRequest request = new("Test Key", ["billing.manage"]);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("billing.manage");
    }

    [Fact]
    public async Task CreateApiKey_ScopeWithinUserPermissions_Succeeds()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim("permission", PermissionType.BillingManage),
            new Claim("permission", PermissionType.StorageRead)
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        CreateApiKeyRequest request = new("Test Key", ["billing.manage", "storage.read"]);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }
}
