using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Api.Contracts.Push;

public sealed record TenantPushConfigResponse(
    Guid Id,
    Guid TenantId,
    PushPlatform Platform,
    string Credentials,
    bool IsEnabled);
