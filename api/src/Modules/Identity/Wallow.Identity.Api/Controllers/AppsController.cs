using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// What the consent screen needs to describe a client to the person being asked. Registering
/// and managing applications happens on the organization-scoped client surface
/// (<see cref="OrganizationClientsController"/>), not here.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/apps")]
[Authorize]
[Tags("Apps")]
[Produces("application/json")]
public class AppsController(IDeveloperAppService developerAppService) : ControllerBase
{
    [HttpGet("consent-info/{clientId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConsentInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsentInfoResponse>> GetConsentInfo(
        string clientId,
        [FromQuery] string? scopes,
        CancellationToken ct)
    {
        IReadOnlyList<string> scopeList = string.IsNullOrWhiteSpace(scopes)
            ? []
            : scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        ConsentInfoDto? dto = await developerAppService.GetConsentInfoAsync(clientId, scopeList, ct);
        if (dto is null)
        {
            return NotFound();
        }

        ConsentInfoResponse response = new(
            dto.ClientId,
            dto.DisplayName,
            dto.LogoUrl,
            dto.RequestedScopes.Select(s => new ScopeInfo(s.Name, s.Description)).ToList());

        return Ok(response);
    }
}
