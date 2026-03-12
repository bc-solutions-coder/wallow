using Foundry.Notifications.Domain.Preferences;
using JetBrains.Annotations;

namespace Foundry.Notifications.Application.Preferences.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ChannelPreferenceDto(
    Guid Id,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
