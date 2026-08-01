using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Mfa;

/// <summary>
/// The gate that lets a cookie login skip the MFA challenge. It runs before any organization is
/// known, so it has to hold for every organization the session can go on to get a token for: the
/// strictest Active membership decides, and one organization asking for a second factor is enough.
/// </summary>
[Trait("Category", "Integration")]
public class MfaExemptionCheckerTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IdentityDbContext DbContext => ScopedServices.GetRequiredService<IdentityDbContext>();

    private IMembershipRepository Repository => ScopedServices.GetRequiredService<IMembershipRepository>();

    private IMfaExemptionChecker Checker => ScopedServices.GetRequiredService<IMfaExemptionChecker>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AMemberOfOneStrictOrganization_IsNotExempt(bool strictFirst)
    {
        OrganizationId strict = await OrganizationAsync(requireMfa: true, allowPasswordlessLogin: false);
        OrganizationId lax = await OrganizationAsync(requireMfa: false, allowPasswordlessLogin: true);

        WallowUser user = PasswordlessUser();
        await EnrollAsync(user.Id, strictFirst ? [strict, lax] : [lax, strict]);

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeFalse();
    }

    [Fact]
    public async Task APasswordlessMemberOfLaxOrganizationsOnly_IsExempt()
    {
        OrganizationId first = await OrganizationAsync(requireMfa: false, allowPasswordlessLogin: true);
        OrganizationId second = await OrganizationAsync(requireMfa: false, allowPasswordlessLogin: true);

        WallowUser user = PasswordlessUser();
        await EnrollAsync(user.Id, [first, second]);

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeTrue();
    }

    [Fact]
    public async Task AMemberWhoStillHasAPassword_IsNotExempt()
    {
        OrganizationId lax = await OrganizationAsync(requireMfa: false, allowPasswordlessLogin: true);

        WallowUser user = NewUser();
        await EnrollAsync(user.Id, [lax]);

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeFalse();
    }

    [Fact]
    public async Task AUserWithNoMembership_IsNotExempt()
    {
        WallowUser user = PasswordlessUser();
        user.SetMfaGraceDeadline(DateTimeOffset.UtcNow.AddDays(30));

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeFalse();
    }

    [Fact]
    public async Task APendingMembership_GrantsNoExemption()
    {
        OrganizationId lax = await OrganizationAsync(requireMfa: false, allowPasswordlessLogin: true);

        WallowUser user = PasswordlessUser();
        Repository.Add(Membership.RequestAccess(user.Id, lax, TimeProvider.System));
        await Repository.SaveChangesAsync();

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeFalse();
    }

    [Fact]
    public async Task ASuspendedMembership_GrantsNoExemption()
    {
        OrganizationId lax = await OrganizationAsync(requireMfa: false, allowPasswordlessLogin: true);

        WallowUser user = PasswordlessUser();
        Membership membership = Membership.Enroll(user.Id, lax, await SeededUserRoleIdAsync(), TimeProvider.System);
        membership.Suspend(Guid.NewGuid(), TimeProvider.System);
        Repository.Add(membership);
        await Repository.SaveChangesAsync();

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeFalse();
    }

    [Fact]
    public async Task AnOrganizationThatStatesNoPolicy_GrantsNoExemption()
    {
        OrganizationId lax = await OrganizationAsync(requireMfa: false, allowPasswordlessLogin: true);
        OrganizationId silent = await OrganizationWithoutSettingsAsync();

        WallowUser user = PasswordlessUser();
        await EnrollAsync(user.Id, [lax, silent]);

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeFalse();
    }

    [Theory]
    [InlineData(14, true)]
    [InlineData(0, false)]
    public async Task AGraceDeadline_CountsOnlyWhereTheOrganizationOffersOne(int gracePeriodDays, bool expected)
    {
        OrganizationId strict = await OrganizationAsync(
            requireMfa: true, allowPasswordlessLogin: false, mfaGracePeriodDays: gracePeriodDays);

        WallowUser user = NewUser();
        user.SetMfaGraceDeadline(DateTimeOffset.UtcNow.AddDays(7));
        await EnrollAsync(user.Id, [strict]);

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().Be(expected);
    }

    [Fact]
    public async Task AnExpiredGraceDeadline_GrantsNoExemption()
    {
        OrganizationId strict = await OrganizationAsync(
            requireMfa: true, allowPasswordlessLogin: false, mfaGracePeriodDays: 14);

        WallowUser user = NewUser();
        await EnrollAsync(user.Id, [strict]);

        bool exempt = await Checker.IsExemptAsync(user, CancellationToken.None);

        exempt.Should().BeFalse();
    }

    private static WallowUser NewUser()
    {
        return WallowUser.Create(
            Guid.NewGuid(), "Mfa", "Subject", $"mfa-{Guid.NewGuid():N}@wallow.dev", TimeProvider.System);
    }

    private static WallowUser PasswordlessUser()
    {
        WallowUser user = NewUser();
        user.SetPasswordless();
        return user;
    }

    private async Task<Guid> SeededUserRoleIdAsync()
    {
        RoleManager<WallowRole> roleManager = ScopedServices.GetRequiredService<RoleManager<WallowRole>>();
        WallowRole? role = await roleManager.FindByNameAsync("user");
        role.Should().NotBeNull();
        return role!.Id;
    }

    private async Task<OrganizationId> OrganizationWithoutSettingsAsync()
    {
        Organization organization = Organization.Create(
            TenantId.Create(Guid.NewGuid()),
            $"Org {Guid.NewGuid():N}",
            $"org-{Guid.NewGuid():N}",
            Guid.Empty,
            TimeProvider.System);

        DbContext.Organizations.Add(organization);
        await DbContext.SaveChangesAsync();

        return organization.Id;
    }

    private async Task<OrganizationId> OrganizationAsync(
        bool requireMfa,
        bool allowPasswordlessLogin,
        int mfaGracePeriodDays = 0)
    {
        Organization organization = Organization.Create(
            TenantId.Create(Guid.NewGuid()),
            $"Org {Guid.NewGuid():N}",
            $"org-{Guid.NewGuid():N}",
            Guid.Empty,
            TimeProvider.System);

        DbContext.Organizations.Add(organization);
        DbContext.OrganizationSettings.Add(OrganizationSettings.Create(
            organization.Id,
            organization.TenantId,
            requireMfa,
            allowPasswordlessLogin,
            mfaGracePeriodDays,
            Guid.Empty,
            TimeProvider.System));
        await DbContext.SaveChangesAsync();

        return organization.Id;
    }

    private async Task EnrollAsync(Guid userId, IEnumerable<OrganizationId> organizationIds)
    {
        Guid roleId = await SeededUserRoleIdAsync();
        foreach (OrganizationId organizationId in organizationIds)
        {
            Repository.Add(Membership.Enroll(userId, organizationId, roleId, TimeProvider.System));
        }

        await Repository.SaveChangesAsync();
    }
}
