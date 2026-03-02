using Foundry.Shared.Infrastructure.Middleware;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using NSubstitute;
using Wolverine;

namespace Foundry.Shared.Infrastructure.Tests.Messaging;

public class TenantRestoringMiddlewareTests
{
    [Fact]
    public void Before_WithValidTenantHeader_CallsSetTenant()
    {
        Guid tenantGuid = Guid.NewGuid();
        ITenantContextSetter setter = Substitute.For<ITenantContextSetter>();
        Envelope envelope = new();
        envelope.Headers["X-Tenant-Id"] = tenantGuid.ToString();

        TenantRestoringMiddleware.Before(envelope, setter);

        setter.Received(1).SetTenant(TenantId.Create(tenantGuid));
    }

    [Fact]
    public void Before_WithMissingHeader_DoesNotCallSetTenant()
    {
        ITenantContextSetter setter = Substitute.For<ITenantContextSetter>();
        Envelope envelope = new();

        TenantRestoringMiddleware.Before(envelope, setter);

        setter.DidNotReceiveWithAnyArgs().SetTenant(default);
    }

    [Fact]
    public void Before_WithInvalidGuidHeader_DoesNotCallSetTenant()
    {
        ITenantContextSetter setter = Substitute.For<ITenantContextSetter>();
        Envelope envelope = new();
        envelope.Headers["X-Tenant-Id"] = "not-a-guid";

        TenantRestoringMiddleware.Before(envelope, setter);

        setter.DidNotReceiveWithAnyArgs().SetTenant(default);
    }
}
