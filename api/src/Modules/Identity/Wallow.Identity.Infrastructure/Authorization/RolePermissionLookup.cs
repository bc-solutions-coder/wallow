using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Authorization;

public sealed class RolePermissionLookup : IRolePermissionLookup
{
    public IReadOnlyCollection<string> GetPermissions(IEnumerable<string> roles)
    {
        return RolePermissionMapping.GetPermissions(roles).ToArray();
    }
}
