using Asp.Versioning;
using Foundry.Announcements.Api.Contracts.Responses;
using Foundry.Announcements.Application.Changelogs.DTOs;
using Foundry.Announcements.Application.Changelogs.Queries.GetChangelog;
using Foundry.Announcements.Application.Changelogs.Queries.GetChangelogEntry;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Announcements.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/changelog")]
[AllowAnonymous]
[Tags("Changelog")]
[Produces("application/json")]
public class ChangelogController(IMessageBus bus) : ControllerBase
{

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
