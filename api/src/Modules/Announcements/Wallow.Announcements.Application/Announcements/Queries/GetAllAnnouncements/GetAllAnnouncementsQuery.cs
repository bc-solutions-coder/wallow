using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Application.Announcements.Mappings;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Announcements.Queries.GetAllAnnouncements;

public sealed record GetAllAnnouncementsQuery;

public sealed class GetAllAnnouncementsHandler(IAnnouncementRepository repository)
{

    public async Task<Result<IReadOnlyList<AnnouncementDto>>> Handle(GetAllAnnouncementsQuery _, CancellationToken ct)
    {
        IReadOnlyList<Announcement> announcements = await repository.GetAllAsync(ct);
        IReadOnlyList<AnnouncementDto> dtos = announcements.Select(a => a.ToDto()).ToList();
        return Result.Success(dtos);
    }
}
