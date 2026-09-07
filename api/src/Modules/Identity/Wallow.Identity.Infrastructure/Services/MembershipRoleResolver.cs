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

        // Resolve assignment IDs against the global role catalog.
        List<string> names = await context.Roles
            .IgnoreQueryFilters()
            .Where(r => roleIds.Contains(r.Id) && r.Name != null)
            .Select(r => r.Name!)
            .ToListAsync(ct);

        return names;
    }
}
