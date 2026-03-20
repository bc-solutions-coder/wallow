using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Application.Channels.Push.DTOs;

public sealed record TenantPushConfigDto(
    Guid Id,
    Guid TenantId,
    PushPlatform Platform,
    string EncryptedCredentials,
    bool IsEnabled);
