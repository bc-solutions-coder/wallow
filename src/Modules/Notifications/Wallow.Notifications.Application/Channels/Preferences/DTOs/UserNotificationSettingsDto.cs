using Wallow.Notifications.Application.Preferences.DTOs;
using Wallow.Notifications.Domain.Preferences;
using JetBrains.Annotations;

namespace Wallow.Notifications.Application.Channels.Preferences.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UserNotificationSettingsDto(
    Guid UserId,
    IReadOnlyList<ChannelSettingDto> ChannelSettings);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ChannelSettingDto(
    ChannelType ChannelType,
    bool IsGloballyEnabled,
    IReadOnlyList<ChannelPreferenceDto> TypePreferences);
