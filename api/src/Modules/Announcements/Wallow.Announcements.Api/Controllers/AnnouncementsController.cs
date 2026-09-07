using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Announcements.Api.Contracts.Responses;
using Wallow.Announcements.Application.Announcements.Commands.DismissAnnouncement;
using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Queries.GetActiveAnnouncements;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Announcements.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/announcements")]
[Authorize]
[Tags("Announcements")]
[Produces("application/json")]
public class AnnouncementsController(IMessageBus bus, ITenantContext tenantContext, ICurrentUserService currentUserService) : ControllerBase
{

    /// <summary>
    /// List active announcements for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user with AnnouncementRead in the current tenant. Returns published
    /// announcements within their publication and expiry dates that match the user's tenant or roles, excluding
    /// dismissible announcements already dismissed by that user. Pinned announcements appear first, then newest
    /// first within each group.
    /// </remarks>
    [HttpGet]
    [HasPermission(PermissionType.AnnouncementRead)]
    [ProducesResponseType(typeof(IReadOnlyList<AnnouncementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAnnouncements(CancellationToken ct)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        IReadOnlyList<string> roles = GetUserRoles();

        Result<IReadOnlyList<AnnouncementDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            new GetActiveAnnouncementsQuery(
                userId.Value,
                tenantContext.TenantId.Value,
                roles),
            ct);

        return result.Map(dtos =>
            (IReadOnlyList<AnnouncementResponse>)dtos.Select(MapToResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Dismiss an announcement for the current user.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user with AnnouncementRead in the current tenant. Hides a dismissible
    /// announcement from this user's active list; repeating the dismissal succeeds without creating another
    /// dismissal. Returns 404 for an unknown announcement and rejects announcements that are not dismissible.
    /// </remarks>
    [HttpPost("{id:guid}/dismiss")]
    [HasPermission(PermissionType.AnnouncementRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DismissAnnouncement(Guid id, CancellationToken ct)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        Result result = await bus.InvokeAsync<Result>(
            new DismissAnnouncementCommand(id, userId.Value),
            ct);

        return result.ToNoContentResult();
    }

    private List<string> GetUserRoles()
    {
        return User.GetRoles().ToList();
    }

    private static AnnouncementResponse MapToResponse(AnnouncementDto dto)
    {
        return new AnnouncementResponse(
            dto.Id,
            dto.Title,
            dto.Content,
            dto.Type.ToString(),
            dto.IsPinned,
            dto.IsDismissible,
            dto.ActionUrl,
            dto.ActionLabel,
            dto.ImageUrl,
            dto.CreatedAt);
    }
}
