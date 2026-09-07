namespace Wallow.Notifications.Api.Contracts.Push;

public sealed record SendPushRequest(
    string Title,
    string Body,
    string NotificationType);
