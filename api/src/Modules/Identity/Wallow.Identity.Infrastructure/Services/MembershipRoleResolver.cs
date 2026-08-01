using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class MembershipRoleResolver(
    IMembershipRepository memberships,
    IdentityDbContext context) : IMembershipRoleResolver
{
    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        Membership? membership = await memberships.GetAsync(userId, organizationId, ct);

        if (membership is null || !membership.IsActive)
        {
            return [];
        }

        List<Guid> roleIds = [.. membership.RoleIds];

        if (roleIds.Count == 0)
        {
            return [];
        }

        // The role catalog is global: roles are seeded with TenantId = Guid.Empty and are
        // addressed here by id, so no tenant scoping applies to the lookup.
        List<string> names = await context.Roles
            .IgnoreQueryFilters()
            .Where(r => roleIds.Contains(r.Id) && r.Name != null)
            .Select(r => r.Name!)
            .ToListAsync(ct);

        return names;
    }
}
