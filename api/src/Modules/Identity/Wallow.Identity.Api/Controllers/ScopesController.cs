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
/// Lists the API scope catalog, including platform-only scope metadata.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/scopes")]
[Authorize]
[Tags("API Scopes")]
[Produces("application/json")]
public class ScopesController(IApiScopeRepository apiScopeRepository) : ControllerBase
{

    /// <summary>
    /// List API scopes.
    /// </summary>
    /// <remarks>
    /// Requires ScopeRead in the resolved tenant. Returns the global scope catalog ordered by category and code,
    /// optionally filtered by an exact category. Includes platform-only scopes as metadata; their presence does not
    /// permit granting them to organization clients.
    /// </remarks>
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
                s.IsDefault,
                s.PlatformOnly))
            .ToList();

        return Ok(dtos);
    }
}
