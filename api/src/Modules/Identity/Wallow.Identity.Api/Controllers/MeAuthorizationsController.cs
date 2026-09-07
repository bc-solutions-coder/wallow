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
    /// List your application consents.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user, without an organization context or management permission. Returns valid
    /// permanent consent records for existing applications, newest first, including authorization IDs and granted
    /// scopes.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectedApplicationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectedApplicationDto>>> ListConnectedApplications(
        CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        return Ok(await connectedApplications.GetConnectedApplicationsAsync(userId, ct));
    }

    /// <summary>
    /// Withdraw consent for an application.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user, without an organization context or management permission. Uses an
    /// authorization ID from your consent list to request revocation of that consent and your tokens for its
    /// application. A missing, invalid, or other user consent returns 404.
    /// </remarks>
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
