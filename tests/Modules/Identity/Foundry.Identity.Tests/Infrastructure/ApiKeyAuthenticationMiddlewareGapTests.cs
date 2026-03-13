using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Infrastructure;

public class ApiKeyAuthenticationMiddlewareGapTests
{
    private readonly IApiKeyService _apiKeyService = Substitute.For<IApiKeyService>();
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger = Substitute.For<ILogger<ApiKeyAuthenticationMiddleware>>();
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_WhitespaceOnlyApiKeyHeader_CallsNext()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "   ";
        TenantContext tenantContext = new TenantContext();

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeTrue();
        await _apiKeyService.DidNotReceive().ValidateApiKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKeyWithEmptyScopesList_SetsUserWithoutScopeClaims()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "sk_live_emptyscopes";
        TenantContext tenantContext = new TenantContext();

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        List<string> emptyScopes = [];

        _apiKeyService.ValidateApiKeyAsync("sk_live_emptyscopes", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(true, "key-id-3", userId, tenantId, emptyScopes, null));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeTrue();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.User.FindAll("scope").Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_DoesNotCallNext()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Api-Key"] = "sk_live_bad";
        TenantContext tenantContext = new TenantContext();

        _apiKeyService.ValidateApiKeyAsync("sk_live_bad", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(false, null, null, null, null, "Invalid API key"));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        _nextCalled.Should().BeFalse();
        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_WritesProperProblemJson()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Api-Key"] = "sk_live_bad";
        TenantContext tenantContext = new TenantContext();

        _apiKeyService.ValidateApiKeyAsync("sk_live_bad", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(false, null, null, null, null, "Key revoked"));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        context.Response.StatusCode.Should().Be(401);
        context.Response.ContentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKeyWithNullError_Returns401WithDefaultMessage()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Api-Key"] = "sk_live_nullerr";
        TenantContext tenantContext = new TenantContext();

        _apiKeyService.ValidateApiKeyAsync("sk_live_nullerr", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(false, null, null, null, null, null));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKey_SetsOrganizationClaim()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "sk_live_orgclaim";
        TenantContext tenantContext = new TenantContext();

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyService.ValidateApiKeyAsync("sk_live_orgclaim", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(true, "key-org", userId, tenantId, null, null));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        context.User.FindFirst("organization")!.Value.Should().Be(tenantId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKey_SetsSubClaim()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "sk_live_subclaim";
        TenantContext tenantContext = new TenantContext();

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyService.ValidateApiKeyAsync("sk_live_subclaim", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(true, "key-sub", userId, tenantId, null, null));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        context.User.FindFirst("sub")!.Value.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKey_SetsTenantContextWithApiKeyPrefix()
    {
        ApiKeyAuthenticationMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "sk_live_tenant";
        TenantContext tenantContext = new TenantContext();

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyService.ValidateApiKeyAsync("sk_live_tenant", Arg.Any<CancellationToken>())
            .Returns(new ApiKeyValidationResult(true, "key-99", userId, tenantId, null, null));

        await middleware.InvokeAsync(context, _apiKeyService, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(tenantId);
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
