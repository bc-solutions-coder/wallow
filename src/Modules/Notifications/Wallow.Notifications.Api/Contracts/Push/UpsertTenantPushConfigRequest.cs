using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Api.Contracts.Push;

public sealed record UpsertTenantPushConfigRequest(
    PushPlatform Platform,
    string Credentials);
