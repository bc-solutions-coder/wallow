using Wallow.Notifications.Domain.Channels.InApp.Identity;

namespace Wallow.Notifications.Application.Channels.InApp.Commands.ArchiveNotification;

public sealed record ArchiveNotificationCommand(NotificationId NotificationId, Guid UserId);
