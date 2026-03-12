using Foundry.Notifications.Domain.Channels.Push.Enums;

namespace Foundry.Notifications.Application.Channels.Push.DTOs;

public sealed record DeviceRegistrationDto(
    Guid Id,
    Guid UserId,
    PushPlatform Platform,
    string Token,
    bool IsActive,
    DateTimeOffset RegisteredAt);
