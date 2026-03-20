using System.Security.Claims;
using Wallow.Identity.Infrastructure.MultiTenancy;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Tests.Infrastructure;

public class TenantResolutionMiddlewareTests
{
    private readonly ILogger<TenantResolutionMiddleware> _logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_CallsNextWithoutResolvingTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_WithGuidOrgIdClaim_ResolvesTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("org_id", orgId.ToString()));

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(orgId);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_WithInvalidOrgClaim_DoesNotResolveTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("org_id", "not-a-guid"));

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_AdminUser_WithTenantIdHeader_OverridesTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", orgId.ToString()),
            new Claim(ClaimTypes.Role, "admin"));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(overrideId);
    }

    [Fact]
    public async Task InvokeAsync_NonAdminUser_WithTenantIdHeader_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", orgId.ToString()),
            new Claim(ClaimTypes.Role, "user"));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(orgId);
    }

    [Fact]
    public async Task InvokeAsync_WithTenantRegionClaim_SetsRegion()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", orgId.ToString()),
            new Claim("tenant_region", "eu-west-1"));

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.Region.Should().Be("eu-west-1");
    }

    [Fact]
    public async Task InvokeAsync_WithRegionHeader_SetsRegion()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", orgId.ToString()));
        context.Request.Headers["X-Tenant-Region"] = "us-east-1";

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.Region.Should().Be("us-east-1");
    }

    [Fact]
    public async Task InvokeAsync_WithNoRegion_SetsPrimaryRegion()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", orgId.ToString()));

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.Region.Should().Be(RegionConfiguration.PrimaryRegion);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyOrgClaim_DoesNotResolveTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("org_id", ""));

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ServiceAccount_WithTenantIdHeader_OverridesTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", orgId.ToString()),
            new Claim("azp", "sa-test-service"));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(overrideId);
    }

    private TenantResolutionMiddleware CreateMiddleware()
    {
        _nextCalled = false;
        return new TenantResolutionMiddleware(
            _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            _logger);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(params Claim[] claims)
    {
        DefaultHttpContext context = new DefaultHttpContext();
        ClaimsIdentity identity = new(claims, "test");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }
}
