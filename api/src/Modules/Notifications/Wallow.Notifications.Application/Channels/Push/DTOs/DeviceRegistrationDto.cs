using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Application.Channels.Push.DTOs;

public sealed record DeviceRegistrationDto(
    Guid Id,
    Guid UserId,
    PushPlatform Platform,
    string Token,
    bool IsActive,
    DateTimeOffset RegisteredAt);
