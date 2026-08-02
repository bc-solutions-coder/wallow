using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Memberships;

/// <summary>
/// Every membership transition reaches the audit table with the actor on it. The service publishes
/// and a Wolverine handler writes, so the two halves only meet in a running host: a unit test of
/// either one passes with the other missing.
///
/// The local queue is buffered, so the write lands after the call returns and every assertion polls.
/// </summary>
[Trait("Category", "Integration")]
public class MembershipTransitionAuditTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task ApproveAsync_AuditsWhoLetThePersonIn()
    {
        (Guid orgId, Guid userId) = await GivenPendingRequestAsync();
        Guid actorId = Guid.NewGuid();

        await ReviewService.ApproveAsync(orgId, userId, actorId);

        await ThenAuditedAsync("MembershipApproved", orgId, userId, actorId);
    }

    [Fact]
    public async Task DenyAsync_AuditsWhoRefused()
    {
        (Guid orgId, Guid userId) = await GivenPendingRequestAsync();
        Guid actorId = Guid.NewGuid();

        await ReviewService.DenyAsync(orgId, userId, actorId);

        await ThenAuditedAsync("MembershipDenied", orgId, userId, actorId);
    }

    [Fact]
    public async Task SuspendAsync_AuditsWhoTookTheAccessAway()
    {
        (Guid orgId, Guid userId) = await GivenActiveMemberAsync();
        Guid actorId = Guid.NewGuid();

        await ReviewService.SuspendAsync(orgId, userId, actorId);

        await ThenAuditedAsync("MembershipSuspended", orgId, userId, actorId);
    }

    [Fact]
    public async Task ReinstateAsync_AuditsWhoGaveTheAccessBack()
    {
        (Guid orgId, Guid userId) = await GivenActiveMemberAsync();
        Guid actorId = Guid.NewGuid();
        await ReviewService.SuspendAsync(orgId, userId, Guid.NewGuid());

        await ReviewService.ReinstateAsync(orgId, userId, actorId);

        await ThenAuditedAsync("MembershipReinstated", orgId, userId, actorId);
    }

    /// <summary>
    /// Leaving has no reviewer, so the actor is the leaver. That is the record, not a placeholder:
    /// "nobody" would make a departure indistinguishable from a removal with the actor lost.
    /// </summary>
    [Fact]
    public async Task LeaveAsync_AuditsTheLeaverAsTheirOwnActor()
    {
        (Guid orgId, Guid userId) = await GivenActiveMemberAsync();

        await ReviewService.LeaveAsync(orgId, userId);

        await ThenAuditedAsync("MembershipLeft", orgId, userId, userId);
    }

    private IMembershipReviewService ReviewService =>
        ScopedServices.GetRequiredService<IMembershipReviewService>();

    private async Task<(Guid OrgId, Guid UserId)> GivenPendingRequestAsync()
    {
        (Guid orgId, Guid userId, IdentityDbContext dbContext) = await GivenOrganizationAsync();

        dbContext.Memberships.Add(Membership.RequestAccess(
            userId, OrganizationId.Create(orgId), TimeProvider.System));
        await dbContext.SaveChangesAsync();

        return (orgId, userId);
    }

    private async Task<(Guid OrgId, Guid UserId)> GivenActiveMemberAsync()
    {
        (Guid orgId, Guid userId, IdentityDbContext dbContext) = await GivenOrganizationAsync();

        Guid roleId = await dbContext.Roles.IgnoreQueryFilters().Select(r => r.Id).FirstAsync();
        dbContext.Memberships.Add(Membership.Enroll(
            userId, OrganizationId.Create(orgId), roleId, TimeProvider.System));
        await dbContext.SaveChangesAsync();

        return (orgId, userId);
    }

    private async Task<(Guid OrgId, Guid UserId, IdentityDbContext DbContext)> GivenOrganizationAsync()
    {
        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();

        Organization organization = Organization.Create(
            default,
            $"Audit {Guid.NewGuid():N}",
            $"audit-{Guid.NewGuid():N}",
            Guid.NewGuid(),
            TimeProvider.System);
        dbContext.SetTenant(organization.TenantId);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        return (organization.Id.Value, Guid.NewGuid(), dbContext);
    }

    private async Task ThenAuditedAsync(string eventType, Guid orgId, Guid userId, Guid actorId)
    {
        AuthAuditEntry? entry = null;

        for (int attempt = 0; attempt < 50 && entry is null; attempt++)
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            AuthAuditDbContext auditContext = scope.ServiceProvider.GetRequiredService<AuthAuditDbContext>();

            entry = await auditContext.AuthAuditEntries
                .FirstOrDefaultAsync(e => e.UserId == userId && e.EventType == eventType);

            if (entry is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        entry.Should().NotBeNull($"the {eventType} transition should have been audited");
        entry!.ActorId.Should().Be(actorId);
        entry.TenantId.Should().Be(orgId);
    }
}
