using Asp.Versioning;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Api.Controllers;

/// <summary>
/// Lists available API scopes that can be assigned to service accounts.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/identity/scopes")]
[Authorize]
[Tags("API Scopes")]
[Produces("application/json")]
public class ScopesController : ControllerBase
{
    private readonly IApiScopeRepository _apiScopeRepository;

    public ScopesController(IApiScopeRepository apiScopeRepository)
    {
        _apiScopeRepository = apiScopeRepository;
    }

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
        IReadOnlyList<ApiScope> scopes = await _apiScopeRepository.GetAllAsync(category, ct);

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
