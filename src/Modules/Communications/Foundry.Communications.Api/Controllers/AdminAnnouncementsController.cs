using Asp.Versioning;
using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Extensions;
using Foundry.Communications.Application.Announcements.Commands.ArchiveAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.CreateAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.PublishAnnouncement;
using Foundry.Communications.Application.Announcements.Commands.UpdateAnnouncement;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Queries.GetAllAnnouncements;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Shared.Infrastructure.Services;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/announcements")]
[HasPermission(PermissionType.AnnouncementManage)]
[Tags("Admin - Announcements")]
[Produces("application/json")]
public class AdminAnnouncementsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly IHtmlSanitizationService _sanitizer;

    public AdminAnnouncementsController(IMessageBus bus, IHtmlSanitizationService sanitizer)
    {
        _bus = bus;
        _sanitizer = sanitizer;
    }

    /// <summary>
    /// Get all announcements (admin view).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AnnouncementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAnnouncements(CancellationToken ct)
    {
        Result<IReadOnlyList<AnnouncementDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<AnnouncementDto>>>(
            new GetAllAnnouncementsQuery(), ct);

        return result.Map(dtos =>
            (IReadOnlyList<AnnouncementResponse>)dtos.Select(MapToResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Create a new announcement.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AnnouncementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAnnouncement(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken ct)
    {
        Result<AnnouncementDto> result = await _bus.InvokeAsync<Result<AnnouncementDto>>(
            new CreateAnnouncementCommand(
                _sanitizer.Sanitize(request.Title),
                _sanitizer.Sanitize(request.Content),
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
            .ToCreatedResult("/api/v1/admin/announcements");
    }

    /// <summary>
    /// Update an announcement.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AnnouncementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAnnouncement(
        Guid id,
        [FromBody] UpdateAnnouncementRequest request,
        CancellationToken ct)
    {
        Result<AnnouncementDto> result = await _bus.InvokeAsync<Result<AnnouncementDto>>(
            new UpdateAnnouncementCommand(
                id,
                _sanitizer.Sanitize(request.Title),
                _sanitizer.Sanitize(request.Content),
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

    /// <summary>
    /// Publish an announcement immediately.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishAnnouncement(Guid id, CancellationToken ct)
    {
        Result result = await _bus.InvokeAsync<Result>(new PublishAnnouncementCommand(id), ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Archive (delete) an announcement.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveAnnouncement(Guid id, CancellationToken ct)
    {
        Result result = await _bus.InvokeAsync<Result>(new ArchiveAnnouncementCommand(id), ct);
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
