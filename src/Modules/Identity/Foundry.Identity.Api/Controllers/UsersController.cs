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
[Route("api/v{version:apiVersion}/identity/users")]
[Authorize]
[Tags("Users")]
[Produces("application/json")]
[Consumes("application/json")]
public class UsersController : ControllerBase
{
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly IKeycloakOrganizationService _keycloakOrg;
    private readonly ITenantContext _tenantContext;

    public UsersController(
        IKeycloakAdminService keycloakAdmin,
        IKeycloakOrganizationService keycloakOrg,
        ITenantContext tenantContext)
    {
        _keycloakAdmin = keycloakAdmin;
        _keycloakOrg = keycloakOrg;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Get a paginated list of users with optional search filtering.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.UsersRead)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int first = 0,
        [FromQuery] int max = 20,
        CancellationToken ct = default)
    {
        IReadOnlyList<UserDto> users = await _keycloakAdmin.GetUsersAsync(search, first, max, ct);
        return Ok(users);
    }

    /// <summary>
    /// Get a specific user by their ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.UsersRead)]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id, CancellationToken ct)
    {
        UserDto? user = await _keycloakAdmin.GetUserByIdAsync(id, ct);
        if (user is null)
        {
            return NotFound();
        }

        if (!await UserBelongsToTenantAsync(id, ct))
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Get the currently authenticated user's profile, roles, and permissions.
    /// </summary>
    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> GetCurrentUser()
    {
        return Ok(new CurrentUserResponse
        {
            Id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            FirstName = User.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty,
            LastName = User.FindFirstValue(ClaimTypes.Surname) ?? string.Empty,
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            Permissions = User.FindAll("permission").Select(c => c.Value).ToList()
        });
    }

    /// <summary>
    /// Create a new user account.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.UsersCreate)]
    public async Task<ActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        Guid userId = await _keycloakAdmin.CreateUserAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            ct);

        UserDto? user = await _keycloakAdmin.GetUserByIdAsync(userId, ct);
        return CreatedAtAction(nameof(GetUserById), new { id = userId }, user);
    }

    /// <summary>
    /// Deactivate a user account.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(PermissionType.UsersUpdate)]
    public async Task<ActionResult> DeactivateUser(Guid id, CancellationToken ct)
    {
        if (!await UserBelongsToTenantAsync(id, ct))
        {
            return NotFound();
        }

        await _keycloakAdmin.DeactivateUserAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Activate a previously deactivated user account.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(PermissionType.UsersUpdate)]
    public async Task<ActionResult> ActivateUser(Guid id, CancellationToken ct)
    {
        if (!await UserBelongsToTenantAsync(id, ct))
        {
            return NotFound();
        }

        await _keycloakAdmin.ActivateUserAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Assign a role to a user.
    /// </summary>
    [HttpPost("{userId:guid}/roles")]
    [HasPermission(PermissionType.RolesUpdate)]
    public async Task<ActionResult> AssignRole(
        Guid userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken ct)
    {
        if (!await UserBelongsToTenantAsync(userId, ct))
        {
            return NotFound();
        }

        await _keycloakAdmin.AssignRoleAsync(userId, request.RoleName, ct);
        return NoContent();
    }

    /// <summary>
    /// Remove a role from a user.
    /// </summary>
    [HttpDelete("{userId:guid}/roles/{roleName}")]
    [HasPermission(PermissionType.RolesUpdate)]
    public async Task<ActionResult> RemoveRole(Guid userId, string roleName, CancellationToken ct)
    {
        if (!await UserBelongsToTenantAsync(userId, ct))
        {
            return NotFound();
        }

        await _keycloakAdmin.RemoveRoleAsync(userId, roleName, ct);
        return NoContent();
    }

    private async Task<bool> UserBelongsToTenantAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyList<OrganizationDto> userOrgs = await _keycloakOrg.GetUserOrganizationsAsync(userId, ct);
        return userOrgs.Any(o => o.Id == _tenantContext.TenantId.Value);
    }
}
