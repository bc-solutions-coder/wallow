using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquirySubmittedSseHandlerTests
{
    private readonly ISseDispatcher _dispatcher = Substitute.For<ISseDispatcher>();
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
        AdminEmail = "admin@company.com",
        AdminUserIds = []
    };

    [Fact]
    public async Task Handle_WhenTenantResolved_SendsToTenantWithCorrectEnvelope()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquirySubmittedEvent @event = BuildEvent();

        await InquirySubmittedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            tenantId,
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquirySubmitted" &&
                e.RequiredPermission == "inquiries.read"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantUnresolved_DoesNotCallDispatcher()
    {
        _tenantContext.IsResolved.Returns(false);

        await InquirySubmittedSseHandler.Handle(BuildEvent(), _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
    }
}
