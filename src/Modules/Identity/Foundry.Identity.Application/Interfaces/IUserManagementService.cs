using Foundry.Identity.Application.DTOs;

namespace Foundry.Identity.Application.Interfaces;

public interface IUserManagementService
{
    Task<Guid> CreateUserAsync(string email, string firstName, string lastName, string? password = null, CancellationToken ct = default);
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetUsersAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default);
    Task DeactivateUserAsync(Guid userId, CancellationToken ct = default);
    Task ActivateUserAsync(Guid userId, CancellationToken ct = default);
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
