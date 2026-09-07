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

    /// <summary>
    /// Create an unpublished changelog entry.
    /// </summary>
    /// <remarks>
    /// Requires ChangelogManage. Changelog entries are global and shared across tenants. Accepts a semantic
    /// version, release date, title, and content, sanitizes the title and content, and returns the unpublished
    /// entry. Publish the entry separately to make it visible anonymously.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ChangelogEntryResponse), StatusCodes.Status201Created)]
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

    /// <summary>
    /// Publish a changelog entry.
    /// </summary>
    /// <remarks>
    /// Requires ChangelogManage and makes the global entry visible through the anonymous changelog endpoints.
    /// Preserves the supplied release date, which determines list order and the latest entry. Returns no
    /// content, including for an already published entry, or 404 if the ID is unknown.
    /// </remarks>
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
