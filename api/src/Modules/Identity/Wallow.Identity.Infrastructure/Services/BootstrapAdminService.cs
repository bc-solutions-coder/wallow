using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Extensions;

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
        WallowUser user = WallowUser.Create(firstName, lastName, email, timeProvider);
        user.EmailConfirmed = true;

        IdentityResult result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user.Id;
    }

    /// <summary>
    /// Writes ASP.NET Identity's own user-role directory. It grants no authorization: roles are
    /// resolved from a membership of a specific organization, and bootstrap runs before any
    /// organization exists. What makes the seeded administrator an administrator is the
    /// membership client sync creates, or the global-admin claim <see cref="GrantGlobalAdminAsync"/>
    /// writes. The first administrator created through the setup wizard therefore still holds no
    /// permission anywhere; which organization they should belong to is an open product question.
    /// </summary>
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

    public async Task GrantGlobalAdminAsync(Guid userId, CancellationToken ct = default)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        IdentityResult result = await userManager.AddClaimAsync(
            user,
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "true"));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to grant global admin to user '{userId}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<bool> UserExistsAsync(string email, CancellationToken ct = default)
    {
        WallowUser? user = await userManager.FindByEmailAsync(email);
        return user is not null;
    }
}
