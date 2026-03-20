using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Announcements.Queries.GetAllAnnouncements;

public sealed record GetAllAnnouncementsQuery;

public sealed class GetAllAnnouncementsHandler(IAnnouncementRepository repository)
{

    public async Task<Result<IReadOnlyList<AnnouncementDto>>> Handle(GetAllAnnouncementsQuery _, CancellationToken ct)
    {
        IReadOnlyList<Announcement> announcements = await repository.GetAllAsync(ct);
        IReadOnlyList<AnnouncementDto> dtos = announcements.Select(MapToDto).ToList();
        return Result.Success(dtos);
    }

    private static AnnouncementDto MapToDto(Announcement a) => new(
        a.Id.Value, a.Title, a.Content, a.Type, a.Target, a.TargetValue,
        a.PublishAt, a.ExpiresAt, a.IsPinned, a.IsDismissible,
        a.ActionUrl, a.ActionLabel, a.ImageUrl, a.Status, a.CreatedAt);
}
