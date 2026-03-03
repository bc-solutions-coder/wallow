using System.Security.Claims;
using System.Text.Json;
using Foundry.Identity.Infrastructure.MultiTenancy;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Infrastructure;

public class TenantResolutionMiddlewareTests
{
    private static readonly string[] _adminUserRoles = ["admin", "user"];
    private readonly ILogger<TenantResolutionMiddleware> _logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_CallsNextWithoutResolvingTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_WithGuidOrganizationClaim_ResolvesTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("organization", orgId.ToString()));

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(orgId);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_WithKeycloak26JsonFormat_ResolvesTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        string orgJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [orgId.ToString()] = new { name = "Test Org" }
        });
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("organization", orgJson));

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(orgId);
        tenantContext.TenantName.Should().Be("Test Org");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_WithInvalidOrgClaim_DoesNotResolveTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("organization", "not-a-guid"));

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_AdminUser_WithTenantIdHeader_OverridesTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim(ClaimTypes.Role, "admin"));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(overrideId);
    }

    [Fact]
    public async Task InvokeAsync_NonAdminUser_WithTenantIdHeader_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim(ClaimTypes.Role, "user"));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(orgId);
    }

    [Fact]
    public async Task InvokeAsync_AdminUser_WithRealmAccessClaim_OverridesTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();
        string realmAccess = JsonSerializer.Serialize(new { roles = _adminUserRoles });

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim("realm_access", realmAccess));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(overrideId);
    }

    [Fact]
    public async Task InvokeAsync_WithTenantRegionClaim_SetsRegion()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim("tenant_region", "eu-west-1"));

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.Region.Should().Be("eu-west-1");
    }

    [Fact]
    public async Task InvokeAsync_WithRegionHeader_SetsRegion()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()));
        context.Request.Headers["X-Tenant-Region"] = "us-east-1";

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.Region.Should().Be("us-east-1");
    }

    [Fact]
    public async Task InvokeAsync_WithNoRegion_SetsPrimaryRegion()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()));

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.Region.Should().Be(RegionConfiguration.PrimaryRegion);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyOrgClaim_DoesNotResolveTenant()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("organization", ""));

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidRealmAccessJson_DoesNotCrash()
    {
        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim("realm_access", "not-valid-json"));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        // Should not override since realm_access is invalid and no admin role found
        tenantContext.TenantId.Value.Should().Be(orgId);
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
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new(claims, "test");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }
}
