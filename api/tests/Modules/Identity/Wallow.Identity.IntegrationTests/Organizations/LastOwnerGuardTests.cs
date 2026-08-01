using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Domain;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// An organization must never be left with no active owner. Two owners are two aggregates, so the
/// rule cannot be checked inside either one: the interesting case is two departures happening at
/// once, each counting the owner the other is about to take away.
///
/// Backend-dependent by necessity. What makes the count trustworthy is a FOR UPDATE row lock held
/// across a transaction, and no in-memory provider has one — run against anything but real Postgres
/// these tests would assert the mechanism they exist to prove is absent.
/// </summary>
[Trait("Category", "Integration")]
public class LastOwnerGuardTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task ADepartureThatWouldLeaveNoActiveOwner_IsRefused()
    {
        (Guid orgId, Guid firstOwner, Guid secondOwner) = await GivenOrganizationWithTwoOwnersAsync();
        ILastOwnerGuard guard = ScopedServices.GetRequiredService<ILastOwnerGuard>();
        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();

        await guard.ExecuteDepartureAsync(
            orgId, firstOwner, token => RemoveMembershipAsync(dbContext, orgId, firstOwner, token));

        Func<Task> lastOne = () => guard.ExecuteDepartureAsync(
            orgId, secondOwner, token => RemoveMembershipAsync(dbContext, orgId, secondOwner, token));

        await lastOne.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "Identity.LastOwner");
        (await CountActiveOwnersAsync(orgId)).Should().Be(1);
    }

    [Fact]
    public async Task ADepartureThatLeavesAnotherActiveOwner_GoesThrough()
    {
        (Guid orgId, Guid firstOwner, _) = await GivenOrganizationWithTwoOwnersAsync();
        ILastOwnerGuard guard = ScopedServices.GetRequiredService<ILastOwnerGuard>();
        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();

        await guard.ExecuteDepartureAsync(
            orgId, firstOwner, token => RemoveMembershipAsync(dbContext, orgId, firstOwner, token));

        (await CountActiveOwnersAsync(orgId)).Should().Be(1);
    }

    /// <summary>
    /// The case a count taken before the write cannot survive: both owners leave at once, each on
    /// its own connection, and the second is still inside the first one's open transaction when it
    /// takes its count. Without the row lock it reads two owners and both departures commit.
    /// </summary>
    [Fact]
    public async Task TwoOwnersLeavingAtOnce_LeaveOneBehind()
    {
        (Guid orgId, Guid firstOwner, Guid secondOwner) = await GivenOrganizationWithTwoOwnersAsync();

        using IServiceScope firstScope = Factory.Services.CreateScope();
        using IServiceScope secondScope = Factory.Services.CreateScope();
        TaskCompletionSource insideFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task first = firstScope.ServiceProvider.GetRequiredService<ILastOwnerGuard>()
            .ExecuteDepartureAsync(orgId, firstOwner, async token =>
            {
                insideFirst.SetResult();
                await releaseFirst.Task;
                await RemoveMembershipAsync(
                    firstScope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
                    orgId, firstOwner, token);
            });

        await insideFirst.Task;

        Task second = secondScope.ServiceProvider.GetRequiredService<ILastOwnerGuard>()
            .ExecuteDepartureAsync(orgId, secondOwner, token => RemoveMembershipAsync(
                secondScope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
                orgId, secondOwner, token));

        // Long enough for the second departure to reach the lock and block on it. Shorter, and the
        // test still passes for the uninteresting reason that the first one had already committed.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        releaseFirst.SetResult();

        await first;
        Func<Task> secondDeparture = () => second;
        await secondDeparture.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "Identity.LastOwner");
        (await CountActiveOwnersAsync(orgId)).Should().Be(1);
    }

    private async Task<(Guid OrgId, Guid FirstOwner, Guid SecondOwner)> GivenOrganizationWithTwoOwnersAsync()
    {
        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();

        Organization organization = Organization.Create(
            default, $"Owners {Guid.NewGuid():N}", $"owners-{Guid.NewGuid():N}", Guid.NewGuid(), TimeProvider.System);
        dbContext.SetTenant(organization.TenantId);
        dbContext.Organizations.Add(organization);

        Guid roleId = await dbContext.Roles.IgnoreQueryFilters()
            .Select(r => r.Id)
            .FirstAsync();

        Guid firstOwner = Guid.NewGuid();
        Guid secondOwner = Guid.NewGuid();
        foreach (Guid ownerId in new[] { firstOwner, secondOwner })
        {
            Membership membership = Membership.Enroll(
                ownerId, organization.Id, roleId, TimeProvider.System);
            membership.MarkOwner(true, ownerId, TimeProvider.System);
            dbContext.Memberships.Add(membership);
        }

        await dbContext.SaveChangesAsync();
        return (organization.Id.Value, firstOwner, secondOwner);
    }

    private static async Task RemoveMembershipAsync(
        IdentityDbContext dbContext, Guid orgId, Guid userId, CancellationToken ct)
    {
        OrganizationId typedOrgId = OrganizationId.Create(orgId);
        Membership membership = await dbContext.Memberships
            .AsTracking()
            .IgnoreQueryFilters()
            .Include(m => m.Roles)
            .FirstAsync(m => m.UserId == userId && m.OrganizationId == typedOrgId, ct);

        dbContext.Memberships.Remove(membership);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<int> CountActiveOwnersAsync(Guid orgId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        OrganizationId typedOrgId = OrganizationId.Create(orgId);

        return await dbContext.Memberships
            .IgnoreQueryFilters()
            .CountAsync(m => m.OrganizationId == typedOrgId
                && m.IsOwner
                && m.Status == MembershipStatus.Active);
    }
}
