using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Invitations;

/// <summary>
/// The two invitation paths that must reach OUTSIDE the ambient tenant. Verification is anonymous,
/// so no tenant resolves at all; acceptance runs as the invited person, whose current organization
/// is by definition not the one inviting them; and the expiry sweep runs from a background job with
/// no tenant either. Under the tenant query filter each of those matches nothing, so all three
/// bypass it — these facts fail if that bypass is removed.
///
/// Backend-dependent: requires the WallowApiFactory stack (Postgres + seeded identity data).
/// </summary>
[Trait("Category", "Integration")]
public class InvitationTenantReachTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IMembershipRepository Memberships => ScopedServices.GetRequiredService<IMembershipRepository>();

    private IInvitationRepository Invitations => ScopedServices.GetRequiredService<IInvitationRepository>();

    private IdentityDbContext DbContext => ScopedServices.GetRequiredService<IdentityDbContext>();

    [Fact]
    public async Task Verify_WithNoTenantResolvedAtAll_StillFindsTheInvitation()
    {
        Guid invitingOrgId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        Invitation invitation = await SeedInvitationAsync(invitingOrgId, email);

        // A bare client: the invited person clicks the link before signing in, so the request
        // carries no token and the middleware resolves no tenant.
        using HttpClient anonymous = Factory.CreateClient();
        HttpResponseMessage response = await anonymous.GetAsync(
            $"/identity/invitations/verify/{invitation.Token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        InvitationRow? row = await response.Content.ReadFromJsonAsync<InvitationRow>();
        row.Should().NotBeNull();
        row!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Accept_WhileSignedIntoADifferentOrganization_EnrollsThemInTheInvitingOne()
    {
        Guid invitingOrgId = Guid.NewGuid();
        Guid currentOrgId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        Invitation invitation = await SeedInvitationAsync(invitingOrgId, email);
        Guid userId = await RegisterAsync(email);

        SetTestUser(userId.ToString());
        SetTestTenant(currentOrgId);
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/invitations/{invitation.Token}/accept", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Membership? membership = await Memberships.GetAsync(userId, invitingOrgId);
        membership.Should().NotBeNull();
        membership!.Status.Should().Be(MembershipStatus.Active);
        (await Memberships.GetAsync(userId, currentOrgId)).Should().BeNull();
    }

    [Fact]
    public async Task CleanupExpired_WithNoTenantResolved_SweepsEveryOrganization()
    {
        DateTimeOffset lapsed = TimeProvider.System.GetUtcNow().AddDays(-1);
        Invitation inOrgA = await SeedInvitationAsync(
            Guid.NewGuid(), $"invited-{Guid.NewGuid():N}@wallow.dev", lapsed);
        Invitation inOrgB = await SeedInvitationAsync(
            Guid.NewGuid(), $"invited-{Guid.NewGuid():N}@wallow.dev", lapsed);
        Invitation live = await SeedInvitationAsync(
            Guid.NewGuid(), $"invited-{Guid.NewGuid():N}@wallow.dev");

        DbContext.SetTenant(default);
        await ScopedServices.GetRequiredService<IInvitationService>().CleanupExpiredAsync();

        (await Invitations.GetByTokenAsync(inOrgA.Token))!.Status.Should().Be(InvitationStatus.Expired);
        (await Invitations.GetByTokenAsync(inOrgB.Token))!.Status.Should().Be(InvitationStatus.Expired);
        (await Invitations.GetByTokenAsync(live.Token))!.Status.Should().Be(InvitationStatus.Pending);
    }

    /// <summary>
    /// Seeds under the owning organization's tenant, because the save interceptor stamps
    /// <c>TenantId</c> from the context rather than from the entity.
    /// </summary>
    private async Task<Invitation> SeedInvitationAsync(
        Guid organizationId, string email, DateTimeOffset? expiresAt = null)
    {
        DbContext.SetTenant(TenantId.Create(organizationId));

        Invitation invitation = Invitation.Create(
            TenantId.Create(organizationId),
            email,
            expiresAt ?? TimeProvider.System.GetUtcNow().AddDays(7),
            Guid.NewGuid(),
            TimeProvider.System);

        Invitations.Add(invitation);
        await Invitations.SaveChangesAsync();

        return invitation;
    }

    private async Task<Guid> RegisterAsync(string email)
    {
        UserManager<WallowUser> userManager = ScopedServices.GetRequiredService<UserManager<WallowUser>>();
        WallowUser user = WallowUser.Create("Invited", "Person", email, TimeProvider.System);
        user.EmailConfirmed = true;

        IdentityResult result = await userManager.CreateAsync(user);
        result.Succeeded.Should().BeTrue();

        return user.Id;
    }

    private sealed record InvitationRow(Guid Id, string Email, string Status);
}
