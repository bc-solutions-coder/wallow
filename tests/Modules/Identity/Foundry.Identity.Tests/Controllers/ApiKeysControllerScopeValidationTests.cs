using System.Security.Claims;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Tests.Controllers;

public class ApiKeysControllerScopeValidationTests
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ITenantContext _tenantContext;
    private readonly Foundry.Shared.Kernel.Services.ICurrentUserService _currentUserService;
    private readonly ApiKeysController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public ApiKeysControllerScopeValidationTests()
    {
        _apiKeyService = Substitute.For<IApiKeyService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(new Shared.Kernel.Identity.TenantId(_tenantId));
        _currentUserService = Substitute.For<Foundry.Shared.Kernel.Services.ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _controller = new ApiKeysController(_apiKeyService, _tenantContext, _currentUserService);

        ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        _apiKeyService.CreateApiKeyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<IEnumerable<string>?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(new ApiKeyCreateResult(true, "key-id", "fnd_key", "fnd_", null));
    }

    [Fact]
    public async Task CreateApiKey_WithInvalidScopes_ReturnsBadRequest()
    {
        List<string> invalidScopes = new() { "invalid.scope", "also.bad" };
        CreateApiKeyRequest request = new("Test Key", invalidScopes, null);

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
        List<string> scopes = new() { "invoices.read", "bad.scope" };
        CreateApiKeyRequest request = new("Test Key", scopes, null);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("bad.scope");
        problem.Detail.Should().NotContain("invoices.read");
    }

    [Fact]
    public async Task CreateApiKey_WithAllValidScopes_DoesNotReturnBadRequestForScopes()
    {
        List<string> allValid = new()
        {
            "invoices.read", "invoices.write",
            "payments.read", "payments.write",
            "subscriptions.read", "subscriptions.write",
            "users.read", "users.write",
            "notifications.read", "notifications.write",
            "webhooks.manage"
        };
        CreateApiKeyRequest request = new("Test Key", allValid, null);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateApiKey_WithNullScopes_Succeeds()
    {
        CreateApiKeyRequest request = new("Test Key", null, null);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateApiKey_WithEmptyScopes_Succeeds()
    {
        List<string> emptyScopes = new();
        CreateApiKeyRequest request = new("Test Key", emptyScopes, null);

        IActionResult result = await _controller.CreateApiKey(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }
}
