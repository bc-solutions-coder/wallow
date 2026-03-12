using Foundry.Notifications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.InApp.Commands.ArchiveNotification;

public sealed record ArchiveNotificationCommand(NotificationId NotificationId, TenantId TenantId, Guid UserId);
