using Wallow.Notifications.Domain.Preferences;

namespace Wallow.Notifications.Api.Contracts.Preferences;

public sealed record SetChannelEnabledRequest(
    ChannelType ChannelType,
    bool IsEnabled);
