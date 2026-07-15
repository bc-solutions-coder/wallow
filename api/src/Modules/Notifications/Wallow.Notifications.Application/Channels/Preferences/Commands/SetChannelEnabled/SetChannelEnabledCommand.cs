using Wallow.Notifications.Domain.Preferences;

namespace Wallow.Notifications.Application.Channels.Preferences.Commands.SetChannelEnabled;

public sealed record SetChannelEnabledCommand(
    Guid UserId,
    ChannelType ChannelType,
    bool IsEnabled,
    string NotificationType = "*");
