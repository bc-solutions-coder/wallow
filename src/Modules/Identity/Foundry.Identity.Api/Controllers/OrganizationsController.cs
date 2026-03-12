using System.Security.Claims;
using Asp.Versioning;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/identity/organizations")]
[Authorize]
[Tags("Organizations")]
[Produces("application/json")]
[Consumes("application/json")]
public class OrganizationsController(IKeycloakOrganizationService orgService, ITenantContext tenantContext) : ControllerBase
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
        string? creatorEmail = User.FindFirstValue(ClaimTypes.Email);
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
        Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await orgService.GetUserOrganizationsAsync(userId, ct));
    }
}
