using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Decides whether the <c>[AllowAnonymous]</c> setup endpoints stay open. "An administrator
/// exists" means an Active membership holding a role that grants <see cref="PermissionType.AdminAccess"/>
/// — the same thing authorization reads, and the only role directory this schema has.
/// </summary>
public sealed class SetupStatusChecker(IdentityDbContext context) : ISetupStatusChecker
{
    public async Task<bool> IsSetupRequiredAsync(CancellationToken ct = default)
    {
        // Roles are a global catalog seeded outside any tenant, and this runs unauthenticated
        // with no ambient tenant resolved, so both reads bypass the tenant filters.
        List<WallowRole> roles = await context.Roles
            .IgnoreQueryFilters()
            .Where(r => r.Name != null)
            .ToListAsync(ct);

        List<Guid> adminRoleIds =
        [
            .. roles
                .Where(r => RolePermissionMapping.GetPermissions([r.Name!])
                    .Contains(PermissionType.AdminAccess, StringComparer.Ordinal))
                .Select(r => r.Id)
        ];

        if (adminRoleIds.Count == 0)
        {
            return true;
        }

        bool administratorExists = await context.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.Status == MembershipStatus.Active)
            .SelectMany(m => m.Roles)
            .AnyAsync(r => adminRoleIds.Contains(r.RoleId), ct);

        return !administratorExists;
    }
}
