using System.Security.Claims;
using System.Text.Json;
using Foundry.Identity.Infrastructure.MultiTenancy;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Infrastructure;

public class TenantResolutionMiddlewareGapTests
{
    private static readonly string[] _readPermissions = ["read"];
    private readonly ILogger<TenantResolutionMiddleware> _logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_InvalidGuidInTenantIdHeader_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim(ClaimTypes.Role, "admin"));
        context.Request.Headers["X-Tenant-Id"] = "not-a-valid-guid";

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(orgId);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserWithoutOrgClaim_DoesNotResolveTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_RealmAccessWithNoRolesProperty_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        string realmAccess = JsonSerializer.Serialize(new { permissions = _readPermissions });
        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim("realm_access", realmAccess));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.TenantId.Value.Should().Be(orgId);
    }

    [Fact]
    public async Task InvokeAsync_TenantRegionClaimTakesPriorityOverHeader()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()),
            new Claim("tenant_region", "eu-central-1"));
        context.Request.Headers["X-Tenant-Region"] = "us-west-2";

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.Region.Should().Be("eu-central-1");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_SetsUserIdOnActivity()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        string userId = Guid.NewGuid().ToString();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, userId));

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NonAdminWithTenantIdHeader_IgnoresOverride()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgId.ToString()));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.TenantId.Value.Should().Be(orgId);
    }

    [Fact]
    public async Task InvokeAsync_JsonOrgClaimWithNoGuidProperty_DoesNotResolveTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();

        string orgJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["not-a-guid"] = new { name = "Test Org" }
        });
        DefaultHttpContext context = CreateAuthenticatedContext(new Claim("organization", orgJson));

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_AdminOverride_ClearsTenantName()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid orgId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();

        string orgJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [orgId.ToString()] = new { name = "Original Org" }
        });
        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("organization", orgJson),
            new Claim(ClaimTypes.Role, "admin"));
        context.Request.Headers["X-Tenant-Id"] = overrideId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

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
