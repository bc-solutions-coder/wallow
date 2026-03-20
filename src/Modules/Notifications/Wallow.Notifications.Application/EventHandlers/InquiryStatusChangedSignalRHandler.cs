using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryStatusChangedSignalRHandler
{
    public static async Task Handle(
        InquiryStatusChangedEvent message,
        ITenantContext tenantContext,
        IRealtimeDispatcher dispatcher)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquiryStatusUpdated",
            new { message.InquiryId, message.NewStatus });

        await dispatcher.SendToTenantAsync(tenantContext.TenantId.Value, envelope);
    }
}
