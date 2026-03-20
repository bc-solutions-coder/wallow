using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquirySubmittedSignalRHandlerTests
{
    private readonly IRealtimeDispatcher _dispatcher = Substitute.For<IRealtimeDispatcher>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private static InquirySubmittedEvent BuildEvent() => new()
    {
        InquiryId = Guid.NewGuid(),
        Name = "Jane Doe",
        Email = "jane@test.com",
        Phone = "555-0100",
        ProjectType = "Sales",
        Message = "Hello",
        SubmittedAt = DateTime.UtcNow,
        AdminEmail = "admin@company.com"
    };

    [Fact]
    public async Task Handle_WhenTenantResolved_DispatchesToTenant()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquirySubmittedEvent @event = BuildEvent();

        await InquirySubmittedSignalRHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            tenantId,
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquirySubmitted"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantUnresolved_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(false);

        await InquirySubmittedSignalRHandler.Handle(BuildEvent(), _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
    }
}
