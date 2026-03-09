using Asp.Versioning;
using Foundry.Communications.Api.Contracts.Announcements.Responses;
using Foundry.Shared.Api.Extensions;
using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Queries.GetChangelog;
using Foundry.Communications.Application.Announcements.Queries.GetChangelogEntry;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/changelog")]
[AllowAnonymous]
[Tags("Changelog")]
[Produces("application/json")]
public class ChangelogController(IMessageBus bus) : ControllerBase
{

    /// <summary>
    /// Get the changelog entries (most recent first).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChangelogEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangelog(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        Result<IReadOnlyList<ChangelogEntryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<ChangelogEntryDto>>>(
            new GetChangelogQuery(limit),
            ct);

        return result.Map(entries =>
            (IReadOnlyList<ChangelogEntryResponse>)entries.Select(MapToResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Get a specific changelog version.
    /// </summary>
    [HttpGet("{changelogVersion}")]
    [ProducesResponseType(typeof(ChangelogEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChangelogByVersion(string changelogVersion, CancellationToken ct)
    {
        Result<ChangelogEntryDto> result = await bus.InvokeAsync<Result<ChangelogEntryDto>>(
            new GetChangelogByVersionQuery(changelogVersion),
            ct);

        return result.Map(MapToResponse).ToActionResult();
    }

    /// <summary>
    /// Get the latest changelog entry.
    /// </summary>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(ChangelogEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestChangelog(CancellationToken ct)
    {
        Result<ChangelogEntryDto> result = await bus.InvokeAsync<Result<ChangelogEntryDto>>(
            new GetLatestChangelogQuery(),
            ct);

        return result.Map(MapToResponse).ToActionResult();
    }

    private static ChangelogEntryResponse MapToResponse(ChangelogEntryDto dto)
    {
        return new ChangelogEntryResponse(
            dto.Id,
            dto.Version,
            dto.Title,
            dto.Content,
            dto.ReleasedAt,
            dto.Items.Select(i => new ChangelogItemResponse(i.Id, i.Description, i.Type.ToString())).ToList());
    }
}
