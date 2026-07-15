using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Announcements.Api.Contracts.Responses;
using Wallow.Announcements.Application.Announcements.Commands.ArchiveAnnouncement;
using Wallow.Announcements.Application.Announcements.Commands.CreateAnnouncement;
using Wallow.Announcements.Application.Announcements.Commands.PublishAnnouncement;
using Wallow.Announcements.Application.Announcements.Commands.UpdateAnnouncement;
using Wallow.Announcements.Application.Announcements.DTOs;
using Wallow.Announcements.Application.Announcements.Queries.GetAllAnnouncements;
using Wallow.Announcements.Domain.Announcements.Enums;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Infrastructure.Core.Services;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Announcements.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/admin/announcements")]
[HasPermission(PermissionType.AnnouncementManage)]
[Tags("Admin - Announcements")]
[Produces("application/json")]
public class AdminAnnouncementsController(IMessageBus bus, IHtmlSanitizationService sanitizer) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AnnouncementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAnnouncements(CancellationToken ct)
    {
        Result<IReadOnlyList<AnnouncementDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            new GetAllAnnouncementsQuery(), ct);

        return result.Map(dtos =>
            (IReadOnlyList<AnnouncementResponse>)dtos.Select(MapToResponse).ToList())
            .ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(AnnouncementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAnnouncement(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken ct)
    {
        Result<AnnouncementDto> result = await bus.InvokeAsync<Result<AnnouncementDto>>(
            new CreateAnnouncementCommand(
                sanitizer.Sanitize(request.Title),
                sanitizer.Sanitize(request.Content),
                request.Type,
                request.Target,
                request.TargetValue,
                request.PublishAt,
                request.ExpiresAt,
                request.IsPinned,
                request.IsDismissible,
                request.ActionUrl,
                request.ActionLabel,
                request.ImageUrl),
            ct);

        return result.Map(MapToResponse)
            .ToCreatedResult("/v1/admin/announcements");
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AnnouncementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAnnouncement(
        Guid id,
        [FromBody] UpdateAnnouncementRequest request,
        CancellationToken ct)
    {
        Result<AnnouncementDto> result = await bus.InvokeAsync<Result<AnnouncementDto>>(
            new UpdateAnnouncementCommand(
                id,
                sanitizer.Sanitize(request.Title),
                sanitizer.Sanitize(request.Content),
                request.Type,
                request.Target,
                request.TargetValue,
                request.PublishAt,
                request.ExpiresAt,
                request.IsPinned,
                request.IsDismissible,
                request.ActionUrl,
                request.ActionLabel,
                request.ImageUrl),
            ct);

        return result.Map(MapToResponse).ToActionResult();
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishAnnouncement(Guid id, CancellationToken ct)
    {
        Result result = await bus.InvokeAsync<Result>(new PublishAnnouncementCommand(id), ct);
        return result.ToNoContentResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveAnnouncement(Guid id, CancellationToken ct)
    {
        Result result = await bus.InvokeAsync<Result>(new ArchiveAnnouncementCommand(id), ct);
        return result.ToNoContentResult();
    }

    private static AnnouncementResponse MapToResponse(AnnouncementDto dto) => new(
        dto.Id, dto.Title, dto.Content, dto.Type.ToString(),
        dto.IsPinned, dto.IsDismissible, dto.ActionUrl, dto.ActionLabel, dto.ImageUrl, dto.CreatedAt);
}

public sealed record CreateAnnouncementRequest(
    string Title,
    string Content,
    AnnouncementType Type,
    AnnouncementTarget Target = AnnouncementTarget.All,
    string? TargetValue = null,
    DateTime? PublishAt = null,
    DateTime? ExpiresAt = null,
    bool IsPinned = false,
    bool IsDismissible = true,
    string? ActionUrl = null,
    string? ActionLabel = null,
    string? ImageUrl = null);

public sealed record UpdateAnnouncementRequest(
    string Title,
    string Content,
    AnnouncementType Type,
    AnnouncementTarget Target,
    string? TargetValue,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    bool IsPinned,
    bool IsDismissible,
    string? ActionUrl,
    string? ActionLabel,
    string? ImageUrl);
