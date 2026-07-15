using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;

public sealed record UpsertTenantPushConfigCommand(
    TenantId TenantId,
    PushPlatform Platform,
    string RawCredentials);
