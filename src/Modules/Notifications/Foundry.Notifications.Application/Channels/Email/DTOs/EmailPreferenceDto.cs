using Foundry.Notifications.Domain.Enums;

namespace Foundry.Notifications.Application.Channels.Email.DTOs;

public sealed record EmailPreferenceDto(
    Guid Id,
    Guid UserId,
    NotificationType NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
