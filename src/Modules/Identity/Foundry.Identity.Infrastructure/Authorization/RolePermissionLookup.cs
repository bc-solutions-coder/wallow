using Foundry.Identity.Application.Interfaces;

namespace Foundry.Identity.Infrastructure.Authorization;

public sealed class RolePermissionLookup : IRolePermissionLookup
{
    public IReadOnlyCollection<string> GetPermissions(IEnumerable<string> roles)
    {
        return RolePermissionMapping.GetPermissions(roles).ToArray();
    }
}
