using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
[Tags("Organizations")]
[Produces("application/json")]
[Consumes("application/json")]
public class OrganizationsController(
    IOrganizationService orgService,
    IMembershipReviewService membershipReview,
    ITenantContext tenantContext,
    IOrganizationAccessPolicy accessPolicy) : ControllerBase
{

    // Organization IS the tenant, so every caller is scoped to the org matching their own
    // tenant id. The is_global_admin claim is the only BLANKET cross-tenant escape hatch, matching
    // TenantResolutionMiddleware and PermissionExpansionMiddleware; no role string grants it,
    // otherwise any tenant-assignable "admin" could reach other tenants' orgs by guessing GUIDs.
    //
    // Creating an org mints a new tenant id that never equals the creator's own, so membership is
    // the second, NARROW path — narrow because the permission travels with it. Each endpoint passes
    // the permission it already demands, so a foreign member who may read this org still cannot
    // delete it, and a member the org granted no role reaches nothing at all.
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
    /// Create a new organization.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.OrganizationsCreate)]
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
    /// Get all organizations with optional search filtering and pagination.
    /// </summary>
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
    /// Get a specific organization by ID.
    /// </summary>
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
    /// Get all members of a specific organization.
    /// </summary>
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
    /// Add a user to an organization.
    /// </summary>
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

        await orgService.AddMemberAsync(id, request.UserId, request.Role, ct);
        return NoContent();
    }

    /// <summary>
    /// Remove a user from an organization.
    /// </summary>
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

        await orgService.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }

    /// <summary>
    /// List the organization's outstanding access requests, oldest first.
    /// </summary>
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
    /// Admit a pending requester, granting them the organization's default role.
    /// </summary>
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
    /// Turn a pending requester away.
    /// </summary>
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
    /// Let a denied requester ask again now, instead of waiting out the denial.
    /// </summary>
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
    /// Take an active member's access to this organization away, keeping the membership so it can
    /// be reinstated.
    /// </summary>
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
    /// Give a suspended member their access back.
    /// </summary>
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
    /// Give up your own membership of an organization.
    /// </summary>
    /// <remarks>
    /// Asks for no permission and consults no access policy: the caller is deciding about
    /// themselves, and requiring one would shut out members of every organization that is not the
    /// one their token is scoped to — which is most of them. Membership itself is the authority
    /// here, so a caller who has none gets the same refusal a stranger does.
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
    /// Get all organizations that the current user belongs to.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetMyOrganizations(CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.GetUserId()!);
        return Ok(await orgService.GetUserOrganizationsAsync(userId, ct));
    }

    /// <summary>
    /// Archive an organization.
    /// </summary>
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
    /// Permanently delete an organization. Requires name confirmation.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionType.OrganizationsUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, DeleteOrganizationRequest request, CancellationToken ct)
    {
        if (!await CanAddressOrganizationAsync(id, PermissionType.OrganizationsUpdate, ct))
        {
            return NotFound();
        }

        await orgService.DeleteAsync(id, request.ConfirmName, ct);
        return NoContent();
    }

    /// <summary>
    /// Get organization branding.
    /// </summary>
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
    /// Upload organization branding logo.
    /// </summary>
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
    /// Get organization settings.
    /// </summary>
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
    /// Update organization settings.
    /// </summary>
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
    /// Set who may join this organization and the role they join with.
    /// </summary>
    /// <remarks>
    /// Separate from the settings route above, and gated on managing members rather than on editing
    /// settings: these three fields decide the organization's membership.
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
