using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/users")]
[Authorize]
[Tags("Users")]
[Produces("application/json")]
[Consumes("application/json")]
public class UsersController(IUserManagementService userManagement, IOrganizationService organizationService, IUserQueryService userQueryService, ITenantContext tenantContext) : ControllerBase
{
    private Guid ActorId() => Guid.Parse(User.GetUserId()!);

    /// <summary>
    /// Get a paginated list of users with optional search filtering.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.UsersRead)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int first = 0,
        [FromQuery] int max = 20,
        CancellationToken ct = default)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        UserSearchPageResult result = await userQueryService.SearchUsersAsync(tenantId, search, first, max, ct);

        IReadOnlyList<UserDto> items = result.Items
            .Select(u => new UserDto(u.Id, u.Email, u.FirstName, u.LastName, u.IsActive, u.Roles))
            .ToList();

        return Ok(new PagedResult<UserDto>(items, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>
    /// Get a specific user by their ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.UsersRead)]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id, CancellationToken ct)
    {
        UserDto? user = await userManagement.GetUserByIdAsync(id, ct);
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
    [AllowWithoutOrganization]
    public ActionResult<CurrentUserResponse> GetCurrentUser()
    {
        return Ok(new CurrentUserResponse
        {
            Id = Guid.Parse(User.GetUserId()!),
            Email = User.GetEmail() ?? string.Empty,
            FirstName = User.GetFirstName() ?? string.Empty,
            LastName = User.GetLastName() ?? string.Empty,
            Roles = User.GetRoles().ToList(),
            Permissions = User.GetPermissions().ToList(),
            IsGlobalAdmin = User.IsGlobalAdmin()
        });
    }

    /// <summary>
    /// Create a new user account.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.UsersCreate)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        Guid userId = await userManagement.CreateUserAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            ct);

        Guid tenantId = tenantContext.TenantId.Value;
        await organizationService.AddMemberAsync(tenantId, userId, "user", ActorId(), ct);

        UserDto? user = await userManagement.GetUserByIdAsync(userId, ct);
        return CreatedAtAction(nameof(GetUserById), new { id = userId }, user);
    }

    /// <summary>
    /// Deactivate a user account.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(PermissionType.UsersUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeactivateUser(Guid id, CancellationToken ct)
    {
        if (!await UserBelongsToTenantAsync(id, ct))
        {
            return NotFound();
        }

        await userManagement.DeactivateUserAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Activate a previously deactivated user account.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(PermissionType.UsersUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ActivateUser(Guid id, CancellationToken ct)
    {
        if (!await UserBelongsToTenantAsync(id, ct))
        {
            return NotFound();
        }

        await userManagement.ActivateUserAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Assign a role to a user IN THE CALLER'S OWN ORGANIZATION. The organization is the ambient
    /// tenant rather than a parameter, so this route can never grant a role somewhere the caller
    /// was not already authorized; the grant lands on the user's membership of that organization
    /// and confers nothing in any other. The reserved global-administrator name is rejected:
    /// global admin is a seeded claim, never a role, so it cannot be granted from inside a tenant.
    /// </summary>
    [HttpPost("{userId:guid}/roles")]
    [HasPermission(PermissionType.RolesUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AssignRole(
        Guid userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken ct)
    {
        if (IsReservedRoleName(request.RoleName))
        {
            return (ActionResult)Result.Failure(IdentityErrors.ReservedRoleName).ToActionResult();
        }

        if (!await UserBelongsToTenantAsync(userId, ct))
        {
            return NotFound();
        }

        await userManagement.AssignRoleAsync(userId, tenantContext.TenantId.Value, request.RoleName, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Remove a role from a user in the caller's own organization. Revocation is scoped the same
    /// way the grant was, so it cannot reach a role the user holds elsewhere.
    /// </summary>
    [HttpDelete("{userId:guid}/roles/{roleName}")]
    [HasPermission(PermissionType.RolesUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveRole(Guid userId, string roleName, CancellationToken ct)
    {
        if (!await UserBelongsToTenantAsync(userId, ct))
        {
            return NotFound();
        }

        await userManagement.RemoveRoleAsync(userId, tenantContext.TenantId.Value, roleName, ActorId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Matches every spelling of the reserved global-administrator name. Comparison is on the
    /// letters and digits alone, so casing, spacing, hyphens, and underscores cannot slip a
    /// variant past it. This is a deny list, not an allow list: custom roles minted through
    /// RolesController must stay assignable.
    /// </summary>
    private static bool IsReservedRoleName(string? roleName)
    {
        string normalized = new string((roleName ?? string.Empty).Where(char.IsLetterOrDigit).ToArray())
            .ToLowerInvariant();

        return normalized is "globaladmin" or "isglobaladmin";
    }

    private async Task<bool> UserBelongsToTenantAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyList<OrganizationDto> userOrgs = await organizationService.GetUserOrganizationsAsync(userId, ct);
        return userOrgs.Any(o => o.Id == tenantContext.TenantId.Value);
    }
}
