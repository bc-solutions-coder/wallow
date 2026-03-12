using Foundry.Notifications.Domain.Preferences;

namespace Foundry.Notifications.Api.Contracts.Preferences;

public sealed record SetChannelEnabledRequest(
    ChannelType ChannelType,
    bool IsEnabled);
