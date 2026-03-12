using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Commands.SendPush;

public sealed record SendPushCommand(
    UserId RecipientId,
    TenantId TenantId,
    string Title,
    string Body,
    string NotificationType);
