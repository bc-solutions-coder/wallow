using JetBrains.Annotations;
using Wallow.Notifications.Domain.Preferences;

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
