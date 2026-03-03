using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Application.Channels.InApp.Commands.ArchiveNotification;

public sealed record ArchiveNotificationCommand(NotificationId NotificationId, TenantId TenantId, Guid UserId);
