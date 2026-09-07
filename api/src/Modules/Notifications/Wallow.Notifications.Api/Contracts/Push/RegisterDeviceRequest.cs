using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Api.Contracts.Push;

public sealed record RegisterDeviceRequest(
    PushPlatform Platform,
    string? Token = null,
    WebPushSubscription? Subscription = null,
    string? SigningKeyId = null);
