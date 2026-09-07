using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Caller-scoped identity reads that do not require an organization context.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/me")]
[Authorize]
[AllowWithoutOrganization]
[Tags("Me")]
[Produces("application/json")]
public class MeController(IOrganizationService orgService) : ControllerBase
{
    /// <summary>
    /// Lists the caller active organization memberships without requiring a management permission.
    /// </summary>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(IReadOnlyList<MyOrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyOrganizationDto>>> GetOrganizations(CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        return Ok(await orgService.GetMyOrganizationsAsync(userId, ct));
    }
}
