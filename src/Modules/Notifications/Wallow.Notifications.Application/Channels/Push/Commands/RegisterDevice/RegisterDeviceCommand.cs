using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Push.Commands.RegisterDevice;

public sealed record RegisterDeviceCommand(
    UserId UserId,
    TenantId TenantId,
    PushPlatform Platform,
    string Token);
