using Foundry.Shared.Infrastructure.Middleware;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using NSubstitute;
using Wolverine;

namespace Foundry.Shared.Infrastructure.Tests.Messaging;

public class TenantStampingMiddlewareTests
{
    [Fact]
    public void Before_WhenTenantIsResolved_SetsHeader()
    {
        Guid tenantGuid = Guid.NewGuid();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.TenantId.Returns(TenantId.Create(tenantGuid));
        Envelope envelope = new();

        TenantStampingMiddleware.Before(envelope, tenantContext);

        envelope.Headers["X-Tenant-Id"].Should().Be(tenantGuid.ToString());
    }

    [Fact]
    public void Before_WhenTenantIsNotResolved_DoesNotSetHeader()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(false);
        Envelope envelope = new();

        TenantStampingMiddleware.Before(envelope, tenantContext);

        envelope.Headers.Should().NotContainKey("X-Tenant-Id");
    }
}
