using Foundry.Notifications.Domain.Channels.Push.Enums;

namespace Foundry.Notifications.Api.Contracts.Push;

public sealed record DeviceRegistrationResponse(
    Guid Id,
    Guid UserId,
    PushPlatform Platform,
    string Token,
    bool IsActive,
    DateTimeOffset RegisteredAt);
