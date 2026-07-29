using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.MultiTenancy;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The cross-tenant X-Tenant-Id override must be gated on the non-assignable global-admin
/// claim (or the platform operator flag), never on the "admin" ROLE string: that role is
/// handed out freely by UsersController.AssignRole to any member of the caller's own tenant,
/// so trusting it turns a same-tenant role grant into cross-tenant reach (finding F5).
/// </summary>
public sealed class TenantResolutionMiddlewareGlobalAdminTests
{
    private readonly ILogger<TenantResolutionMiddleware> _logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_TenantAdminRoleWithoutGlobalAdminClaim_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid victimTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("role", "admin"));
        context.Request.Headers["X-Tenant-Id"] = victimTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(
            ownTenantId,
            "a tenant-assignable admin role must not reach another tenant");
        context.Items["TenantId"].Should().Be(ownTenantId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_GlobalAdminClaim_OverridesTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid targetTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "true"));
        context.Request.Headers["X-Tenant-Id"] = targetTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(
            targetTenantId,
            "the global-admin claim is the cross-tenant governance escape hatch");
        context.Items["TenantId"].Should().Be(targetTenantId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_GlobalAdminClaimFalseWithAdminRole_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid victimTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "false"));
        context.Request.Headers["X-Tenant-Id"] = victimTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.TenantId.Value.Should().Be(
            ownTenantId,
            "the claim value must be honoured, not merely its presence");
    }

    [Fact]
    public async Task InvokeAsync_OperatorServiceAccount_StillOverridesTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid targetTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim("azp", "sa-platform-operator"),
            new Claim(ClaimsPrincipalExtensions.OperatorClaimType, "true"));
        context.Request.Headers["X-Tenant-Id"] = targetTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.TenantId.Value.Should().Be(
            targetTenantId,
            "removing the admin-role gate must not regress the operator gate from Wallow-pu6a.1.4");
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
