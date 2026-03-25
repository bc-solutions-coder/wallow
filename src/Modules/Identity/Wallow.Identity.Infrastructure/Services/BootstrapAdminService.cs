using Microsoft.AspNetCore.Identity;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class BootstrapAdminService(
    UserManager<WallowUser> userManager,
    RoleManager<WallowRole> roleManager,
    TimeProvider timeProvider) : IBootstrapAdminService
{
    public async Task EnsureRoleExistsAsync(string roleName, CancellationToken ct = default)
    {
        bool exists = await roleManager.RoleExistsAsync(roleName);
        if (!exists)
        {
            IdentityResult result = await roleManager.CreateAsync(new WallowRole { Name = roleName });
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }

    public async Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, CancellationToken ct = default)
    {
        // Bootstrap admin uses Guid.Empty as tenant — the admin exists outside tenant boundaries
        WallowUser user = WallowUser.Create(Guid.Empty, firstName, lastName, email, timeProvider);
        user.EmailConfirmed = true;

        IdentityResult result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user.Id;
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        IdentityResult result = await userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign role '{roleName}' to user '{userId}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<bool> UserExistsAsync(string email, CancellationToken ct = default)
    {
        WallowUser? user = await userManager.FindByEmailAsync(email);
        return user is not null;
    }
}
