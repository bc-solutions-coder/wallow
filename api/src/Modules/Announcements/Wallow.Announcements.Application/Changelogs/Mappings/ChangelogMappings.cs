using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Domain.Changelogs.Entities;

namespace Wallow.Announcements.Application.Changelogs.Mappings;

public static class ChangelogMappings
{
    public static ChangelogEntryDto ToDto(this ChangelogEntry e) => new(
        e.Id.Value, e.Version, e.Title, e.Content, e.ReleasedAt, e.IsPublished,
        e.Items.Select(i => new ChangelogItemDto(i.Id.Value, i.Description, i.Type)).ToList(),
        e.CreatedAt);
}
