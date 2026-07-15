using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/membership-requests")]
[Authorize]
[Tags("Membership Requests")]
[Produces("application/json")]
[Consumes("application/json")]
public class MembershipRequestsController(
    IDomainAssignmentService domainAssignmentService,
    IMembershipRequestRepository membershipRequestRepository) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult> RequestMembership(CreateMembershipRequest request, CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        Guid requestId = await domainAssignmentService.RequestMembershipAsync(userId, request.EmailDomain, ct);
        return CreatedAtAction(nameof(GetById), new { id = requestId }, new { id = requestId });
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.OrganizationsRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById(Guid id, CancellationToken ct)
    {
        MembershipRequest? request = await membershipRequestRepository.GetByIdAsync(
            MembershipRequestId.Create(id), ct);
        if (request is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(request));
    }

    [HttpGet("pending")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPending(
        [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        List<MembershipRequest> requests = await membershipRequestRepository.GetPendingAsync(skip, take, ct);
        return Ok(requests.Select(MapToResponse));
    }

    [HttpPost("{id:guid}/approve")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Approve(Guid id, ApproveMembershipRequest request, CancellationToken ct)
    {
        await domainAssignmentService.ApproveMembershipRequestAsync(id, request.OrganizationId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Reject(Guid id, CancellationToken ct)
    {
        await domainAssignmentService.RejectMembershipRequestAsync(id, ct);
        return NoContent();
    }

    private static object MapToResponse(MembershipRequest request)
    {
        return new
        {
            id = request.Id.Value,
            userId = request.UserId,
            emailDomain = request.EmailDomain,
            status = request.Status.ToString(),
            resolvedOrganizationId = request.ResolvedOrganizationId?.Value,
            createdAt = request.CreatedAt
        };
    }
}

public record CreateMembershipRequest(string EmailDomain);
public record ApproveMembershipRequest(Guid OrganizationId);
