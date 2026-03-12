namespace Foundry.Notifications.Api.Contracts.Push;

public sealed record SendPushRequest(
    Guid RecipientId,
    string Title,
    string Body,
    string NotificationType);
