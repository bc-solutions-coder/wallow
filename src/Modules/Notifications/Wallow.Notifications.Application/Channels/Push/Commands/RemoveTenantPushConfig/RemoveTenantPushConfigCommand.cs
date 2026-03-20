using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.RemoveTenantPushConfig;

public sealed record RemoveTenantPushConfigCommand(
    TenantId TenantId,
    PushPlatform Platform);
