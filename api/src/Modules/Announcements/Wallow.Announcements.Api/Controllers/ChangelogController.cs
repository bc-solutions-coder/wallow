using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Announcements.Api.Contracts.Responses;
using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Queries.GetChangelog;
using Wallow.Announcements.Application.Changelogs.Queries.GetChangelogEntry;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Announcements.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/changelog")]
[AllowAnonymous]
[Tags("Changelog")]
[Produces("application/json")]
public class ChangelogController(IMessageBus bus) : ControllerBase
{

    /// <summary>
    /// List published changelog entries.
    /// </summary>
    /// <remarks>
    /// Available without authentication. Returns global entries shared across tenants, ordered by release date
    /// descending, up to the requested limit. Unpublished entries are excluded.
    /// </remarks>
    /// <param name="limit">Maximum number of entries to return; defaults to 50.</param>
    /// <param name="ct">Cancels the request.</param>
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
    /// Get a published changelog version.
    /// </summary>
    /// <remarks>
    /// Available without authentication and independent of the current tenant. Returns the published global
    /// entry with the exact version string, including its change items. Returns 404 when the version is unknown
    /// or unpublished.
    /// </remarks>
    /// <param name="changelogVersion">Exact version string recorded on the entry, including any prerelease or build suffix.</param>
    /// <param name="ct">Cancels the request.</param>
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
    /// Get the latest published changelog entry.
    /// </summary>
    /// <remarks>
    /// Available without authentication. Returns the global published entry with the greatest release date,
    /// including its change items. Returns 404 when no published entry exists.
    /// </remarks>
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
