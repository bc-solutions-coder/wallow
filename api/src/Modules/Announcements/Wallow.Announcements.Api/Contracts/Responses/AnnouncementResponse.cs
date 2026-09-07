namespace Wallow.Announcements.Api.Contracts.Responses;

public sealed record AnnouncementResponse(
    Guid Id,
    string Title,
    string Content,
    string Type,
    bool IsPinned,
    bool IsDismissible,
    string? ActionUrl,
    string? ActionLabel,
    string? ImageUrl,
    DateTime CreatedAt);
