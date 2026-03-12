using Foundry.Notifications.Domain.Preferences;

namespace Foundry.Notifications.Api.Contracts.Preferences;

public sealed record SetNotificationTypeEnabledRequest(
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled);
