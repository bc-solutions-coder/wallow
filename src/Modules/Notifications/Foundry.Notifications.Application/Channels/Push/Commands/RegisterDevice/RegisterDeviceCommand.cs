using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.Push.Commands.RegisterDevice;

public sealed record RegisterDeviceCommand(
    UserId UserId,
    TenantId TenantId,
    PushPlatform Platform,
    string Token);
