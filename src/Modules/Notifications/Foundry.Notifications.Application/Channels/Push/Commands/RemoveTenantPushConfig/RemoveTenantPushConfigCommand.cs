using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Commands.RemoveTenantPushConfig;

public sealed record RemoveTenantPushConfigCommand(
    TenantId TenantId,
    PushPlatform Platform);
