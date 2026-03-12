using Asp.Versioning;
using Foundry.Announcements.Api.Contracts.Responses;
using Foundry.Announcements.Application.Announcements.Commands.DismissAnnouncement;
using Foundry.Announcements.Application.Announcements.DTOs;
using Foundry.Announcements.Application.Announcements.Queries.GetActiveAnnouncements;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Announcements.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/announcements")]
[Authorize]
[Tags("Announcements")]
[Produces("application/json")]
public class AnnouncementsController(IMessageBus bus, ITenantContext tenantContext, ICurrentUserService currentUserService) : ControllerBase
{

    [HttpGet]
    [HasPermission(PermissionType.AnnouncementRead)]
    [ProducesResponseType(typeof(IReadOnlyList<AnnouncementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAnnouncements(CancellationToken ct)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        IReadOnlyList<string> roles = GetUserRoles();
        string? planName = GetUserPlan();

        Result<IReadOnlyList<AnnouncementDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            new GetActiveAnnouncementsQuery(
                userId.Value,
                tenantContext.TenantId.Value,
                planName,
                roles),
            ct);

        return result.Map(dtos =>
            (IReadOnlyList<AnnouncementResponse>)dtos.Select(MapToResponse).ToList())
            .ToActionResult();
    }

    [HttpPost("{id:guid}/dismiss")]
    [HasPermission(PermissionType.AnnouncementRead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DismissAnnouncement(Guid id, CancellationToken ct)
    {
        Guid? userId = currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Result result = await bus.InvokeAsync<Result>(
            new DismissAnnouncementCommand(id, userId.Value),
            ct);

        return result.ToNoContentResult();
    }

    private List<string> GetUserRoles()
    {
        return User.FindAll(System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
    }

    private string? GetUserPlan()
    {
        return User.FindFirst("plan")?.Value;
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
