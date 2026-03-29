using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Domain.Announcements.Entities;

namespace Wallow.Announcements.Application.Announcements.Mappings;

public static class AnnouncementMappings
{
    public static AnnouncementDto ToDto(this Announcement a) => new(
        a.Id.Value, a.Title, a.Content, a.Type, a.Target, a.TargetValue,
        a.PublishAt, a.ExpiresAt, a.IsPinned, a.IsDismissible,
        a.ActionUrl, a.ActionLabel, a.ImageUrl, a.Status, a.CreatedAt);
}
