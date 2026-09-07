using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/invitations")]
[Tags("Invitations")]
[Produces("application/json")]
[Consumes("application/json")]
public class InvitationsController(
    IInvitationService invitationService,
    IInvitationRepository invitationRepository,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>
    /// Invite a user to the resolved organization.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers in the resolved tenant. Creates an email invitation valid for seven days,
    /// or renews an outstanding invitation with the same token, and requests delivery. Existing active members cannot
    /// be invited; the response includes invitation status and expiry.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<InvitationResponse>> Create(
        CreateInvitationRequest request, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);

        Invitation invitation = await invitationService.CreateInvitationAsync(request.Email, userId, ct);

        InvitationResponse response = MapToResponse(invitation);
        return CreatedAtAction(nameof(Verify), new { token = invitation.Token }, response);
    }

    /// <summary>
    /// List organization invitations.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers in the resolved tenant. Returns invitations of all statuses, newest first,
    /// without invitation tokens. skip is a zero-based offset and take is the maximum number of results.
    /// </remarks>
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

    /// <summary>
    /// Revoke an organization invitation.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers in the resolved tenant. Revokes a pending invitation identified by its ID,
    /// preventing acceptance. An invitation outside the resolved tenant or a missing invitation returns 404.
    /// </remarks>
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

    /// <summary>
    /// Look up an invitation token.
    /// </summary>
    /// <remarks>
    /// Allows anonymous access without an organization context. Returns the matching invitation recipient, status,
    /// and expiry, including invitations that are no longer pending. A successful lookup does not guarantee that
    /// acceptance is allowed; unknown tokens return 404.
    /// </remarks>
    [HttpGet("verify/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvitationResponse>> Verify(string token, CancellationToken ct)
    {
        Invitation? invitation = await invitationService.GetInvitationByTokenAsync(token, ct);
        if (invitation is null)
        {
            return this.Problem(IdentityErrors.InvitationNotFound);
        }

        return Ok(MapToResponse(invitation));
    }

    /// <summary>
    /// Accept an organization invitation.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user whose verified email matches the invitation, without an existing organization
    /// context. A valid pending invitation creates or approves membership using the invited organization default
    /// role. Expired invitations and suspended or denied memberships are rejected.
    /// </remarks>
    [HttpPost("{token}/accept")]
    [Authorize]
    [AllowWithoutOrganization]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
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
