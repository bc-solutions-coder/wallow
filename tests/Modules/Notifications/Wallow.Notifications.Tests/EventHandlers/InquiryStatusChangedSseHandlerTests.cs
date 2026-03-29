using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryStatusChangedSseHandlerTests
{
    private readonly ISseDispatcher _dispatcher = Substitute.For<ISseDispatcher>();
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
    public async Task Handle_WhenTenantResolved_CallsSendToTenantAsyncOnce()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquiryStatusChangedEvent @event = BuildEvent();

        await InquiryStatusChangedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            tenantId,
            Arg.Any<RealtimeEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantResolved_SendsEnvelopeWithCorrectType()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquiryStatusChangedEvent @event = BuildEvent();

        await InquiryStatusChangedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            tenantId,
            Arg.Is<RealtimeEnvelope>(e => e.Type == "InquiryStatusUpdated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantResolved_SendsEnvelopeWithRequiredPermission()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquiryStatusChangedEvent @event = BuildEvent();

        await InquiryStatusChangedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            tenantId,
            Arg.Is<RealtimeEnvelope>(e => e.RequiredPermission == "inquiries.read"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantResolved_PayloadContainsInquiryIdAndNewStatus()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquiryStatusChangedEvent @event = BuildEvent();

        RealtimeEnvelope? capturedEnvelope = null;
        await _dispatcher.SendToTenantAsync(
            Arg.Any<Guid>(),
            Arg.Do<RealtimeEnvelope>(e => capturedEnvelope = e),
            Arg.Any<CancellationToken>());

        await InquiryStatusChangedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        capturedEnvelope.Should().NotBeNull();
        string payloadJson = System.Text.Json.JsonSerializer.Serialize(capturedEnvelope!.Payload);
        payloadJson.Should().Contain(@event.InquiryId.ToString());
        payloadJson.Should().Contain(@event.NewStatus);
    }

    [Fact]
    public async Task Handle_WhenTenantUnresolved_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(false);

        await InquiryStatusChangedSseHandler.Handle(BuildEvent(), _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
    }
}
