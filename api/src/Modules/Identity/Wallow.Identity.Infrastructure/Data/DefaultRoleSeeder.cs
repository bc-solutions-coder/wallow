using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Data;

public sealed partial class DefaultRoleSeeder(
    RoleManager<WallowRole> roleManager,
    ILogger<DefaultRoleSeeder> logger)
{
    private static readonly string[] _defaultRoles = ["admin", "manager", "user"];

    public async Task SeedAsync()
    {
        foreach (string roleName in _defaultRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            WallowRole role = new()
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
                TenantId = Guid.Empty
            };

            IdentityResult result = await roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                LogRoleSeeded(roleName);
            }
            else
            {
                LogRoleSeedFailed(roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded role: {RoleName}")]
    private partial void LogRoleSeeded(string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to seed role {RoleName}: {Errors}")]
    private partial void LogRoleSeedFailed(string roleName, string errors);
}
