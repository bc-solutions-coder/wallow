using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Keeps setup open until an active membership holds a role granting
/// <see cref="PermissionType.AdminAccess"/>.
/// </summary>
public sealed class SetupStatusChecker(IdentityDbContext context) : ISetupStatusChecker
{
    public async Task<bool> IsSetupRequiredAsync(CancellationToken ct = default)
    {
        // Setup runs without a tenant and must find administrators across organizations.
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
