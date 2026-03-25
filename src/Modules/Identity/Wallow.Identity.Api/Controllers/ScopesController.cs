using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Lists available API scopes that can be assigned to service accounts.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/scopes")]
[Authorize]
[Tags("API Scopes")]
[Produces("application/json")]
public class ScopesController(IApiScopeRepository apiScopeRepository) : ControllerBase
{

    /// <summary>
    /// List available API scopes with optional category filter.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.ScopeRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ApiScopeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiScopeDto>>> List(
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<ApiScope> scopes = await apiScopeRepository.GetAllAsync(category, ct);

        List<ApiScopeDto> dtos = scopes
            .Select(s => new ApiScopeDto(
                s.Id,
                s.Code,
                s.DisplayName,
                s.Category,
                s.Description,
                s.IsDefault))
            .ToList();

        return Ok(dtos);
    }
}
