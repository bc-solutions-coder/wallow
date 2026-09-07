namespace Wallow.Notifications.Domain.Enums;

public enum NotificationType
{
    TaskAssigned = 0,
    TaskCompleted = 1,
    TaskComment = 2,
    SystemAlert = 3,
    // Value 4 is reserved for persisted BillingInvoice records.
    Mention = 5,
    Announcement = 6,
    SystemNotification = 7,
    InquirySubmitted = 8,
    InquiryComment = 9
}
