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
    /// Search user accounts.
    /// </summary>
    /// <remarks>
    /// Requires UsersRead in the resolved tenant. Searches the global user directory by email, first name, or last
    /// name, ordered by email; displayed roles come only from active memberships in the resolved tenant. first is a
    /// zero-based offset and max is the page size; the response includes total count and page metadata.
    /// </remarks>
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
    /// Get an organization member account.
    /// </summary>
    /// <remarks>
    /// Requires UsersRead and an active membership for the target user in the resolved tenant. Returns the account
    /// profile and roles in that tenant, or 404 when the user is missing or is not a member.
    /// </remarks>
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
    /// Get the authenticated user profile.
    /// </summary>
    /// <remarks>
    /// Requires authentication without an organization context or management permission. Returns profile fields,
    /// roles, permissions, and global administrator status from the current authentication claims.
    /// </remarks>
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
    /// Create a user and organization membership.
    /// </summary>
    /// <remarks>
    /// Requires UsersCreate in the resolved tenant. Creates an account with the supplied email and profile,
    /// optionally sets a password, and enrolls the user with the user role. Returns the created account and its URL.
    /// </remarks>
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
    /// Lock a member user account.
    /// </summary>
    /// <remarks>
    /// Requires UsersUpdate and an active membership for the target user in the resolved tenant. Locks the account
    /// indefinitely and revokes the user access across organizations. A missing membership returns 404.
    /// </remarks>
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
    /// Unlock a member user account.
    /// </summary>
    /// <remarks>
    /// Requires UsersUpdate and an active membership for the target user in the resolved tenant. Clears the account
    /// lockout across organizations. Previously revoked tokens remain revoked; a missing membership returns 404.
    /// </remarks>
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
    /// Assign a role to an organization member.
    /// </summary>
    /// <remarks>
    /// Requires RolesUpdate and an active membership for the target user in the resolved tenant. Adds an existing
    /// named role to that membership without replacing other roles. Reserved global-administrator role names are
    /// rejected.
    /// </remarks>
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
    /// Remove a role from an organization member.
    /// </summary>
    /// <remarks>
    /// Requires RolesUpdate and an active membership for the target user in the resolved tenant. Removes the named
    /// role from that membership while retaining other roles. A missing membership returns 404.
    /// </remarks>
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
    /// Rejects globaladmin and isglobaladmin after removing non-alphanumeric characters
    /// and lowercasing; other role names proceed to service validation.
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
