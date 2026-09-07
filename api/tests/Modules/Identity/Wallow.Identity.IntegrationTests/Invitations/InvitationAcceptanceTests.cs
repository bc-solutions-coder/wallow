using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Invitations;

/// <summary>
/// Checks invitation acceptance against persisted memberships and invitation state,
/// including confirmed-email matching and refusals that create no membership.
/// </summary>
[Trait("Category", "Integration")]
public class InvitationAcceptanceTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IMembershipRepository Memberships => ScopedServices.GetRequiredService<IMembershipRepository>();

    private IInvitationRepository Invitations => ScopedServices.GetRequiredService<IInvitationRepository>();

    private IdentityDbContext DbContext => ScopedServices.GetRequiredService<IdentityDbContext>();

    [Fact]
    public async Task Accept_ByTheVerifiedInvitedIdentity_EnrollsThemAndSpendsTheTokenTogether()
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
        membership.OrganizationId.Value.Should().Be(invitingOrgId);
        membership.RoleIds.Should().ContainSingle().Which.Should().Be(await MemberRoleIdAsync());

        Invitation spent = await ReReadAsync(invitation.Token);
        spent.Status.Should().Be(InvitationStatus.Accepted);
        spent.AcceptedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task Accept_AfterTheInvitationLapses_IsRefusedAndSettlesTheRowExpired()
    {
        Guid invitingOrgId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        Invitation invitation = await SeedInvitationAsync(
            invitingOrgId, email, expiresAt: TimeProvider.System.GetUtcNow().AddDays(-1));
        Guid userId = await RegisterAsync(email, emailConfirmed: true);

        HttpResponseMessage response = await AcceptAsync(invitation.Token, userId);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Memberships.GetAsync(userId, invitingOrgId)).Should().BeNull();

        // Acceptance of an expired token must also mark its invitation expired.
        Invitation lapsed = await ReReadAsync(invitation.Token);
        lapsed.Status.Should().Be(InvitationStatus.Expired);
        lapsed.AcceptedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Accept_ByAVerifiedIdentityAtAnotherAddress_IsRefusedAndGrantsNothing()
    {
        Guid invitingOrgId = Guid.NewGuid();
        Invitation invitation = await SeedInvitationAsync(
            invitingOrgId, $"invited-{Guid.NewGuid():N}@wallow.dev");
        Guid userId = await RegisterAsync($"bystander-{Guid.NewGuid():N}@wallow.dev", emailConfirmed: true);

        HttpResponseMessage response = await AcceptAsync(invitation.Token, userId);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Memberships.GetAsync(userId, invitingOrgId)).Should().BeNull();

        Invitation unspent = await ReReadAsync(invitation.Token);
        unspent.Status.Should().Be(InvitationStatus.Pending);
        unspent.AcceptedByUserId.Should().BeNull();
    }

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

        Invitation unspent = await ReReadAsync(invitation.Token);
        unspent.Status.Should().Be(InvitationStatus.Pending);
        unspent.AcceptedByUserId.Should().BeNull();
    }

    private Task<HttpResponseMessage> AcceptAsync(string token, Guid userId)
    {
        SetTestUser(userId.ToString());
        return Client.PostAsJsonAsync($"/identity/invitations/{token}/accept", new { });
    }

    /// <summary>
    /// Clears tracked state before reading changes written by the HTTP request.
    /// </summary>
    private async Task<Invitation> ReReadAsync(string token)
    {
        DbContext.ChangeTracker.Clear();

        Invitation? invitation = await Invitations.GetByTokenAsync(token);
        invitation.Should().NotBeNull();

        return invitation!;
    }

    private async Task<Guid> MemberRoleIdAsync()
    {
        WallowRole role = await DbContext.Roles
            .IgnoreQueryFilters()
            .FirstAsync(r => r.NormalizedName == "USER");

        return role.Id;
    }

    /// <summary>
    /// Matches the tenant context to the inviting organization for save-time tenant stamping.
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
