using NSubstitute;
using Wallow.Shared.Infrastructure.Core.Middleware;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Shared.Infrastructure.Tests.Messaging;

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
    public void Before_WithMissingHeaderAndNoMessage_DoesNotCallSetTenant()
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

    [Fact]
    public void Before_WithMissingHeader_FallsBackToMessageTenantId()
    {
        Guid tenantGuid = Guid.NewGuid();
        ITenantContextSetter setter = Substitute.For<ITenantContextSetter>();
        Envelope envelope = new() { Message = new MessageWithTenantId { TenantId = tenantGuid } };

        TenantRestoringMiddleware.Before(envelope, setter);

        setter.Received(1).SetTenant(TenantId.Create(tenantGuid));
    }

    [Fact]
    public void Before_WithMissingHeader_IgnoresEmptyMessageTenantId()
    {
        ITenantContextSetter setter = Substitute.For<ITenantContextSetter>();
        Envelope envelope = new() { Message = new MessageWithTenantId { TenantId = Guid.Empty } };

        TenantRestoringMiddleware.Before(envelope, setter);

        setter.DidNotReceiveWithAnyArgs().SetTenant(default);
    }

    [Fact]
    public void Before_WithMissingHeader_IgnoresMessageWithoutTenantId()
    {
        ITenantContextSetter setter = Substitute.For<ITenantContextSetter>();
        Envelope envelope = new() { Message = new MessageWithoutTenantId() };

        TenantRestoringMiddleware.Before(envelope, setter);

        setter.DidNotReceiveWithAnyArgs().SetTenant(default);
    }

    [Fact]
    public void Before_WithValidHeader_DoesNotCheckMessageBody()
    {
        Guid headerTenantGuid = Guid.NewGuid();
        Guid messageTenantGuid = Guid.NewGuid();
        ITenantContextSetter setter = Substitute.For<ITenantContextSetter>();
        Envelope envelope = new() { Message = new MessageWithTenantId { TenantId = messageTenantGuid } };
        envelope.Headers["X-Tenant-Id"] = headerTenantGuid.ToString();

        TenantRestoringMiddleware.Before(envelope, setter);

        setter.Received(1).SetTenant(TenantId.Create(headerTenantGuid));
    }

    private sealed class MessageWithTenantId
    {
        public Guid TenantId { get; set; }
    }

    private sealed class MessageWithoutTenantId
    {
        public string Name { get; set; } = "test";
    }
}
