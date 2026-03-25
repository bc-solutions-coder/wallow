using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/invitations")]
[Tags("Invitations")]
[Produces("application/json")]
[Consumes("application/json")]
public class InvitationsController(
    IInvitationService invitationService,
    IInvitationRepository invitationRepository,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<InvitationResponse>> Create(
        CreateInvitationRequest request, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        Guid tenantId = tenantContext.TenantId.Value;

        Invitation invitation = await invitationService.CreateInvitationAsync(tenantId, request.Email, userId, ct);

        InvitationResponse response = MapToResponse(invitation);
        return CreatedAtAction(nameof(Verify), new { token = invitation.Token }, response);
    }

    [HttpGet]
    [Authorize]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InvitationResponse>>> GetByTenant(
        [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        List<Invitation> invitations = await invitationRepository.GetPagedByTenantAsync(tenantId, skip, take, ct);
        List<InvitationResponse> responses = invitations.Select(MapToResponse).ToList();
        return Ok(responses);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Revoke(Guid id, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        await invitationService.RevokeInvitationAsync(id, userId, ct);
        return NoContent();
    }

    [HttpGet("verify/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvitationResponse>> Verify(string token, CancellationToken ct)
    {
        Invitation? invitation = await invitationService.GetInvitationByTokenAsync(token, ct);
        if (invitation is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(invitation));
    }

    [HttpPost("{token}/accept")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Accept(string token, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        await invitationService.AcceptInvitationAsync(token, userId, ct);
        return NoContent();
    }

    private static InvitationResponse MapToResponse(Invitation invitation)
    {
        return new InvitationResponse(
            invitation.Id.Value,
            invitation.Email,
            invitation.Status.ToString(),
            invitation.ExpiresAt,
            invitation.CreatedAt,
            invitation.AcceptedByUserId);
    }
}
