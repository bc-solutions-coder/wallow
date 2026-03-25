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
[Route("api/v{version:apiVersion}/identity/organizations")]
[Authorize]
[Tags("Organizations")]
[Produces("application/json")]
[Consumes("application/json")]
public class OrganizationsController(IOrganizationService orgService, ITenantContext tenantContext) : ControllerBase
{

    private bool IsCurrentTenantOrg(Guid orgId) => orgId == tenantContext.TenantId.Value;

    /// <summary>
    /// Create a new organization.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.OrganizationsCreate)]
    public async Task<ActionResult<CreateOrganizationResponse>> Create(
        CreateOrganizationRequest request, CancellationToken ct)
    {
        string? creatorEmail = User.GetEmail();
        Guid orgId = await orgService.CreateOrganizationAsync(request.Name, request.Domain, creatorEmail, ct);
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
        if (!IsCurrentTenantOrg(id))
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
        if (!IsCurrentTenantOrg(id))
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
    public async Task<ActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
    {
        if (!IsCurrentTenantOrg(id))
        {
            return NotFound();
        }

        await orgService.AddMemberAsync(id, request.UserId, ct);
        return NoContent();
    }

    /// <summary>
    /// Remove a user from an organization.
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (!IsCurrentTenantOrg(id))
        {
            return NotFound();
        }

        await orgService.RemoveMemberAsync(id, userId, ct);
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
    public async Task<ActionResult> Archive(Guid id, CancellationToken ct)
    {
        if (!IsCurrentTenantOrg(id))
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
    public async Task<ActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        if (!IsCurrentTenantOrg(id))
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
    public async Task<ActionResult> Delete(Guid id, DeleteOrganizationRequest request, CancellationToken ct)
    {
        if (!IsCurrentTenantOrg(id))
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
        if (!IsCurrentTenantOrg(id))
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
        if (!IsCurrentTenantOrg(id))
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
    public async Task<ActionResult<object>> UploadBrandingLogo(
        Guid id, IFormFile file, CancellationToken ct)
    {
        if (!IsCurrentTenantOrg(id))
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
        if (!IsCurrentTenantOrg(id))
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
    public async Task<ActionResult> UpdateSettings(Guid id, UpdateOrganizationSettingsRequest request, CancellationToken ct)
    {
        if (!IsCurrentTenantOrg(id))
        {
            return NotFound();
        }

        Guid actorId = Guid.Parse(User.GetUserId()!);
        await orgService.UpdateSettingsAsync(id, request.RequireMfa ?? false, false, request.MfaGracePeriodDays ?? 0, actorId, ct);
        return NoContent();
    }
}
