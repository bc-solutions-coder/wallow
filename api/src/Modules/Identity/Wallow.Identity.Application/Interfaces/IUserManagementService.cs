using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

public interface IUserManagementService
{
    Task<Guid> CreateUserAsync(string email, string firstName, string lastName, string? password = null, CancellationToken ct = default);
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetUsersAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default);
    Task DeactivateUserAsync(Guid userId, CancellationToken ct = default);
    Task ActivateUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>
    /// Assigns a role within the named organization. actorId identifies the grantor
    /// for audit events and is distinct from the user receiving the role.
    /// </summary>
    Task AssignRoleAsync(Guid userId, Guid organizationId, string roleName, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Removes an organization-scoped role and records actorId as the revoking user.
    /// </summary>
    Task RemoveRoleAsync(Guid userId, Guid organizationId, string roleName, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Resolves role names from this user's active membership in the organization.
    /// </summary>
    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
