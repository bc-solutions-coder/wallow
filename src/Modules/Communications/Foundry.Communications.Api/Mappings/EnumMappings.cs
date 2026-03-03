using Foundry.Communications.Api.Contracts.Email.Enums;
using Foundry.Communications.Domain.Enums;

namespace Foundry.Communications.Api.Mappings;

/// <summary>Extension methods for mapping between API and Domain enums.</summary>
public static class EnumMappings
{
    public static NotificationType ToDomain(this ApiNotificationType api) => api switch
    {
        ApiNotificationType.TaskAssigned => NotificationType.TaskAssigned,
        ApiNotificationType.TaskCompleted => NotificationType.TaskCompleted,
        ApiNotificationType.BillingInvoice => NotificationType.BillingInvoice,
        ApiNotificationType.SystemNotification => NotificationType.SystemNotification,
        _ => throw new ArgumentOutOfRangeException(nameof(api), api, "Unknown notification type")
    };

    public static ApiNotificationType ToApi(this NotificationType domain) => domain switch
    {
        NotificationType.TaskAssigned => ApiNotificationType.TaskAssigned,
        NotificationType.TaskCompleted => ApiNotificationType.TaskCompleted,
        NotificationType.BillingInvoice => ApiNotificationType.BillingInvoice,
        NotificationType.SystemNotification => ApiNotificationType.SystemNotification,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown notification type")
    };
}
