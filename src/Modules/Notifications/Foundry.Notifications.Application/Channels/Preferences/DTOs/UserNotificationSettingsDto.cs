using Foundry.Notifications.Application.Preferences.DTOs;
using Foundry.Notifications.Domain.Preferences;
using JetBrains.Annotations;

namespace Foundry.Notifications.Application.Channels.Preferences.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UserNotificationSettingsDto(
    Guid UserId,
    IReadOnlyList<ChannelSettingDto> ChannelSettings);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ChannelSettingDto(
    ChannelType ChannelType,
    bool IsGloballyEnabled,
    IReadOnlyList<ChannelPreferenceDto> TypePreferences);
