using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Contracts.Identity;

namespace Foundry.Identity.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IKeycloakAdminService _keycloakAdmin;

    public UserService(IKeycloakAdminService keycloakAdmin)
    {
        _keycloakAdmin = keycloakAdmin;
    }

    public async Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        UserDto? user = await _keycloakAdmin.GetUserByIdAsync(userId, ct);

        if (user == null)
        {
            return null;
        }

        return new UserInfo(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Enabled);
    }

    public async Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        UserDto? user = await _keycloakAdmin.GetUserByEmailAsync(email, ct);

        if (user == null)
        {
            return null;
        }

        return new UserInfo(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Enabled);
    }
}
