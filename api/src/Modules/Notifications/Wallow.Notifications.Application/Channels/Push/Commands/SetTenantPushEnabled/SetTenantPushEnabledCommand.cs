using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.SetTenantPushEnabled;

public sealed record SetTenantPushEnabledCommand(
    TenantId TenantId,
    PushPlatform Platform,
    bool IsEnabled);
