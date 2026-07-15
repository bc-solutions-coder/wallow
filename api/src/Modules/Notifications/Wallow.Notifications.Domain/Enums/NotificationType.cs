namespace Wallow.Notifications.Domain.Enums;

public enum NotificationType
{
    TaskAssigned = 0,
    TaskCompleted = 1,
    TaskComment = 2,
    SystemAlert = 3,
    // 4 was BillingInvoice (removed)
    Mention = 5,
    Announcement = 6,
    SystemNotification = 7,
    InquirySubmitted = 8,
    InquiryComment = 9
}
