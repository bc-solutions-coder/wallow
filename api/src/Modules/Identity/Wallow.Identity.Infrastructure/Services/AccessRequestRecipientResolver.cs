using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class AccessRequestRecipientResolver(IdentityDbContext dbContext) : IAccessRequestRecipientResolver
{
    /// <summary>
    /// The address the organization nominated, else the people who can actually act on the
    /// request. An organization with neither yields nothing: the pending membership is the
    /// durable record, so there is nothing here worth failing a join over.
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default)
    {
        OrganizationId orgId = OrganizationId.Create(organizationId);

        // Every read here runs at authorize time, before a tenant is resolved, so the filter
        // would hide the only rows that matter.
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
