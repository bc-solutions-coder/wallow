using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wallow.Identity.Api.Authorization;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/organizations")]
[Authorize]
[TypeFilter(typeof(RefusePlatformSuspendedOrganizationFilter))]
[Tags("Organizations")]
[Produces("application/json")]
[Consumes("application/json")]
public class OrganizationsController(
    IOrganizationService orgService,
    IMembershipReviewService membershipReview,
    ITenantContext tenantContext,
    IOrganizationAccessPolicy accessPolicy) : ControllerBase
{

    // Foreign organizations require global administration or a membership with the requested permission.
    private async Task<bool> CanAddressOrganizationAsync(Guid orgId, string requiredPermission, CancellationToken ct)
    {
        if (orgId == tenantContext.TenantId.Value || User.IsGlobalAdmin())
        {
            return true;
        }

        return Guid.TryParse(User.GetUserId(), out Guid callerId)
            && await accessPolicy.HasPermissionInOrganizationAsync(orgId, callerId, requiredPermission, ct);
    }

    private Guid ActorId() => Guid.Parse(User.GetUserId()!);

    /// <summary>
    /// Create an organization.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated user, without an existing organization or tenant permission. Creates a tenant and
    /// enrolls the caller as an owner with the admin role, then returns the organization ID.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [AllowWithoutOrganization]
    [EnableRateLimiting("registration")]
    public async Task<ActionResult<CreateOrganizationResponse>> Create(
        CreateOrganizationRequest request, CancellationToken ct)
    {
        string? creatorEmail = User.GetEmail();
        Guid creatorUserId = Guid.Parse(User.GetUserId()!);
        Guid orgId = await orgService.CreateOrganizationAsync(request.Name, request.Domain, creatorEmail, creatorUserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = orgId },
            new CreateOrganizationResponse(orgId));
    }

    /// <summary>
    /// Find the resolved tenant organization.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsRead. Searches organization names and pages results in name order before retaining only
    /// the resolved tenant, so the response contains at most one organization and can be empty. first is the
    /// zero-based offset and max is the result limit before this tenant filter.
    /// </remarks>
    [HttpGet]
    [HasPermission(PermissionType.OrganizationsRead)]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] int first = 0, [FromQuery] int max = 20,
        CancellationToken ct = default)
    {
        IReadOnlyList<OrganizationDto> orgs = await orgService.GetOrganizationsAsync(search, first, max, ct);
        Guid tenantId = tenantContext.TenantId.Value;
        IReadOnlyList<OrganizationDto> filtered = orgs.Where(o => o.Id == tenantId).ToList();
        return Ok(filtered);
    }

    /// <summary>
    /// Get an organization.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsRead. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns organization details,
    /// or 404 when the organization is missing or inaccessible.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.OrganizationsRead)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsRead, ct))
        {
            return NotFound();
        }

        OrganizationDto? org = await orgService.GetOrganizationByIdAsync(id, ct);
        return org is null ? NotFound() : Ok(org);
    }

    /// <summary>
    /// List active organization members.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsRead. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns user profiles and roles
    /// for active memberships in the addressed organization.
    /// </remarks>
    [HttpGet("{id:guid}/members")]
    [HasPermission(PermissionType.OrganizationsRead)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsRead, ct))
        {
            return NotFound();
        }

        return Ok(await orgService.GetMembersAsync(id, ct));
    }

    /// <summary>
    /// Grant organization membership.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Supply an existing user ID and
    /// role name. Creates or activates the membership and adds the role, including when the existing membership is
    /// pending, denied, or suspended.
    /// </remarks>
    [HttpPost("{id:guid}/members")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        await orgService.AddMemberAsync(id, request.UserId, request.Role, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Remove an organization member.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Deletes the membership and
    /// revokes its access. Removing the last active owner is rejected.
    /// </remarks>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        await orgService.RemoveMemberAsync(id, userId, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// List pending membership requests.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns requester profiles and
    /// request times, oldest first.
    /// </remarks>
    [HttpGet("{id:guid}/members/pending")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PendingMembershipDto>>> GetPendingMembers(
        Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        return Ok(await membershipReview.GetPendingAsync(id, ct));
    }

    /// <summary>
    /// List suspended organization members.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns suspended memberships
    /// with user profiles, ordered by most recent membership update.
    /// </remarks>
    [HttpGet("{id:guid}/members/suspended")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReviewedMembershipDto>>> GetSuspendedMembers(
        Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        return Ok(await membershipReview.GetSuspendedAsync(id, ct));
    }

    /// <summary>
    /// List denied membership requests.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns denied memberships with
    /// requester profiles, ordered by most recent review.
    /// </remarks>
    [HttpGet("{id:guid}/members/denied")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReviewedMembershipDto>>> GetDeniedMembers(
        Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        return Ok(await membershipReview.GetDeniedAsync(id, ct));
    }

    /// <summary>
    /// Approve a membership request.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Activates a pending membership
    /// and grants the organization default role. A membership that is not pending is rejected.
    /// </remarks>
    [HttpPost("{id:guid}/members/{userId:guid}/approve")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ApproveMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        await membershipReview.ApproveAsync(id, userId, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Deny a membership request.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Marks a pending membership as
    /// denied. A membership that is not pending is rejected.
    /// </remarks>
    [HttpPost("{id:guid}/members/{userId:guid}/deny")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> DenyMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        await membershipReview.DenyAsync(id, userId, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Clear a membership denial.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Removes a denied membership so
    /// the user can request access again immediately. This does not grant access.
    /// </remarks>
    [HttpDelete("{id:guid}/members/{userId:guid}/denial")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ClearDenial(Guid id, Guid userId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        await membershipReview.ClearDenialAsync(id, userId, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Suspend an organization member.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Suspends an active membership
    /// and revokes its access while retaining its roles. Suspending the last active owner is rejected.
    /// </remarks>
    [HttpPost("{id:guid}/members/{userId:guid}/suspend")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SuspendMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        await membershipReview.SuspendAsync(id, userId, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Reinstate a suspended member.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Reactivates a suspended
    /// membership with its retained roles. Revoked tokens remain revoked.
    /// </remarks>
    [HttpPost("{id:guid}/members/{userId:guid}/reinstate")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReinstateMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        await membershipReview.ReinstateAsync(id, userId, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Leave an organization.
    /// </summary>
    /// <remarks>
    /// Requires an authenticated member, without a management permission. Removes the caller membership and revokes
    /// access to that organization. Leaving as the last active owner is rejected, and platform suspension prevents
    /// this action for non-global administrators.
    /// </remarks>
    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Leave(Guid id, CancellationToken ct)
    {
        await membershipReview.LeaveAsync(id, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Archive an organization.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsUpdate. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Archives the organization and
    /// revokes organization access. Registration and membership records remain available for reactivation.
    /// </remarks>
    [HttpPost("{id:guid}/archive")]
    [HasPermission(PermissionType.OrganizationsUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsUpdate, ct))
        {
            return NotFound();
        }

        Guid actorId = Guid.Parse(User.GetUserId()!);
        await orgService.ArchiveAsync(id, actorId, ct);
        return NoContent();
    }

    /// <summary>
    /// Reactivate an archived organization.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsUpdate. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Restores the organization to
    /// active status. Revoked credentials and separate client suspensions are not restored.
    /// </remarks>
    [HttpPost("{id:guid}/reactivate")]
    [HasPermission(PermissionType.OrganizationsUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsUpdate, ct))
        {
            return NotFound();
        }

        Guid actorId = Guid.Parse(User.GetUserId()!);
        await orgService.ReactivateAsync(id, actorId, ct);
        return NoContent();
    }

    /// <summary>
    /// Suspend an organization at platform level.
    /// </summary>
    /// <remarks>
    /// Requires a global administrator. Records the supplied reason and revokes organization access. Non-global
    /// administrators cannot mutate the organization while the platform suspension remains in effect.
    /// </remarks>
    [HttpPost("{id:guid}/platform-suspension")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> PlacePlatformSuspension(
        Guid id, PlatformSuspensionRequest request, CancellationToken ct)
    {
        if (!User.IsGlobalAdmin())
        {
            return Forbid();
        }

        await orgService.SuspendByPlatformAsync(id, request.Reason, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Lift an organization platform suspension.
    /// </summary>
    /// <remarks>
    /// Requires a global administrator. Clears the addressed organization platform suspension. Revoked tokens,
    /// archive status, and separate client suspensions remain unchanged.
    /// </remarks>
    [HttpDelete("{id:guid}/platform-suspension")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> LiftPlatformSuspension(Guid id, CancellationToken ct)
    {
        if (!User.IsGlobalAdmin())
        {
            return Forbid();
        }

        await orgService.ReinstateByPlatformAsync(id, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Permanently delete an organization.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsDelete. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. confirmName must exactly match
    /// the organization name. Revokes access and deletes the organization, memberships, clients, invitations, and
    /// identity settings, then requests cleanup in other modules.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionType.OrganizationsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, DeleteOrganizationRequest request, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsDelete, ct))
        {
            return NotFound();
        }

        await orgService.DeleteAsync(id, request.ConfirmName, ActorId(), User.IsGlobalAdmin(), ct);
        return NoContent();
    }

    /// <summary>
    /// Get organization branding.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsRead. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns stored logo and colors,
    /// or 404 when no branding exists in the resolved tenant. displayName is not stored by this endpoint and is
    /// returned as null.
    /// </remarks>
    [HttpGet("{id:guid}/branding")]
    [HasPermission(PermissionType.OrganizationsRead)]
    public async Task<ActionResult<OrganizationBrandingResponse>> GetBranding(Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsRead, ct))
        {
            return NotFound();
        }

        OrganizationBrandingDto? branding = await orgService.GetBrandingAsync(id, ct);
        if (branding is null)
        {
            return NotFound();
        }

        return Ok(new OrganizationBrandingResponse(
            branding.DisplayName,
            branding.LogoUrl,
            branding.PrimaryColor,
            branding.AccentColor));
    }

    /// <summary>
    /// Update organization branding.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsUpdate. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Replaces the logo URL and
    /// primary color, preserving the accent color. displayName is echoed in this response but is not persisted.
    /// </remarks>
    [HttpPut("{id:guid}/branding")]
    [HasPermission(PermissionType.OrganizationsUpdate)]
    public async Task<ActionResult<OrganizationBrandingResponse>> UpdateBranding(
        Guid id, UpdateOrganizationBrandingRequest request, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsUpdate, ct))
        {
            return NotFound();
        }

        Guid actorId = Guid.Parse(User.GetUserId()!);
        OrganizationBrandingDto branding = await orgService.UpdateBrandingAsync(
            id, request.DisplayName, request.LogoUrl, request.PrimaryColor, actorId, ct);

        return Ok(new OrganizationBrandingResponse(
            branding.DisplayName,
            branding.LogoUrl,
            branding.PrimaryColor,
            branding.AccentColor));
    }

    /// <summary>
    /// Get a proposed organization logo URL.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsUpdate. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Accepts a multipart file and
    /// returns a URL based on its filename. This endpoint currently does not store the file or update the
    /// organization branding.
    /// </remarks>
    [HttpPost("{id:guid}/branding/logo")]
    [HasPermission(PermissionType.OrganizationsUpdate)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(OrganizationLogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> UploadBrandingLogo(
        Guid id, IFormFile file, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsUpdate, ct))
        {
            return NotFound();
        }

        Guid actorId = Guid.Parse(User.GetUserId()!);
        await using Stream stream = file.OpenReadStream();
        string logoUrl = await orgService.UploadBrandingLogoAsync(
            id, stream, file.FileName, file.ContentType, actorId, ct);

        return Ok(new { LogoUrl = logoUrl });
    }

    /// <summary>
    /// Get organization security and enrollment settings.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsRead. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Returns MFA, passwordless
    /// login, and enrollment settings, or 404 when settings are absent from the resolved tenant.
    /// </remarks>
    [HttpGet("{id:guid}/settings")]
    [HasPermission(PermissionType.OrganizationsRead)]
    public async Task<ActionResult<OrganizationSettingsDto>> GetSettings(Guid id, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsRead, ct))
        {
            return NotFound();
        }

        OrganizationSettingsDto? settings = await orgService.GetSettingsAsync(id, ct);
        return settings is null ? NotFound() : Ok(settings);
    }

    /// <summary>
    /// Replace organization MFA settings.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsUpdate. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Omitted requireMfa and
    /// mfaGracePeriodDays become false and zero; passwordless login is disabled. A positive grace period with
    /// required MFA sets a new deadline for active members without MFA; allowedLoginMethods and defaultMemberRole are
    /// currently ignored.
    /// </remarks>
    [HttpPut("{id:guid}/settings")]
    [HasPermission(PermissionType.OrganizationsUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateSettings(Guid id, UpdateOrganizationSettingsRequest request, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsUpdate, ct))
        {
            return NotFound();
        }

        Guid actorId = Guid.Parse(User.GetUserId()!);
        await orgService.UpdateSettingsAsync(id, request.RequireMfa ?? false, false, request.MfaGracePeriodDays ?? 0, actorId, ct);
        return NoContent();
    }

    /// <summary>
    /// Replace organization enrollment settings.
    /// </summary>
    /// <remarks>
    /// Requires OrganizationsManageMembers. The organization must be the resolved tenant, or you must be a global
    /// administrator or hold this permission through membership in that organization. Replaces the enrollment policy,
    /// access-request email, and default role ID. A supplied role ID must exist; null selects the user role for
    /// future enrollments.
    /// </remarks>
    [HttpPut("{id:guid}/enrollment")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateEnrollment(Guid id, UpdateOrganizationEnrollmentRequest request, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsManageMembers, ct))
        {
            return NotFound();
        }

        Guid actorId = Guid.Parse(User.GetUserId()!);
        await orgService.UpdateEnrollmentAsync(
            id, request.EnrollmentPolicy, request.AccessRequestEmail, request.DefaultRoleId, actorId, ct);

        return NoContent();
    }
}
