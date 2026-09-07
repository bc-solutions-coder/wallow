using Wallow.Shared.Kernel.Errors;

namespace Wallow.Notifications.Domain.Errors;

/// <summary>
/// The error catalog the Notifications module owns. Registered by <c>AddNotificationsModule</c>.
/// </summary>
public static class NotificationsErrors
{
    public static readonly ErrorCatalogEntry WebPushInvalidSubscription = new(
        "WebPush.InvalidSubscription", ErrorKind.Validation, "The browser subscription or signing key is invalid or unavailable");

    public static readonly ErrorCatalogEntry WebPushInvalidConfiguration = new(
        "WebPush.InvalidConfiguration", ErrorKind.Validation, "The Web Push key configuration is invalid; retained key identities cannot be replaced or revived");

    public static readonly ErrorCatalogEntry WebPushUnavailable = new(
        "WebPush.Unavailable", ErrorKind.Conflict, "Web Push is not configured or enabled for this organization");

    public static readonly ErrorCatalogEntry NotificationNotFound = new(
        "Notification.NotFound", ErrorKind.NotFound, "Notification not found");

    public static readonly ErrorCatalogEntry NotificationAccessDenied = new(
        "Notification.AccessDenied", ErrorKind.Forbidden, "Unauthorized access to notification");

    public static readonly ErrorCatalogEntry TenantPushConfigurationNotFound = new(
        "TenantPushConfiguration.NotFound", ErrorKind.NotFound, "Push configuration not found for this tenant and platform");

    public static readonly ErrorCatalogEntry DeviceRegistrationNotFound = new(
        "DeviceRegistration.NotFound", ErrorKind.NotFound, "Device registration not found");

    public static readonly ErrorCatalogEntry DeviceRegistrationConflict = new(
        "DeviceRegistration.Conflict", ErrorKind.Conflict, "The device token is already registered");

    public static readonly ErrorCatalogEntry SmsInvalidPhoneNumber = new(
        "Sms.InvalidPhoneNumber", ErrorKind.Validation, "The phone number is invalid");

    public static readonly ErrorCatalogEntry EmailInvalidEmailAddress = new(
        "Email.InvalidEmailAddress", ErrorKind.Validation, "The email address is invalid");
}
