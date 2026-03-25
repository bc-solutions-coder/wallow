using Wallow.Notifications.Domain.Enums;

namespace Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;

public sealed record SendNotificationCommand(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    string? ActionUrl = null,
    string? SourceModule = null);
