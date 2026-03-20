using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.SendPush;

public sealed record SendPushCommand(
    UserId RecipientId,
    TenantId TenantId,
    string Title,
    string Body,
    string NotificationType);
