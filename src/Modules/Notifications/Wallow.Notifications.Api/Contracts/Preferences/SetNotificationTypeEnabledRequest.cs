using Wallow.Notifications.Domain.Preferences;

namespace Wallow.Notifications.Api.Contracts.Preferences;

public sealed record SetNotificationTypeEnabledRequest(
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled);
