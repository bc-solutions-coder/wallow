using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryCommentAddedSignalRHandler
{
    public static async Task Handle(
        InquiryCommentAddedEvent message,
        ITenantContext tenantContext,
        IRealtimeDispatcher dispatcher)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquiryCommentAdded",
            message);

        await dispatcher.SendToTenantAsync(message.TenantId, envelope);
    }
}
