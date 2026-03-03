using Foundry.Communications.Domain.Enums;

namespace Foundry.Communications.Application.Channels.Email.DTOs;

public sealed record EmailPreferenceDto(
    Guid Id,
    Guid UserId,
    NotificationType NotificationType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
