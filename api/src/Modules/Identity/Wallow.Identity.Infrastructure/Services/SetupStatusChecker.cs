using Microsoft.AspNetCore.Identity;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class SetupStatusChecker(
    RoleManager<WallowRole> roleManager,
    UserManager<WallowUser> userManager) : ISetupStatusChecker
{
    public async Task<bool> IsSetupRequiredAsync(CancellationToken ct = default)
    {
        WallowRole? adminRole = await roleManager.FindByNameAsync("admin");
        if (adminRole is null)
        {
            return true;
        }

        IList<WallowUser> admins = await userManager.GetUsersInRoleAsync("admin");
        return admins.Count == 0;
    }
}
