using Wallow.Notifications.Domain.Enums;

namespace Wallow.Notifications.Application.Channels.Email.DTOs;

public sealed record EmailPreferenceDto(
    Guid Id,
    Guid UserId,
    NotificationType NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
