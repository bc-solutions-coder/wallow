using Asp.Versioning;
using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Shared.Api.Extensions;
using Foundry.Communications.Application.Announcements.Commands.CreateChangelogEntry;
using Foundry.Communications.Application.Announcements.Commands.PublishChangelogEntry;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Shared.Infrastructure.Core.Services;
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
public class AdminChangelogController(IMessageBus bus, IHtmlSanitizationService sanitizer) : ControllerBase
{

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
        Result<ChangelogEntryDto> result = await bus.InvokeAsync<Result<ChangelogEntryDto>>(
            new CreateChangelogEntryCommand(
                request.Version,
                sanitizer.Sanitize(request.Title),
                sanitizer.Sanitize(request.Content),
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
