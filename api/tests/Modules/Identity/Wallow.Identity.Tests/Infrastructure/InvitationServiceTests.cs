using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class InvitationServiceTests : IDisposable
{
    private readonly IInvitationRepository _invRepo;
    private readonly IMessageBus _messageBus;
    private readonly IdentityDbContext _dbContext;
    private readonly MembershipRepository _memberships;
    private readonly InvitationService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeTimeProvider _tp;

    public InvitationServiceTests()
    {
        _invRepo = Substitute.For<IInvitationRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
        TenantContext tc = new(); tc.SetTenant(new TenantId(_tenantId));
        DbContextOptions<IdentityDbContext> opts = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(opts, dp);
        _dbContext.SetTenant(new TenantId(_tenantId));
        _memberships = new MembershipRepository(_dbContext);

        // The real InvitationRepository saves the same IdentityDbContext the membership repository
        // does, which is what makes acceptance one transaction. The substitute has to do the same
        // or the membership half of that write silently disappears.
        _invRepo.When(r => r.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => _dbContext.SaveChanges());

        _sut = new InvitationService(_invRepo, _memberships, _messageBus, _tp, _dbContext);
    }

    public void Dispose() { _dbContext.Dispose(); }

    /// <summary>
    /// The accepting identity, verified unless told otherwise. Acceptance reads the user straight
    /// off the context, so it has to exist there rather than only as a bare id.
    /// </summary>
    private async Task<Guid> SeedUserAsync(string email, bool emailConfirmed = true)
    {
        WallowUser user = WallowUser.Create("Invited", "Person", email, _tp);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.NormalizedUserName = email.ToUpperInvariant();
        user.EmailConfirmed = emailConfirmed;
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> SeedMemberRoleAsync()
    {
        WallowRole role = new() { Id = Guid.NewGuid(), Name = "user", NormalizedName = "USER" };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();
        return role.Id;
    }

    private Invitation SeedInvitation(string email, DateTimeOffset? expiresAt = null)
    {
        Invitation invitation = Invitation.Create(
            new TenantId(_tenantId), email, expiresAt ?? _tp.GetUtcNow().AddDays(7), Guid.NewGuid(), _tp);
        _invRepo.GetByTokenAsync(invitation.Token, Arg.Any<CancellationToken>()).Returns(invitation);
        return invitation;
    }

    [Fact]
    public async Task CreateInvitation_PersistsAndPublishes()
    {
        Invitation r = await _sut.CreateInvitationAsync(_tenantId, "i@t.com", Guid.NewGuid());
        r.Email.Should().Be("i@t.com");
        r.Token.Should().NotBeNullOrEmpty();
        _invRepo.Received(1).Add(Arg.Any<Invitation>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<InvitationCreatedEvent>(e => e.Email == "i@t.com"));
    }

    [Fact]
    public async Task RevokeInvitation_WhenExists_Revokes()
    {
        Invitation inv = Invitation.Create(new TenantId(_tenantId), "r@t.com", DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid(), TimeProvider.System);
        _invRepo.GetByIdAsync(Arg.Any<InvitationId>(), Arg.Any<CancellationToken>()).Returns(inv);
        await _sut.RevokeInvitationAsync(inv.Id.Value, Guid.NewGuid());
        await _invRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeInvitation_NotFound_Throws()
    {
        _invRepo.GetByIdAsync(Arg.Any<InvitationId>(), Arg.Any<CancellationToken>()).Returns((Invitation?)null);
        Func<Task> act = () => _sut.RevokeInvitationAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task GetByToken_Delegates()
    {
        Invitation inv = Invitation.Create(new TenantId(_tenantId), "g@t.com", DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid(), TimeProvider.System);
        _invRepo.GetByTokenAsync("tk", Arg.Any<CancellationToken>()).Returns(inv);
        Invitation? r = await _sut.GetInvitationByTokenAsync("tk");
        r.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptInvitation_EnrollsTheInvitedUserInTheInvitingOrganization()
    {
        Guid roleId = await SeedMemberRoleAsync();
        Guid userId = await SeedUserAsync("a@t.com");
        Invitation inv = SeedInvitation("a@t.com");

        await _sut.AcceptInvitationAsync(inv.Token, userId);

        inv.Status.Should().Be(InvitationStatus.Accepted);
        Membership? membership = await _memberships.GetAsync(userId, _tenantId);
        membership.Should().NotBeNull();
        membership!.Status.Should().Be(MembershipStatus.Active);
        membership.RoleIds.Should().ContainSingle().Which.Should().Be(roleId);
        await _messageBus.Received(1).PublishAsync(Arg.Any<OrganizationMemberAddedEvent>());
    }

    /// <summary>
    /// A leaked or forwarded token is otherwise a join credential for whoever holds it, which in an
    /// invite-only organization is the whole perimeter.
    /// </summary>
    [Fact]
    public async Task AcceptInvitation_ByAnotherEmail_IsRefusedAndGrantsNothing()
    {
        await SeedMemberRoleAsync();
        Guid intruderId = await SeedUserAsync("intruder@t.com");
        Invitation inv = SeedInvitation("invited@t.com");

        Func<Task> act = () => _sut.AcceptInvitationAsync(inv.Token, intruderId);

        await act.Should().ThrowAsync<BusinessRuleException>();
        inv.Status.Should().Be(InvitationStatus.Pending);
        (await _memberships.GetAsync(intruderId, _tenantId)).Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvitation_FromAnUnverifiedEmail_IsRefusedAndGrantsNothing()
    {
        await SeedMemberRoleAsync();
        Guid userId = await SeedUserAsync("a@t.com", emailConfirmed: false);
        Invitation inv = SeedInvitation("a@t.com");

        Func<Task> act = () => _sut.AcceptInvitationAsync(inv.Token, userId);

        await act.Should().ThrowAsync<BusinessRuleException>();
        (await _memberships.GetAsync(userId, _tenantId)).Should().BeNull();
    }

    /// <summary>
    /// The inviter types the address; Identity stores it upper-invariant. Comparing raw would
    /// reject a legitimate acceptance over nothing but casing.
    /// </summary>
    [Fact]
    public async Task AcceptInvitation_WhenTheEmailDiffersOnlyInCase_Succeeds()
    {
        await SeedMemberRoleAsync();
        Guid userId = await SeedUserAsync("a@t.com");
        Invitation inv = SeedInvitation("A@T.CoM");

        await _sut.AcceptInvitationAsync(inv.Token, userId);

        (await _memberships.GetAsync(userId, _tenantId)).Should().NotBeNull();
    }

    /// <summary>
    /// Between an invitation lapsing and the next sweep, Pending is not the same thing as live.
    /// </summary>
    [Fact]
    public async Task AcceptInvitation_PastItsExpiry_IsRefusedAndSettlesTheInvitation()
    {
        await SeedMemberRoleAsync();
        Guid userId = await SeedUserAsync("a@t.com");
        Invitation inv = SeedInvitation("a@t.com", _tp.GetUtcNow().AddDays(1));

        _tp.Advance(TimeSpan.FromDays(2));

        Func<Task> act = () => _sut.AcceptInvitationAsync(inv.Token, userId);

        await act.Should().ThrowAsync<BusinessRuleException>();
        inv.Status.Should().Be(InvitationStatus.Expired);
        (await _memberships.GetAsync(userId, _tenantId)).Should().BeNull();
    }

    /// <summary>
    /// An invitation supersedes a request the same person already made. Leaving the row Pending
    /// strands it: it blocks the next legitimate request and outlives a later denial.
    /// </summary>
    [Fact]
    public async Task AcceptInvitation_ClosesAPendingAccessRequestForTheSameOrganization()
    {
        Guid roleId = await SeedMemberRoleAsync();
        Guid userId = await SeedUserAsync("a@t.com");
        _dbContext.Memberships.Add(Membership.RequestAccess(userId, OrganizationId.Create(_tenantId), _tp));
        await _dbContext.SaveChangesAsync();
        Invitation inv = SeedInvitation("a@t.com");

        await _sut.AcceptInvitationAsync(inv.Token, userId);

        Membership? membership = await _memberships.GetAsync(userId, _tenantId);
        membership!.Status.Should().Be(MembershipStatus.Active);
        membership.RoleIds.Should().ContainSingle().Which.Should().Be(roleId);
    }

    [Fact]
    public async Task AcceptInvitation_ByAnExistingActiveMember_AddsNoSecondMembership()
    {
        Guid roleId = await SeedMemberRoleAsync();
        Guid userId = await SeedUserAsync("a@t.com");
        _dbContext.Memberships.Add(Membership.Enroll(userId, OrganizationId.Create(_tenantId), roleId, _tp));
        await _dbContext.SaveChangesAsync();
        Invitation inv = SeedInvitation("a@t.com");

        await _sut.AcceptInvitationAsync(inv.Token, userId);

        inv.Status.Should().Be(InvitationStatus.Accepted);
        _dbContext.Memberships.IgnoreQueryFilters().Count(m => m.UserId == userId).Should().Be(1);
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<OrganizationMemberAddedEvent>());
    }

    [Fact]
    public async Task AcceptInvitation_ByASuspendedMember_IsRefused()
    {
        Guid roleId = await SeedMemberRoleAsync();
        Guid userId = await SeedUserAsync("a@t.com");
        Membership suspended = Membership.Enroll(userId, OrganizationId.Create(_tenantId), roleId, _tp);
        suspended.Suspend(Guid.NewGuid(), _tp);
        _dbContext.Memberships.Add(suspended);
        await _dbContext.SaveChangesAsync();
        Invitation inv = SeedInvitation("a@t.com");

        Func<Task> act = () => _sut.AcceptInvitationAsync(inv.Token, userId);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task AcceptInvitation_NotFound_Throws()
    {
        _invRepo.GetByTokenAsync("bad", Arg.Any<CancellationToken>()).Returns((Invitation?)null);
        Func<Task> act = () => _sut.AcceptInvitationAsync("bad", Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task CleanupExpired_MarksExpired()
    {
        Invitation expired = Invitation.Create(new TenantId(_tenantId), "e@t.com", _tp.GetUtcNow().AddDays(-1), Guid.NewGuid(), _tp);
        _dbContext.Invitations.Add(expired); await _dbContext.SaveChangesAsync();
        await _sut.CleanupExpiredAsync();
        Invitation? r = await _dbContext.Invitations.AsTracking().FirstOrDefaultAsync(i => i.Id == expired.Id);
        r!.Status.Should().Be(InvitationStatus.Expired);
    }

    /// <summary>
    /// The sweep is a background job, so nothing has resolved a tenant for it. An unresolved tenant
    /// matches no row, which would make the job a silent no-op rather than a failure.
    /// </summary>
    [Fact]
    public async Task CleanupExpired_MarksExpired_WhenNoTenantIsResolved()
    {
        Invitation expired = Invitation.Create(new TenantId(_tenantId), "e@t.com", _tp.GetUtcNow().AddDays(-1), Guid.NewGuid(), _tp);
        _dbContext.Invitations.Add(expired); await _dbContext.SaveChangesAsync();

        _dbContext.SetTenant(default);
        await _sut.CleanupExpiredAsync();

        _dbContext.SetTenant(new TenantId(_tenantId));
        Invitation? r = await _dbContext.Invitations.AsTracking().FirstOrDefaultAsync(i => i.Id == expired.Id);
        r!.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public async Task CleanupExpired_DoesNotMarkValid()
    {
        Invitation valid = Invitation.Create(new TenantId(_tenantId), "v@t.com", _tp.GetUtcNow().AddDays(7), Guid.NewGuid(), _tp);
        _dbContext.Invitations.Add(valid); await _dbContext.SaveChangesAsync();
        await _sut.CleanupExpiredAsync();
        Invitation? r = await _dbContext.Invitations.AsTracking().FirstOrDefaultAsync(i => i.Id == valid.Id);
        r!.Status.Should().Be(InvitationStatus.Pending);
    }
}
