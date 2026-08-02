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
    /// Roles are held per <c>(user, organization)</c>, so every one of these takes the
    /// organization it acts in. A role granted in one organization confers nothing in another,
    /// and a revocation that named no organization could only revoke everywhere or nowhere.
    /// <para>
    /// <c>actorId</c> is who granted or revoked it. The subject cannot stand in for the actor here:
    /// a role somebody was given and a role somebody took for themselves are different events.
    /// </para>
    /// </summary>
    Task AssignRoleAsync(Guid userId, Guid organizationId, string roleName, Guid actorId, CancellationToken ct = default);

    /// <inheritdoc cref="AssignRoleAsync"/>
    Task RemoveRoleAsync(Guid userId, Guid organizationId, string roleName, Guid actorId, CancellationToken ct = default);

    /// <inheritdoc cref="AssignRoleAsync"/>
    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
