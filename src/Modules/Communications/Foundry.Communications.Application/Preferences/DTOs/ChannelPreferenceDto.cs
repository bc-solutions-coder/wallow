using Foundry.Communications.Domain.Preferences;
using JetBrains.Annotations;

namespace Foundry.Communications.Application.Preferences.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ChannelPreferenceDto(
    Guid Id,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
