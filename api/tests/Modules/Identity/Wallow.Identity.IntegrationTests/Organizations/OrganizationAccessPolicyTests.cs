using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// The gate every organization-scoped endpoint consults for an organization outside the caller's
/// own tenant. It answers one permission at a time, so read reach and destroy reach are separate
/// answers, and a bare membership carrying no useful role reaches nothing.
/// </summary>
[Trait("Category", "Integration")]
public class OrganizationAccessPolicyTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IMembershipRepository Repository => ScopedServices.GetRequiredService<IMembershipRepository>();

    private IOrganizationAccessPolicy Policy => ScopedServices.GetRequiredService<IOrganizationAccessPolicy>();

    private async Task<Guid> SeededRoleIdAsync(string roleName)
    {
        RoleManager<WallowRole> roleManager = ScopedServices.GetRequiredService<RoleManager<WallowRole>>();
        WallowRole? role = await roleManager.FindByNameAsync(roleName);
        role.Should().NotBeNull();
        return role!.Id;
    }

    private async Task<(Guid UserId, Guid OrgId)> EnrolledAsync(string roleName)
    {
        Guid userId = Guid.NewGuid();
        OrganizationId orgId = OrganizationId.New();

        Repository.Add(Membership.Enroll(userId, orgId, await SeededRoleIdAsync(roleName), TimeProvider.System));
        await Repository.SaveChangesAsync();

        return (userId, orgId.Value);
    }

    [Theory]
    [InlineData(PermissionType.OrganizationsRead, true)]
    [InlineData(PermissionType.OrganizationsManageMembers, true)]
    [InlineData(PermissionType.OrganizationsUpdate, true)]
    [InlineData(PermissionType.OrganizationClientsManage, true)]
    [InlineData(PermissionType.AdminAccess, true)]
    public async Task AnAdminMember_HoldsTheOrganizationsAdministrativePermissions(string permission, bool expected)
    {
        (Guid userId, Guid orgId) = await EnrolledAsync("admin");

        bool granted = await Policy.HasPermissionInOrganizationAsync(orgId, userId, permission);

        granted.Should().Be(expected);
    }

    [Theory]
    [InlineData(PermissionType.OrganizationsRead, true)]
    [InlineData(PermissionType.OrganizationsManageMembers, true)]
    [InlineData(PermissionType.OrganizationClientsManage, true)]
    [InlineData(PermissionType.AdminAccess, false)]
    public async Task AManagerMember_ManagesMembersWithoutAdministeringTheOrganization(string permission, bool expected)
    {
        (Guid userId, Guid orgId) = await EnrolledAsync("manager");

        bool granted = await Policy.HasPermissionInOrganizationAsync(orgId, userId, permission);

        granted.Should().Be(expected);
    }

    [Theory]
    [InlineData(PermissionType.OrganizationsRead, true)]
    [InlineData(PermissionType.OrganizationsManageMembers, false)]
    [InlineData(PermissionType.OrganizationClientsManage, false)]
    [InlineData(PermissionType.AdminAccess, false)]
    public async Task AUserMember_ReadsWithoutReachingTheMemberLifecycle(string permission, bool expected)
    {
        (Guid userId, Guid orgId) = await EnrolledAsync("user");

        bool granted = await Policy.HasPermissionInOrganizationAsync(orgId, userId, permission);

        granted.Should().Be(expected);
    }

    [Fact]
    public async Task AnAdminOfOneOrganization_HoldsNothingInAnother()
    {
        (Guid userId, Guid _) = await EnrolledAsync("admin");
        Guid unrelatedOrgId = OrganizationId.New().Value;

        bool granted = await Policy.HasPermissionInOrganizationAsync(
            unrelatedOrgId, userId, PermissionType.OrganizationsRead);

        granted.Should().BeFalse();
    }

    [Fact]
    public async Task ANonMember_HoldsNothing()
    {
        bool granted = await Policy.HasPermissionInOrganizationAsync(
            OrganizationId.New().Value, Guid.NewGuid(), PermissionType.OrganizationsRead);

        granted.Should().BeFalse();
    }

    [Fact]
    public async Task APendingMember_HoldsNothing()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId orgId = OrganizationId.New();

        Repository.Add(Membership.RequestAccess(userId, orgId, TimeProvider.System));
        await Repository.SaveChangesAsync();

        bool granted = await Policy.HasPermissionInOrganizationAsync(
            orgId.Value, userId, PermissionType.OrganizationsRead);

        granted.Should().BeFalse();
    }

    [Fact]
    public async Task ASuspendedMember_HoldsNothing()
    {
        Guid userId = Guid.NewGuid();
        OrganizationId orgId = OrganizationId.New();

        Membership membership = Membership.Enroll(
            userId, orgId, await SeededRoleIdAsync("admin"), TimeProvider.System);
        membership.Suspend(Guid.NewGuid(), TimeProvider.System);
        Repository.Add(membership);
        await Repository.SaveChangesAsync();

        bool granted = await Policy.HasPermissionInOrganizationAsync(
            orgId.Value, userId, PermissionType.OrganizationsRead);

        granted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NotAPermission")]
    public async Task AnUnrecognizedPermission_IsNeverGranted(string permission)
    {
        (Guid userId, Guid orgId) = await EnrolledAsync("admin");

        bool granted = await Policy.HasPermissionInOrganizationAsync(orgId, userId, permission);

        granted.Should().BeFalse();
    }
}
