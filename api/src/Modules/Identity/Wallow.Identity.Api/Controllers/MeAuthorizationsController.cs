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
/// Lists and withdraws the caller permanent consents without requiring organization context.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/me/authorizations")]
[Authorize]
[AllowWithoutOrganization]
[Tags("Me")]
[Produces("application/json")]
public sealed class MeAuthorizationsController(
    IConnectedApplicationService connectedApplications) : ControllerBase
{
    /// <summary>
    /// Lists valid permanent consent records for the caller.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectedApplicationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectedApplicationDto>>> ListConnectedApplications(
        CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        return Ok(await connectedApplications.GetConnectedApplicationsAsync(userId, ct));
    }

    /// <summary>
    /// Withdraws the caller consent and revokes associated user/client access.
    /// Returns 404 when the consent cannot be found for the caller.
    /// </summary>
    [HttpDelete("{authorizationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WithdrawConsent(string authorizationId, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        bool withdrawn = await connectedApplications.WithdrawAsync(userId, authorizationId, ct);
        return withdrawn ? NoContent() : NotFound();
    }
}
