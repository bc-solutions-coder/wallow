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
/// What the caller can be told about themselves, independent of the organization their token
/// is scoped to.
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
    /// The organizations the caller belongs to.
    /// </summary>
    /// <remarks>
    /// This is the organization picker's data: a first-party app lists these and re-authorizes
    /// with the <c>organization</c> hint to switch context. The token it holds still opens one
    /// organization's door at a time, so the switch is a new authorize round-trip, never a
    /// header. Reachable without an organization, because a caller who belongs to three and
    /// has picked none must be able to see them.
    ///
    /// Asks for no permission — the answer is about the caller, and demanding one would hide
    /// every organization but the one their token is scoped to, which is the question.
    /// </remarks>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(IReadOnlyList<MyOrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyOrganizationDto>>> GetOrganizations(CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        return Ok(await orgService.GetMyOrganizationsAsync(userId, ct));
    }
}
