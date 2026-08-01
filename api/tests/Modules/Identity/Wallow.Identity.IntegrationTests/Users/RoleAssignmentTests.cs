using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Users;

/// <summary>
/// Role writes land on the membership of one organization. A grant in org A confers nothing in
/// org B, and a revocation reaches only the organization it named. The service refuses to write
/// where there is no membership, so a grant can never double as an enrollment.
/// </summary>
[Trait("Category", "Integration")]
public class RoleAssignmentTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    /// <summary>
    /// Built by hand: the test host registers a no-op fake for <see cref="IUserManagementService"/>,
    /// so resolving the interface would assert nothing.
    /// </summary>
    private UserManagementService UserManagement =>
        ActivatorUtilities.CreateInstance<UserManagementService>(ScopedServices);

    private IMembershipRoleResolver Resolver => ScopedServices.GetRequiredService<IMembershipRoleResolver>();

    private IMembershipRepository Repository => ScopedServices.GetRequiredService<IMembershipRepository>();

    [Fact]
    public async Task AGrantInOneOrganization_ResolvesThereAndNowhereElse()
    {
        Guid userId = await UserAsync();
        Guid orgA = Guid.NewGuid();
        Guid orgB = Guid.NewGuid();
        await EnrollAsync(userId, orgA);
        await EnrollAsync(userId, orgB);

        await UserManagement.AssignRoleAsync(userId, orgA, "admin", CancellationToken.None);

        IReadOnlyList<string> inA = await Resolver.GetRoleNamesAsync(userId, orgA, CancellationToken.None);
        IReadOnlyList<string> inB = await Resolver.GetRoleNamesAsync(userId, orgB, CancellationToken.None);

        inA.Should().Contain("admin");
        inB.Should().NotContain("admin");
        inB.Should().Contain("user");
    }

    [Fact]
    public async Task ARevocation_RemovesTheRoleTheResolverReturns()
    {
        Guid userId = await UserAsync();
        Guid organizationId = Guid.NewGuid();
        await EnrollAsync(userId, organizationId);
        await UserManagement.AssignRoleAsync(userId, organizationId, "admin", CancellationToken.None);

        await UserManagement.RemoveRoleAsync(userId, organizationId, "admin", CancellationToken.None);

        IReadOnlyList<string> roles = await Resolver.GetRoleNamesAsync(userId, organizationId, CancellationToken.None);

        roles.Should().NotContain("admin");
        roles.Should().Contain("user");
    }

    [Fact]
    public async Task ARevocationInOneOrganization_LeavesTheSameRoleStandingInAnother()
    {
        Guid userId = await UserAsync();
        Guid orgA = Guid.NewGuid();
        Guid orgB = Guid.NewGuid();
        await EnrollAsync(userId, orgA);
        await EnrollAsync(userId, orgB);
        await UserManagement.AssignRoleAsync(userId, orgA, "admin", CancellationToken.None);
        await UserManagement.AssignRoleAsync(userId, orgB, "admin", CancellationToken.None);

        await UserManagement.RemoveRoleAsync(userId, orgA, "admin", CancellationToken.None);

        IReadOnlyList<string> inA = await Resolver.GetRoleNamesAsync(userId, orgA, CancellationToken.None);
        IReadOnlyList<string> inB = await Resolver.GetRoleNamesAsync(userId, orgB, CancellationToken.None);

        inA.Should().NotContain("admin");
        inB.Should().Contain("admin");
    }

    [Fact]
    public async Task AGrantWithoutAMembership_IsRefused()
    {
        Guid userId = await UserAsync();

        Func<Task> act = () => UserManagement.AssignRoleAsync(
            userId, Guid.NewGuid(), "admin", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a member*");
    }

    [Fact]
    public async Task ReadingRolesForAnOrganizationTheUserDoesNotBelongTo_ReturnsEmpty()
    {
        Guid userId = await UserAsync();
        await EnrollAsync(userId, Guid.NewGuid());

        IReadOnlyList<string> roles = await UserManagement.GetUserRolesAsync(
            userId, Guid.NewGuid(), CancellationToken.None);

        roles.Should().BeEmpty();
    }

    private async Task<Guid> UserAsync()
    {
        UserManager<WallowUser> userManager = ScopedServices.GetRequiredService<UserManager<WallowUser>>();
        WallowUser user = WallowUser.Create(
            Guid.NewGuid(), "Role", "Subject", $"role-{Guid.NewGuid():N}@wallow.dev", TimeProvider.System);

        IdentityResult result = await userManager.CreateAsync(user);
        result.Succeeded.Should().BeTrue();

        return user.Id;
    }

    private async Task EnrollAsync(Guid userId, Guid organizationId)
    {
        RoleManager<WallowRole> roleManager = ScopedServices.GetRequiredService<RoleManager<WallowRole>>();
        WallowRole? role = await roleManager.FindByNameAsync("user");
        role.Should().NotBeNull();

        Repository.Add(Membership.Enroll(
            userId, OrganizationId.Create(organizationId), role!.Id, TimeProvider.System));

        await Repository.SaveChangesAsync();
    }
}
