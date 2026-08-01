using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests.Invitations;

/// <summary>
/// One live token per address per organization. Revoke acts on a single invitation by id, so a
/// second token minted by a second click is one the admin cannot see in the list and cannot take
/// back — re-inviting refreshes the outstanding invitation instead. Inviting a sitting member is
/// refused outright.
///
/// Backend-dependent: requires the WallowApiFactory stack (Postgres + seeded identity data).
/// </summary>
[Trait("Category", "Integration")]
public class InvitationIssuanceTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IdentityDbContext DbContext => ScopedServices.GetRequiredService<IdentityDbContext>();

    private IMembershipRepository Memberships => ScopedServices.GetRequiredService<IMembershipRepository>();

    [Fact]
    public async Task InvitingTheSameAddressTwice_LeavesOneLiveToken()
    {
        Guid organizationId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        AuthenticateAsAdminOf(organizationId);

        string firstToken = await InviteAsync(email, HttpStatusCode.Created);
        string secondToken = await InviteAsync(email.ToUpperInvariant(), HttpStatusCode.Created);

        secondToken.Should().Be(firstToken);

        List<Invitation> issued = await DbContext.Invitations
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == TenantId.Create(organizationId))
            .ToListAsync();

        issued.Should().ContainSingle();
    }

    [Fact]
    public async Task ReInviting_PushesTheExpiryOut()
    {
        Guid organizationId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        AuthenticateAsAdminOf(organizationId);

        string token = await InviteAsync(email, HttpStatusCode.Created);
        DateTimeOffset first = await ExpiryOfAsync(token);

        await InviteAsync(email, HttpStatusCode.Created);

        (await ExpiryOfAsync(token)).Should().BeOnOrAfter(first);
    }

    [Fact]
    public async Task InvitingAnActiveMember_IsRefused()
    {
        Guid organizationId = Guid.NewGuid();
        string email = $"member-{Guid.NewGuid():N}@wallow.dev";
        await EnrollAsync(email, organizationId);
        AuthenticateAsAdminOf(organizationId);

        await InviteAsync(email, HttpStatusCode.UnprocessableEntity);

        bool any = await DbContext.Invitations
            .IgnoreQueryFilters()
            .AnyAsync(i => i.TenantId == TenantId.Create(organizationId));

        any.Should().BeFalse();
    }

    [Fact]
    public async Task InvitingSomeoneWhoBelongsToADifferentOrganization_IsAllowed()
    {
        string email = $"member-{Guid.NewGuid():N}@wallow.dev";
        await EnrollAsync(email, Guid.NewGuid());
        AuthenticateAsAdminOf(Guid.NewGuid());

        await InviteAsync(email, HttpStatusCode.Created);
    }

    private void AuthenticateAsAdminOf(Guid organizationId)
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");
        SetTestTenant(organizationId);
    }

    private async Task<string> InviteAsync(string email, HttpStatusCode expected)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/identity/invitations", new { email });
        response.StatusCode.Should().Be(expected);

        if (expected != HttpStatusCode.Created)
        {
            return string.Empty;
        }

        string location = response.Headers.Location!.ToString();
        return location[(location.LastIndexOf('/') + 1)..];
    }

    private async Task<DateTimeOffset> ExpiryOfAsync(string token)
    {
        Invitation invitation = await DbContext.Invitations
            .IgnoreQueryFilters()
            .SingleAsync(i => i.Token == token);

        return invitation.ExpiresAt;
    }

    private async Task EnrollAsync(string email, Guid organizationId)
    {
        UserManager<WallowUser> userManager = ScopedServices.GetRequiredService<UserManager<WallowUser>>();
        WallowUser user = WallowUser.Create("Sitting", "Member", email, TimeProvider.System);
        user.EmailConfirmed = true;
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();

        RoleManager<WallowRole> roleManager = ScopedServices.GetRequiredService<RoleManager<WallowRole>>();
        WallowRole? role = await roleManager.FindByNameAsync("user");

        Memberships.Add(Membership.Enroll(
            user.Id, OrganizationId.Create(organizationId), role!.Id, TimeProvider.System));

        await Memberships.SaveChangesAsync();
    }
}
