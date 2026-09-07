using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class AccessRequestRecipientResolver(IdentityDbContext dbContext) : IAccessRequestRecipientResolver
{
    /// <summary>
    /// Uses the nominated address, otherwise active owners' email addresses.
    /// An empty result is valid; the saved membership remains the access-request record.
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        // Authorize-time callers may have no tenant; select settings by organization explicitly.
        string? nominated = await dbContext.OrganizationSettings
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == orgId)
            .Select(s => s.AccessRequestEmail)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(nominated))
        {
            return [nominated];
        }

        List<Guid> ownerUserIds = await dbContext.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == orgId && m.IsOwner && m.Status == MembershipStatus.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (ownerUserIds.Count == 0)
        {
            return [];
        }

        List<string> emails = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => ownerUserIds.Contains(u.Id) && u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync(ct);

        return emails;
    }
}
