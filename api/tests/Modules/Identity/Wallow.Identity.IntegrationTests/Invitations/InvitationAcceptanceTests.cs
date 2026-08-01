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
/// POST /identity/invitations/{token}/accept over the real stack. Anyone can register an account
/// against an address they do not control, so the address alone is not the invited person —
/// acceptance turns on a CONFIRMED email. The verified control below is what keeps the refusal
/// honest: it proves the token, the org and the harness are otherwise sound.
///
/// Backend-dependent: requires the WallowApiFactory stack (Postgres + seeded identity data).
/// </summary>
[Trait("Category", "Integration")]
public class InvitationAcceptanceTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IMembershipRepository Memberships => ScopedServices.GetRequiredService<IMembershipRepository>();

    private IInvitationRepository Invitations => ScopedServices.GetRequiredService<IInvitationRepository>();

    [Fact]
    public async Task Accept_ByAnUnverifiedRegistrationOfTheInvitedAddress_IsRefusedAndGrantsNothing()
    {
        Guid invitingOrgId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        Invitation invitation = await SeedInvitationAsync(invitingOrgId, email);
        Guid userId = await RegisterAsync(email, emailConfirmed: false);

        HttpResponseMessage response = await AcceptAsync(invitation.Token, userId);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Memberships.GetAsync(userId, invitingOrgId)).Should().BeNull();

        Invitation? unspent = await Invitations.GetByTokenAsync(invitation.Token);
        unspent!.Status.Should().Be(InvitationStatus.Pending);
        unspent.AcceptedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Accept_OnceTheSameRegistrationIsVerified_EnrollsThem()
    {
        Guid invitingOrgId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        Invitation invitation = await SeedInvitationAsync(invitingOrgId, email);
        Guid userId = await RegisterAsync(email, emailConfirmed: true);

        HttpResponseMessage response = await AcceptAsync(invitation.Token, userId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Membership? membership = await Memberships.GetAsync(userId, invitingOrgId);
        membership.Should().NotBeNull();
        membership!.Status.Should().Be(MembershipStatus.Active);
    }

    private Task<HttpResponseMessage> AcceptAsync(string token, Guid userId)
    {
        SetTestUser(userId.ToString());
        return Client.PostAsJsonAsync($"/identity/invitations/{token}/accept", new { });
    }

    /// <summary>
    /// Seeds the invitation under the inviting organization's tenant, because the save interceptor
    /// stamps <c>TenantId</c> from the context rather than from the entity.
    /// </summary>
    private async Task<Invitation> SeedInvitationAsync(Guid organizationId, string email)
    {
        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();
        dbContext.SetTenant(TenantId.Create(organizationId));

        Invitation invitation = Invitation.Create(
            TenantId.Create(organizationId),
            email,
            TimeProvider.System.GetUtcNow().AddDays(7),
            Guid.NewGuid(),
            TimeProvider.System);

        Invitations.Add(invitation);
        await Invitations.SaveChangesAsync();

        return invitation;
    }

    private async Task<Guid> RegisterAsync(string email, bool emailConfirmed)
    {
        UserManager<WallowUser> userManager = ScopedServices.GetRequiredService<UserManager<WallowUser>>();
        WallowUser user = WallowUser.Create("Invited", "Person", email, TimeProvider.System);
        user.EmailConfirmed = emailConfirmed;

        IdentityResult result = await userManager.CreateAsync(user);
        result.Succeeded.Should().BeTrue();

        return user.Id;
    }
}
