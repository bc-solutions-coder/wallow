using System.Security.Claims;
using Asp.Versioning;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity.Authorization;
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
public class OrganizationsController : ControllerBase
{
    private readonly IKeycloakOrganizationService _orgService;

    public OrganizationsController(IKeycloakOrganizationService orgService)
    {
        _orgService = orgService;
    }

    /// <summary>
    /// Create a new organization.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.OrganizationsCreate)]
    public async Task<ActionResult<CreateOrganizationResponse>> Create(
        CreateOrganizationRequest request, CancellationToken ct)
    {
        Guid orgId = await _orgService.CreateOrganizationAsync(request.Name, request.Domain, ct);
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
        IReadOnlyList<OrganizationDto> orgs = await _orgService.GetOrganizationsAsync(search, first, max, ct);
        return Ok(orgs);
    }

    /// <summary>
    /// Get a specific organization by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.OrganizationsRead)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken ct)
    {
        OrganizationDto? org = await _orgService.GetOrganizationByIdAsync(id, ct);
        return org is null ? NotFound() : Ok(org);
    }

    /// <summary>
    /// Get all members of a specific organization.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [HasPermission(PermissionType.OrganizationsRead)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        return Ok(await _orgService.GetMembersAsync(id, ct));
    }

    /// <summary>
    /// Add a user to an organization.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    public async Task<ActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
    {
        await _orgService.AddMemberAsync(id, request.UserId, ct);
        return NoContent();
    }

    /// <summary>
    /// Remove a user from an organization.
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [HasPermission(PermissionType.OrganizationsManageMembers)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await _orgService.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }

    /// <summary>
    /// Get all organizations that the current user belongs to.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetMyOrganizations(CancellationToken ct)
    {
        Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _orgService.GetUserOrganizationsAsync(userId, ct));
    }
}
