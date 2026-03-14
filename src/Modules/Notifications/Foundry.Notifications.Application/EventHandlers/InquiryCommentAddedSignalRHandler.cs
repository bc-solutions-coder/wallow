using Foundry.Shared.Contracts.Inquiries.Events;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Notifications.Application.EventHandlers;

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
