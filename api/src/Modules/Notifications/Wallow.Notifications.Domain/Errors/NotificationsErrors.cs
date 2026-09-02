using Wallow.Shared.Kernel.Errors;

namespace Wallow.Notifications.Domain.Errors;

/// <summary>
/// The error catalog the Notifications module owns. Registered by <c>AddNotificationsModule</c>.
/// </summary>
public static class NotificationsErrors
{
    public static readonly ErrorCatalogEntry NotificationNotFound = new(
        "Notification.NotFound", ErrorKind.NotFound, "Notification not found");

    public static readonly ErrorCatalogEntry NotificationAccessDenied = new(
        "Notification.AccessDenied", ErrorKind.Forbidden, "Unauthorized access to notification");

    public static readonly ErrorCatalogEntry TenantPushConfigurationNotFound = new(
        "TenantPushConfiguration.NotFound", ErrorKind.NotFound, "Push configuration not found for this tenant and platform");

    public static readonly ErrorCatalogEntry DeviceRegistrationNotFound = new(
        "DeviceRegistration.NotFound", ErrorKind.NotFound, "Device registration not found");

    public static readonly ErrorCatalogEntry SmsInvalidPhoneNumber = new(
        "Sms.InvalidPhoneNumber", ErrorKind.Validation, "The phone number is invalid");

    public static readonly ErrorCatalogEntry EmailInvalidEmailAddress = new(
        "Email.InvalidEmailAddress", ErrorKind.Validation, "The email address is invalid");
}
