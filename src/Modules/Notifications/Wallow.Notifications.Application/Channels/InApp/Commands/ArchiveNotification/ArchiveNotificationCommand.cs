using Wallow.Notifications.Domain.Channels.InApp.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.InApp.Commands.ArchiveNotification;

public sealed record ArchiveNotificationCommand(NotificationId NotificationId, TenantId TenantId, Guid UserId);
