using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Services;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Announcements.Queries.GetActiveAnnouncements;

public sealed record GetActiveAnnouncementsQuery(
    Guid UserId,
    Guid TenantId,
    string? PlanName,
    IReadOnlyList<string> Roles);

public sealed class GetActiveAnnouncementsHandler(IAnnouncementTargetingService targetingService)
{
    public async Task<Result<IReadOnlyList<AnnouncementDto>>> Handle(
        GetActiveAnnouncementsQuery query,
        CancellationToken ct)
    {
        UserContext userContext = new(
            UserId.Create(query.UserId),
            TenantId.Create(query.TenantId),
            query.PlanName,
            query.Roles);

        IReadOnlyList<AnnouncementDto> announcements = await targetingService.GetActiveAnnouncementsForUserAsync(userContext, ct);
        return Result.Success(announcements);
    }
}
