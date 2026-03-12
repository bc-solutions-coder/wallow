namespace Foundry.Announcements.Api.Contracts.Responses;

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

public sealed record ChangelogEntryResponse(
    Guid Id,
    string Version,
    string Title,
    string Content,
    DateTime ReleasedAt,
    IReadOnlyList<ChangelogItemResponse> Items);

public sealed record ChangelogItemResponse(
    Guid Id,
    string Description,
    string Type);
