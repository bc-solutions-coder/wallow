using Wallow.Notifications.Domain.Preferences;
using JetBrains.Annotations;

namespace Wallow.Notifications.Application.Preferences.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ChannelPreferenceDto(
    Guid Id,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
