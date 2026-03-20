using Wallow.Notifications.Domain.Channels.Email.Enums;

namespace Wallow.Notifications.Application.Channels.Email.DTOs;

public sealed record EmailDto(
    Guid Id,
    string To,
    string? From,
    string Subject,
    string Body,
    EmailStatus Status,
    DateTime? SentAt,
    string? FailureReason,
    int RetryCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
