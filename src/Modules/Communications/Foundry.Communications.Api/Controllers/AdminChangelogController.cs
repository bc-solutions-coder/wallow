using Asp.Versioning;
using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Communications.Api.Extensions;
using Foundry.Communications.Application.Announcements.Commands.CreateChangelogEntry;
using Foundry.Communications.Application.Announcements.Commands.PublishChangelogEntry;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Shared.Infrastructure.Services;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/changelog")]
[HasPermission(PermissionType.ChangelogManage)]
[Tags("Admin - Changelog")]
[Produces("application/json")]
public class AdminChangelogController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly IHtmlSanitizationService _sanitizer;

    public AdminChangelogController(IMessageBus bus, IHtmlSanitizationService sanitizer)
    {
        _bus = bus;
        _sanitizer = sanitizer;
    }

    /// <summary>
    /// Create a new changelog entry.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChangelogEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateChangelogEntry(
        [FromBody] CreateChangelogEntryRequest request,
        CancellationToken ct)
    {
        Result<ChangelogEntryDto> result = await _bus.InvokeAsync<Result<ChangelogEntryDto>>(
            new CreateChangelogEntryCommand(
                request.Version,
                _sanitizer.Sanitize(request.Title),
                _sanitizer.Sanitize(request.Content),
                request.ReleasedAt),
            ct);

        return result.Map(MapToResponse)
            .ToCreatedResult("/api/v1/admin/changelog");
    }

    /// <summary>
    /// Publish a changelog entry.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishChangelogEntry(Guid id, CancellationToken ct)
    {
        Result result = await _bus.InvokeAsync<Result>(new PublishChangelogEntryCommand(id), ct);
        return result.ToNoContentResult();
    }

    private static ChangelogEntryResponse MapToResponse(ChangelogEntryDto dto) => new(
        dto.Id, dto.Version, dto.Title, dto.Content, dto.ReleasedAt,
        dto.Items.Select(i => new ChangelogItemResponse(i.Id, i.Description, i.Type.ToString())).ToList());
}

public sealed record CreateChangelogEntryRequest(
    string Version,
    string Title,
    string Content,
    DateTime ReleasedAt);
