using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Memberships;

/// <summary>
/// The membership repository against real Postgres. The reads that run at authorize time
/// (<c>GetAsync</c>, <c>GetForUserAsync</c>) must resolve across organizations while the ambient
/// tenant is some other org — or a person who belongs to two organizations can never sign in to
/// the second one. <c>membership_roles.role_id</c> is a real FK, so every role id here is a
/// seeded role.
/// </summary>
[Trait("Category", "Integration")]
public class MembershipRepositoryTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IMembershipRepository Repository => ScopedServices.GetRequiredService<IMembershipRepository>();

    private async Task<Guid> SeededRoleIdAsync(string roleName)
    {
        RoleManager<WallowRole> roleManager = ScopedServices.GetRequiredService<RoleManager<WallowRole>>();
        WallowRole? role = await roleManager.FindByNameAsync(roleName);
        role.Should().NotBeNull();
        return role!.Id;
    }

    private void SetAmbientTenant(Guid tenantId)
    {
        IdentityDbContext context = ScopedServices.GetRequiredService<IdentityDbContext>();
        context.SetTenant(TenantId.Create(tenantId));
    }

    [Fact]
    public async Task GetAsync_ResolvesAMembership_WhileTheAmbientTenantIsADifferentOrganization()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId homeOrg = OrganizationId.New();
        OrganizationId otherOrg = OrganizationId.New();
        Guid roleId = await SeededRoleIdAsync("user");

        Repository.Add(Membership.Enroll(userId, homeOrg, roleId, TimeProvider.System));
        await Repository.SaveChangesAsync();

        SetAmbientTenant(otherOrg.Value);
        Membership? found = await Repository.GetAsync(userId, homeOrg.Value);

        found.Should().NotBeNull();
        found!.OrganizationId.Should().Be(homeOrg);
        found.RoleIds.Should().Equal([roleId]);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForAnOrganizationTheUserDoesNotBelongTo()
    {
        Guid userId = Guid.NewGuid();
        Guid roleId = await SeededRoleIdAsync("user");

        Repository.Add(Membership.Enroll(userId, OrganizationId.New(), roleId, TimeProvider.System));
        await Repository.SaveChangesAsync();

        Membership? found = await Repository.GetAsync(userId, Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetForUserAsync_SpansOrganizations()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId first = OrganizationId.New();
        OrganizationId second = OrganizationId.New();
        Guid roleId = await SeededRoleIdAsync("user");

        Repository.Add(Membership.Enroll(userId, first, roleId, TimeProvider.System));
        Repository.Add(Membership.RequestAccess(userId, second, TimeProvider.System));
        await Repository.SaveChangesAsync();

        SetAmbientTenant(Guid.NewGuid());
        IReadOnlyList<Membership> memberships = await Repository.GetForUserAsync(userId);

        memberships.Select(m => m.OrganizationId).Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public async Task GetForOrganizationAsync_FiltersByStatus()
    {
        OrganizationId orgId = OrganizationId.New();
        Guid activeUserId = Guid.NewGuid();
        Guid pendingUserId = Guid.NewGuid();
        Guid roleId = await SeededRoleIdAsync("user");

        Repository.Add(Membership.Enroll(activeUserId, orgId, roleId, TimeProvider.System));
        Repository.Add(Membership.RequestAccess(pendingUserId, orgId, TimeProvider.System));
        await Repository.SaveChangesAsync();

        IReadOnlyList<Membership> all = await Repository.GetForOrganizationAsync(orgId.Value);
        IReadOnlyList<Membership> pending =
            await Repository.GetForOrganizationAsync(orgId.Value, MembershipStatus.Pending);

        all.Select(m => m.UserId).Should().BeEquivalentTo([activeUserId, pendingUserId]);
        pending.Select(m => m.UserId).Should().Equal([pendingUserId]);
    }

    [Fact]
    public async Task Remove_DeletesTheMembershipAndItsRoleRows()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId orgId = OrganizationId.New();
        Guid roleId = await SeededRoleIdAsync("user");

        Repository.Add(Membership.Enroll(userId, orgId, roleId, TimeProvider.System));
        await Repository.SaveChangesAsync();

        Membership? membership = await Repository.GetAsync(userId, orgId.Value);
        Repository.Remove(membership!);
        await Repository.SaveChangesAsync();

        (await Repository.GetAsync(userId, orgId.Value)).Should().BeNull();
    }
}
