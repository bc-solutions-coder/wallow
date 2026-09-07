using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class OrganizationAdminEmailResolver(IdentityDbContext dbContext) : IOrganizationAdminEmailResolver
{
    /// <summary>
    /// Resolves active owners and active members with the admin role.
    /// Uses member addresses rather than the nominated access-request address.
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        // Match the stored normalized role name directly.
        List<Guid> adminRoleIds = await dbContext.Roles
            .IgnoreQueryFilters()
            .Where(r => r.NormalizedName == "ADMIN")
            .Select(r => r.Id)
            .ToListAsync(ct);

        // Resolve recipients in the addressed organization, independently of the caller's tenant.
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
