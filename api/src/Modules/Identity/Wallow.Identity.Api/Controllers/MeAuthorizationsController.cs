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
/// The caller's consent ledger: the applications they have durably authorized, and the
/// withdrawal that disconnects one — revoking the authorization and every token chained to it.
/// Caller-scoped like the rest of <c>/me</c>, so it needs no organization context.
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
    /// The applications the caller has consented to.
    /// </summary>
    /// <remarks>
    /// One entry per durable consent record, naming the client and the scopes the caller agreed
    /// to. First-party sign-ins never appear here — their authorizations are session bookkeeping,
    /// not consent.
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
    /// Withdraws one consent, revoking the authorization and every token issued under it.
    /// </summary>
    /// <remarks>
    /// Refresh tokens chained to the authorization fail with <c>invalid_grant</c>, and issued
    /// access tokens are refused on their next request. Answers 404 for an authorization that
    /// does not exist or is not the caller's own.
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
