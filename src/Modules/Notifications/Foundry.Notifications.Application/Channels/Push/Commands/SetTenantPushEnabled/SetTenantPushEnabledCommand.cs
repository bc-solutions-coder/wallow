using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Commands.SetTenantPushEnabled;

public sealed record SetTenantPushEnabledCommand(
    TenantId TenantId,
    PushPlatform Platform,
    bool IsEnabled);
