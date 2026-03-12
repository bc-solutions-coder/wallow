using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;

public sealed record UpsertTenantPushConfigCommand(
    TenantId TenantId,
    PushPlatform Platform,
    string RawCredentials);
