using Foundry.Notifications.Domain.Enums;

namespace Foundry.Notifications.Application.Channels.InApp.Commands.SendNotification;

public sealed record SendNotificationCommand(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message);
