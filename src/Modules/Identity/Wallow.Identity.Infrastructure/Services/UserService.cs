using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.Identity;

namespace Wallow.Identity.Infrastructure.Services;

public class UserService(IUserManagementService keycloakAdmin) : IUserService
{

    public async Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        UserDto? user = await keycloakAdmin.GetUserByIdAsync(userId, ct);

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
        UserDto? user = await keycloakAdmin.GetUserByEmailAsync(email, ct);

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
