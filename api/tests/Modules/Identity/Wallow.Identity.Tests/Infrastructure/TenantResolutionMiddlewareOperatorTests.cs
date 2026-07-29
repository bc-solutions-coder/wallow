using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.MultiTenancy;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The X-Tenant-Id override must be gated on an explicit operator flag, never on the
/// "sa-" client_id naming convention: a client id is chosen at service-account creation
/// time and is therefore spoofable by any tenant that can name its own service account.
/// </summary>
public class TenantResolutionMiddlewareOperatorTests
{
    private const string OperatorClaimType = "is_operator";

    private readonly ILogger<TenantResolutionMiddleware> _logger = Substitute.For<ILogger<TenantResolutionMiddleware>>();
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_ServiceAccountWithoutOperatorClaim_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid victimTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim("azp", $"sa-{ownTenantId.ToString()[..8]}-billing"));
        context.Request.Headers["X-Tenant-Id"] = victimTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        _nextCalled.Should().BeTrue();
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(ownTenantId);
        context.Items["TenantId"].Should().Be(ownTenantId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ServiceAccountWithOperatorClaimFalse_DoesNotOverrideTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid victimTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim("azp", $"sa-{ownTenantId.ToString()[..8]}-billing"),
            new Claim(OperatorClaimType, "false"));
        context.Request.Headers["X-Tenant-Id"] = victimTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.TenantId.Value.Should().Be(ownTenantId);
    }

    [Fact]
    public async Task InvokeAsync_OperatorClaimWithoutSaPrefixedClientId_OverridesTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid targetTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim("azp", "wallow-ops-console"),
            new Claim(OperatorClaimType, "true"));
        context.Request.Headers["X-Tenant-Id"] = targetTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Value.Should().Be(targetTenantId);
    }

    [Fact]
    public async Task InvokeAsync_OperatorServiceAccountWithOperatorClaim_OverridesTenant()
    {
        TenantContext tenantContext = new TenantContext();
        TenantResolutionMiddleware middleware = CreateMiddleware();
        Guid ownTenantId = Guid.NewGuid();
        Guid targetTenantId = Guid.NewGuid();

        DefaultHttpContext context = CreateAuthenticatedContext(
            new Claim("org_id", ownTenantId.ToString()),
            new Claim("azp", "sa-platform-operator"),
            new Claim(OperatorClaimType, "true"));
        context.Request.Headers["X-Tenant-Id"] = targetTenantId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        tenantContext.TenantId.Value.Should().Be(targetTenantId);
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
