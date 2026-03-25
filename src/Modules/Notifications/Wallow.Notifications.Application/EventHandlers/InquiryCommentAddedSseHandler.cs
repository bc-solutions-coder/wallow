using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryCommentAddedSseHandler
{
    public static async Task Handle(
        InquiryCommentAddedEvent message,
        ITenantContext tenantContext,
        ISseDispatcher dispatcher)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquiryCommentAdded",
            message);

        if (message.IsInternal)
        {
            await dispatcher.SendToTenantPermissionAsync(message.TenantId, "inquiries.manage", envelope);
            return;
        }

        bool isSubmitterComment = message.SubmitterUserId.HasValue
            && message.AuthorId == message.SubmitterUserId.Value.ToString();

        if (isSubmitterComment)
        {
            await dispatcher.SendToTenantPermissionAsync(message.TenantId, "inquiries.manage", envelope);
        }
        else if (message.SubmitterUserId.HasValue)
        {
            await dispatcher.SendToUserAsync(message.SubmitterUserId.Value.ToString(), envelope);
        }
    }
}
