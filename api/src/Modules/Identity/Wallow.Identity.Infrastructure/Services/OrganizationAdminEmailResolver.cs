using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class OrganizationAdminEmailResolver(IdentityDbContext dbContext) : IOrganizationAdminEmailResolver
{
    /// <summary>
    /// The organization's active owners together with every active member holding the admin
    /// role — the people who administer the organization. The platform's suspension is exactly
    /// the kind of event they answer for, so no nominated address stands in for them here,
    /// unlike access requests.
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        // Identity's default normalizer upper-cases invariantly, so this matches what
        // RoleManager wrote without paying for a case-insensitive collation scan.
        List<Guid> adminRoleIds = await dbContext.Roles
            .IgnoreQueryFilters()
            .Where(r => r.NormalizedName == "ADMIN")
            .Select(r => r.Id)
            .ToListAsync(ct);

        // A global admin acts from outside the organization, so the tenant filter would hide
        // the only rows that matter.
        List<Guid> recipientUserIds = await dbContext.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active)
            .Where(m => m.IsOwner || m.Roles.Any(r => adminRoleIds.Contains(r.RoleId)))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (recipientUserIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => recipientUserIds.Contains(u.Id) && u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync(ct);
    }
}
