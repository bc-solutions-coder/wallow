using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryStatusChangedSignalRHandlerTests
{
    private readonly IRealtimeDispatcher _dispatcher = Substitute.For<IRealtimeDispatcher>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private static InquiryStatusChangedEvent BuildEvent() => new()
    {
        InquiryId = Guid.NewGuid(),
        OldStatus = "Open",
        NewStatus = "InProgress",
        ChangedAt = DateTime.UtcNow,
        SubmitterEmail = "submitter@test.com"
    };

    [Fact]
    public async Task Handle_WhenTenantResolved_DispatchesToTenant()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquiryStatusChangedEvent @event = BuildEvent();

        await InquiryStatusChangedSignalRHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            tenantId,
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryStatusUpdated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantUnresolved_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(false);

        await InquiryStatusChangedSignalRHandler.Handle(BuildEvent(), _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
    }
}
