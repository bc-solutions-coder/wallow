using Foundry.Notifications.Domain.Channels.Push.Enums;

namespace Foundry.Notifications.Api.Contracts.Push;

public sealed record TenantPushConfigResponse(
    Guid Id,
    Guid TenantId,
    PushPlatform Platform,
    string Credentials,
    bool IsEnabled);
