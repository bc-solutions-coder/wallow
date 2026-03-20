using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Api.Contracts.Push;

public sealed record DeviceRegistrationResponse(
    Guid Id,
    Guid UserId,
    PushPlatform Platform,
    string Token,
    bool IsActive,
    DateTimeOffset RegisteredAt);
