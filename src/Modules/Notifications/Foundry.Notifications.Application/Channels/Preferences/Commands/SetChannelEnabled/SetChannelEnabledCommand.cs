using Foundry.Notifications.Domain.Preferences;

namespace Foundry.Notifications.Application.Channels.Preferences.Commands.SetChannelEnabled;

public sealed record SetChannelEnabledCommand(
    Guid UserId,
    ChannelType ChannelType,
    bool IsEnabled,
    string NotificationType = "*");
