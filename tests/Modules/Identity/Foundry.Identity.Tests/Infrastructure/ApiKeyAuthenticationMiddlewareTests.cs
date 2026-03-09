using System.Security.Claims;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Infrastructure;

public class ApiKeyAuthenticationMiddlewareTests
{
    private readonly IApiKeyService _apiKeyService = Substitute.For<IApiKeyService>();
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger = Substitute.For<ILogger<ApiKeyAuthenticationMiddleware>>();
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_NoApiKeyHeader_CallsNext()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        TenantContext tenantContext = new TenantContext();

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_EmptyApiKeyHeader_CallsNext()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "";
        TenantContext tenantContext = new TenantContext();

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_Returns401()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Api-Key"] = "sk_live_invalid";
        TenantContext tenantContext = new TenantContext();

        _apiKeyService.ValidateApiKeyAsync("sk_live_invalid", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(false, null, null, null, null, "Invalid API key"));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKey_SetsUserAndTenant()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "sk_live_validkey";
        TenantContext tenantContext = new TenantContext();

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        List<string> scopes = ["invoices.read", "users.read"];

        _apiKeyService.ValidateApiKeyAsync("sk_live_validkey", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(true, "key-id-1", userId, tenantId, scopes, null));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeTrue();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(userId.ToString());
        context.User.FindFirst("api_key_id")!.Value.Should().Be("key-id-1");
        context.User.FindFirst("auth_method")!.Value.Should().Be("api_key");
        context.User.FindAll("scope").Should().HaveCount(2);
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(tenantId);
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKeyNoScopes_SetsUserWithoutScopeClaims()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "sk_live_noscopes";
        TenantContext tenantContext = new TenantContext();

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyService.ValidateApiKeyAsync("sk_live_noscopes", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(true, "key-id-2", userId, tenantId, null, null));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeTrue();
        context.User.FindAll("scope").Should().BeEmpty();
    }

    private ApiKeyAuthenticationMiddleware CreateMiddleware()
    {
        _nextCalled = false;
        return new ApiKeyAuthenticationMiddleware(
            _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            _logger);
    }
}
