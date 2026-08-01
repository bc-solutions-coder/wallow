using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Memberships;

/// <summary>
/// Role resolution is per (user, organization): a role granted by one organization confers
/// nothing in another, and only an Active membership resolves anything at all.
/// </summary>
[Trait("Category", "Integration")]
public class MembershipRoleResolverTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IMembershipRepository Repository => ScopedServices.GetRequiredService<IMembershipRepository>();

    private IMembershipRoleResolver Resolver => ScopedServices.GetRequiredService<IMembershipRoleResolver>();

    private async Task<Guid> SeededRoleIdAsync(string roleName)
    {
        RoleManager<WallowRole> roleManager = ScopedServices.GetRequiredService<RoleManager<WallowRole>>();
        WallowRole? role = await roleManager.FindByNameAsync(roleName);
        role.Should().NotBeNull();
        return role!.Id;
    }

    [Fact]
    public async Task GetRoleNamesAsync_GrantsAnOrganizationsRoles_OnlyInThatOrganization()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId adminOrg = OrganizationId.New();
        OrganizationId visitorOrg = OrganizationId.New();

        Repository.Add(Membership.Enroll(userId, adminOrg, await SeededRoleIdAsync("admin"), TimeProvider.System));
        Repository.Add(Membership.Enroll(userId, visitorOrg, await SeededRoleIdAsync("user"), TimeProvider.System));
        await Repository.SaveChangesAsync();

        IReadOnlyList<string> inAdminOrg = await Resolver.GetRoleNamesAsync(userId, adminOrg.Value);
        IReadOnlyList<string> inVisitorOrg = await Resolver.GetRoleNamesAsync(userId, visitorOrg.Value);

        inAdminOrg.Should().Equal(["admin"]);
        inVisitorOrg.Should().Equal(["user"]);
    }

    [Fact]
    public async Task GetRoleNamesAsync_IsEmpty_WhenThereIsNoMembership()
    {
        IReadOnlyList<string> roles = await Resolver.GetRoleNamesAsync(Guid.NewGuid(), Guid.NewGuid());

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoleNamesAsync_IsEmpty_ForAPendingMembership()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId orgId = OrganizationId.New();

        Repository.Add(Membership.RequestAccess(userId, orgId, TimeProvider.System));
        await Repository.SaveChangesAsync();

        IReadOnlyList<string> roles = await Resolver.GetRoleNamesAsync(userId, orgId.Value);

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoleNamesAsync_IsEmpty_ForASuspendedMembership_EvenThoughItKeptItsRoles()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId orgId = OrganizationId.New();
        Guid roleId = await SeededRoleIdAsync("admin");

        Membership membership = Membership.Enroll(userId, orgId, roleId, TimeProvider.System);
        membership.Suspend(Guid.NewGuid(), TimeProvider.System);
        Repository.Add(membership);
        await Repository.SaveChangesAsync();

        IReadOnlyList<string> roles = await Resolver.GetRoleNamesAsync(userId, orgId.Value);

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoleNamesAsync_ReturnsEveryRoleOnTheMembership()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId orgId = OrganizationId.New();

        Membership membership = Membership.Enroll(
            userId, orgId, await SeededRoleIdAsync("user"), TimeProvider.System);
        membership.AssignRole(await SeededRoleIdAsync("manager"), Guid.NewGuid(), TimeProvider.System);
        Repository.Add(membership);
        await Repository.SaveChangesAsync();

        IReadOnlyList<string> roles = await Resolver.GetRoleNamesAsync(userId, orgId.Value);

        roles.Should().BeEquivalentTo(["user", "manager"]);
    }
}
