using Foundry.Communications.Domain.Preferences;

namespace Foundry.Communications.Application.Preferences.DTOs;

public sealed record ChannelPreferenceDto(
    Guid Id,
    Guid UserId,
    ChannelType ChannelType,
    string NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
