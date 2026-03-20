using Wallow.Announcements.Domain.Announcements.Enums;

namespace Wallow.Announcements.Application.Announcements.DTOs;

public sealed record AnnouncementDto(
    Guid Id,
    string Title,
    string Content,
    AnnouncementType Type,
    AnnouncementTarget Target,
    string? TargetValue,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    bool IsPinned,
    bool IsDismissible,
    string? ActionUrl,
    string? ActionLabel,
    string? ImageUrl,
    AnnouncementStatus Status,
    DateTime CreatedAt);
