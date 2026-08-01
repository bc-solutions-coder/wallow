using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// What the caller can be told about themselves, independent of the organization their token
/// is scoped to.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/me")]
[Authorize]
[Tags("Me")]
[Produces("application/json")]
public class MeController(IOrganizationService orgService) : ControllerBase
{
    /// <summary>
    /// The organizations the caller belongs to.
    /// </summary>
    /// <remarks>
    /// A client is bound to one organization, so this is not a switcher: an app cannot open
    /// another organization's door with the token it holds. It can link to it, which is the
    /// only honest thing to offer someone who belongs to three.
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
