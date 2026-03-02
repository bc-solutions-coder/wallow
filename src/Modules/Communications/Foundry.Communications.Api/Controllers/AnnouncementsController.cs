using Asp.Versioning;
using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Extensions;
using Foundry.Communications.Application.Announcements.Commands.DismissAnnouncement;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Queries.GetActiveAnnouncements;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/announcements")]
[Authorize]
[Tags("Announcements")]
[Produces("application/json")]
public class AnnouncementsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public AnnouncementsController(IMessageBus bus, ITenantContext tenantContext, ICurrentUserService currentUserService)
    {
        _bus = bus;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get active announcements for the current user.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.AnnouncementRead)]
    [ProducesResponseType(typeof(IReadOnlyList<AnnouncementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAnnouncements(CancellationToken ct)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        IReadOnlyList<string> roles = GetUserRoles();
        string? planName = GetUserPlan();

        Result<IReadOnlyList<AnnouncementDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            new GetActiveAnnouncementsQuery(
                userId.Value,
                _tenantContext.TenantId.Value,
                planName,
                roles),
            ct);

        return result.Map(dtos =>
            (IReadOnlyList<AnnouncementResponse>)dtos.Select(MapToResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Dismiss an announcement so it no longer appears for the current user.
    /// </summary>
    [HttpPost("{id:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DismissAnnouncement(Guid id, CancellationToken ct)
    {
        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        Result result = await _bus.InvokeAsync<Result>(
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
