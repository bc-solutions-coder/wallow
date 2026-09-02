using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class LastOwnerGuard(IdentityDbContext dbContext) : ILastOwnerGuard
{
    public Task ExecuteDepartureAsync(
        Guid organizationId,
        Guid departingUserId,
        Func<CancellationToken, Task> departure,
        CancellationToken ct = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            ct,
            (token) => RunAsync(organizationId, departingUserId, departure, token));
    }

    private async Task RunAsync(
        Guid organizationId,
        Guid departingUserId,
        Func<CancellationToken, Task> departure,
        CancellationToken ct)
    {
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(ct);

        // FOR UPDATE rather than a bare count: it holds the organization's active-owner rows for the
        // rest of this transaction, so a second departure blocks here instead of counting the owner
        // this one is about to take away. Under READ COMMITTED the waiter then re-reads the rows it
        // blocked on and sees the smaller set, which is what makes the count below trustworthy.
        // Status is stored as its name, not its ordinal (MembershipConfiguration).
        await dbContext.Database.ExecuteSqlAsync(
            $"""
             SELECT 1 FROM identity.memberships
             WHERE organization_id = {organizationId} AND is_owner AND status = 'Active'
             FOR UPDATE
             """,
            ct);

        OrganizationId typedOrganizationId = OrganizationId.Create(organizationId);
        List<Guid> owners = await dbContext.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == typedOrganizationId
                && m.IsOwner
                && m.Status == MembershipStatus.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (owners.Count == 1 && owners[0] == departingUserId)
        {
            throw new BusinessRuleException(IdentityErrors.LastOwner);
        }

        await departure(ct);
        await transaction.CommitAsync(ct);
    }
}
