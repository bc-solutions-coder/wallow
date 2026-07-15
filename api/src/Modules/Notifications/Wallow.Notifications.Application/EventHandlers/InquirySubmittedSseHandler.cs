using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquirySubmittedSseHandler
{
    public static async Task Handle(
        InquirySubmittedEvent message,
        ITenantContext tenantContext,
        ISseDispatcher dispatcher)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquirySubmitted",
            new { message.InquiryId, message.Name, message.Email }) with
        {
            RequiredPermission = "inquiries.read"
        };

        await dispatcher.SendToTenantAsync(tenantContext.TenantId.Value, envelope);
    }
}
