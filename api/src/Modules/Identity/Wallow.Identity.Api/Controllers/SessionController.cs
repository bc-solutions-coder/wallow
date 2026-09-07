using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/sessions")]
public sealed class SessionController(ISessionService sessionService) : ControllerBase
{
    /// <summary>
    /// List the current user active sign-in sessions.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user. Returns unrevoked, unexpired account sessions across organizations, ordered
    /// newest first, with creation, activity, and expiry timestamps.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSessions(CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        List<ActiveSession> sessions = await sessionService.GetActiveSessionsAsync(userId, ct);
        List<SessionDto> dtos = sessions
            .Select(s => new SessionDto(s.Id.Value, s.CreatedAt, s.LastActivityAt, s.ExpiresAt))
            .ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Revoke one of the current user sign-in sessions.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user. Marks the owned session revoked and revokes credentials associated with its
    /// OIDC session identifier. Returns no content after revocation; the session must belong to the caller.
    /// </remarks>
    /// <param name="sessionId">Session ID returned by the session list, not an access token.</param>
    /// <param name="ct">Cancels the request.</param>
    [HttpDelete("{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        await sessionService.RevokeSessionAsync(sessionId, userId, ct);
        return NoContent();
    }
}
