using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;

namespace Foundry.Tests.Common.Fakes;

public sealed class FakeUserManagementService : IUserManagementService
{
    public Task<Guid> CreateUserAsync(string email, string firstName, string lastName, string? password = null, CancellationToken ct = default)
    {
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return Task.FromResult<UserDto?>(null);
    }

    public Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        return Task.FromResult<UserDto?>(null);
    }

    public Task<IReadOnlyList<UserDto>> GetUsersAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<UserDto>>(Array.Empty<UserDto>());
    }

    public Task DeactivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ActivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
