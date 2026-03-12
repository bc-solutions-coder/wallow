using Foundry.Notifications.Domain.Channels.Push.Enums;

namespace Foundry.Notifications.Api.Contracts.Push;

public sealed record RegisterDeviceRequest(
    PushPlatform Platform,
    string Token);
