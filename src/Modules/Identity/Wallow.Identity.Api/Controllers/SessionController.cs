using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion(1)]
[IgnoreAntiforgeryToken]
[Route("api/v{version:apiVersion}/identity/sessions")]
public sealed class SessionController(ISessionService sessionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListSessions(CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        List<ActiveSession> sessions = await sessionService.GetActiveSessionsAsync(userId, ct);
        List<SessionDto> dtos = sessions
            .Select(s => new SessionDto(s.Id.Value, s.CreatedAt, s.LastActivityAt, s.ExpiresAt))
            .ToList();
        return Ok(dtos);
    }

    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        await sessionService.RevokeSessionAsync(sessionId, userId, ct);
        return NoContent();
    }
}
