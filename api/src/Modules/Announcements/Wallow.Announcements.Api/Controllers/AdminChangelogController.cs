using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Announcements.Api.Contracts.Responses;
using Wallow.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;
using Wallow.Announcements.Application.Changelogs.Commands.PublishChangelogEntry;
using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Infrastructure.Core.Services;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Announcements.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/admin/changelog")]
[HasPermission(PermissionType.ChangelogManage)]
[Tags("Admin - Changelog")]
[Produces("application/json")]
public class AdminChangelogController(IMessageBus bus, IHtmlSanitizationService sanitizer) : ControllerBase
{

    [HttpPost]
    [ProducesResponseType(typeof(ChangelogEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateChangelogEntry(
        [FromBody] CreateChangelogEntryRequest request,
        CancellationToken ct)
    {
        Result<ChangelogEntryDto> result = await bus.InvokeAsync<Result<ChangelogEntryDto>>(
            new CreateChangelogEntryCommand(
                request.Version,
                sanitizer.Sanitize(request.Title),
                sanitizer.Sanitize(request.Content),
                request.ReleasedAt),
            ct);

        return result.Map(MapToResponse)
            .ToCreatedResult("/v1/admin/changelog");
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishChangelogEntry(Guid id, CancellationToken ct)
    {
        Result result = await bus.InvokeAsync<Result>(new PublishChangelogEntryCommand(id), ct);
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
