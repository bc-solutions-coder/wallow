using Wallow.Announcements.Domain.Changelogs.Enums;

namespace Wallow.Announcements.Application.Changelogs.DTOs;

public sealed record ChangelogEntryDto(
    Guid Id,
    string Version,
    string Title,
    string Content,
    DateTime ReleasedAt,
    bool IsPublished,
    IReadOnlyList<ChangelogItemDto> Items,
    DateTime CreatedAt);

public sealed record ChangelogItemDto(
    Guid Id,
    string Description,
    ChangeType Type);
