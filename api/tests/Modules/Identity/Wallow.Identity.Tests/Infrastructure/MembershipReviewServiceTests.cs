using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.DTOs;
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
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The four answers a reviewer gives about somebody else's membership, the three listings they work
/// from — waiting, suspended, refused — and the one decision a member makes about their own: leaving.
///
/// A status reaches exactly one listing: a membership no listing claims can only be acted on by
/// somebody who already knows it exists.
///
/// The transitions that END access are the ones with a second obligation: a status decides only the
/// NEXT sign-in, so anything already issued has to be revoked separately. Denial is the exception —
/// only a Pending membership can be denied and a Pending membership never authenticated, so there
/// is nothing outstanding to take away.
/// </summary>
public sealed class MembershipReviewServiceTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly IDefaultMemberRoleResolver _roleResolver = Substitute.For<IDefaultMemberRoleResolver>();
    private readonly IMembershipAccessRevoker _accessRevoker = Substitute.For<IMembershipAccessRevoker>();
    private readonly UnguardedLastOwnerGuard _lastOwnerGuard = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-03-01T09:00:00Z", null));
    private readonly MembershipReviewService _sut;
    private readonly Organization _organization;
    private readonly Guid _orgId;
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _defaultRoleId = Guid.NewGuid();

    public MembershipReviewServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        IDataProtectionProvider dataProtection = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dataProtection);

        _organization = Organization.Create(
            TenantId.Create(Guid.NewGuid()), "Acme", "acme", Guid.NewGuid(), _time);
        _orgId = _organization.Id.Value;
        _dbContext.SetTenant(_organization.TenantId);
        _dbContext.Organizations.Add(_organization);
        _dbContext.SaveChanges();

        _roleResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_defaultRoleId);

        _sut = new MembershipReviewService(
            new MembershipRepository(_dbContext),
            _dbContext,
            _roleResolver,
            _accessRevoker,
            _lastOwnerGuard,
            _messageBus,
            _time,
            NullLogger<MembershipReviewService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task GetPendingAsync_ReturnsTheOutstandingRequestsOldestFirst()
    {
        WallowUser waiting = await GivenUserAsync("bob@acme.test", "Bob", "Waiting");
        WallowUser recent = await GivenUserAsync("cara@acme.test", "Cara", "Recent");
        await GivenPendingRequestAsync(waiting.Id);
        _time.Advance(TimeSpan.FromDays(6));
        await GivenPendingRequestAsync(recent.Id);

        IReadOnlyList<PendingMembershipDto> pending = await _sut.GetPendingAsync(_orgId);

        pending.Select(p => p.UserId).Should().ContainInOrder(waiting.Id, recent.Id);
        pending[0].Email.Should().Be("bob@acme.test");
        pending[0].FirstName.Should().Be("Bob");
        pending[0].LastName.Should().Be("Waiting");
        pending[0].RequestedAt.Should().BeBefore(pending[1].RequestedAt!.Value);
    }

    [Fact]
    public async Task GetPendingAsync_LeavesOutEveryStatusThatIsNotPending()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        IReadOnlyList<PendingMembershipDto> pending = await _sut.GetPendingAsync(_orgId);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAsync_LeavesOutAnotherOrganizationsRequests()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        _dbContext.Memberships.Add(Membership.RequestAccess(
            requester.Id, OrganizationId.Create(Guid.NewGuid()), _time));
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<PendingMembershipDto> pending = await _sut.GetPendingAsync(_orgId);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuspendedAsync_ReturnsWhoIsSuspendedAndWhenTheirAccessEnded()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test", "Ada", "Lovelace");
        await GivenActiveMembershipAsync(member.Id);
        _time.Advance(TimeSpan.FromDays(2));
        await _sut.SuspendAsync(_orgId, member.Id, _actorId);

        IReadOnlyList<ReviewedMembershipDto> suspended = await _sut.GetSuspendedAsync(_orgId);

        ReviewedMembershipDto row = suspended.Should().ContainSingle().Subject;
        row.UserId.Should().Be(member.Id);
        row.Email.Should().Be("ada@acme.test");
        row.FirstName.Should().Be("Ada");
        row.LastName.Should().Be("Lovelace");
        row.Status.Should().Be(MembershipStatus.Suspended);
        row.StatusChangedAt.Should().Be(_time.GetUtcNow());
    }

    [Fact]
    public async Task GetSuspendedAsync_PutsTheMostRecentlySuspendedFirst()
    {
        WallowUser earlier = await GivenUserAsync("bob@acme.test", "Bob", "Earlier");
        WallowUser later = await GivenUserAsync("cara@acme.test", "Cara", "Later");
        await GivenActiveMembershipAsync(earlier.Id);
        await GivenActiveMembershipAsync(later.Id);
        await _sut.SuspendAsync(_orgId, earlier.Id, _actorId);
        _time.Advance(TimeSpan.FromDays(3));
        await _sut.SuspendAsync(_orgId, later.Id, _actorId);

        IReadOnlyList<ReviewedMembershipDto> suspended = await _sut.GetSuspendedAsync(_orgId);

        suspended.Select(s => s.UserId).Should().ContainInOrder(later.Id, earlier.Id);
    }

    [Fact]
    public async Task GetSuspendedAsync_LeavesOutEveryStatusThatIsNotSuspended()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        WallowUser waiting = await GivenUserAsync("bob@acme.test");
        WallowUser refused = await GivenUserAsync("cara@acme.test");
        await GivenActiveMembershipAsync(member.Id);
        await GivenPendingRequestAsync(waiting.Id);
        await GivenPendingRequestAsync(refused.Id);
        await _sut.DenyAsync(_orgId, refused.Id, _actorId);

        IReadOnlyList<ReviewedMembershipDto> suspended = await _sut.GetSuspendedAsync(_orgId);

        suspended.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuspendedAsync_LeavesOutAnotherOrganizationsSuspensions()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenSuspendedMembershipElsewhereAsync(member.Id);

        IReadOnlyList<ReviewedMembershipDto> suspended = await _sut.GetSuspendedAsync(_orgId);

        suspended.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeniedAsync_ReturnsWhoWasRefusedAndWhenTheyWereRefused()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test", "Ada", "Lovelace");
        await GivenPendingRequestAsync(requester.Id);
        _time.Advance(TimeSpan.FromDays(2));
        await _sut.DenyAsync(_orgId, requester.Id, _actorId);

        IReadOnlyList<ReviewedMembershipDto> denied = await _sut.GetDeniedAsync(_orgId);

        ReviewedMembershipDto row = denied.Should().ContainSingle().Subject;
        row.UserId.Should().Be(requester.Id);
        row.Email.Should().Be("ada@acme.test");
        row.FirstName.Should().Be("Ada");
        row.LastName.Should().Be("Lovelace");
        row.Status.Should().Be(MembershipStatus.Denied);
        row.StatusChangedAt.Should().Be(_time.GetUtcNow());
    }

    [Fact]
    public async Task GetDeniedAsync_PutsTheMostRecentlyRefusedFirst()
    {
        WallowUser earlier = await GivenUserAsync("bob@acme.test", "Bob", "Earlier");
        WallowUser later = await GivenUserAsync("cara@acme.test", "Cara", "Later");
        await GivenPendingRequestAsync(earlier.Id);
        await GivenPendingRequestAsync(later.Id);
        await _sut.DenyAsync(_orgId, earlier.Id, _actorId);
        _time.Advance(TimeSpan.FromDays(3));
        await _sut.DenyAsync(_orgId, later.Id, _actorId);

        IReadOnlyList<ReviewedMembershipDto> denied = await _sut.GetDeniedAsync(_orgId);

        denied.Select(d => d.UserId).Should().ContainInOrder(later.Id, earlier.Id);
    }

    [Fact]
    public async Task GetDeniedAsync_LeavesOutEveryStatusThatIsNotDenied()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        WallowUser waiting = await GivenUserAsync("bob@acme.test");
        WallowUser suspendedMember = await GivenUserAsync("cara@acme.test");
        await GivenActiveMembershipAsync(member.Id);
        await GivenPendingRequestAsync(waiting.Id);
        await GivenActiveMembershipAsync(suspendedMember.Id);
        await _sut.SuspendAsync(_orgId, suspendedMember.Id, _actorId);

        IReadOnlyList<ReviewedMembershipDto> denied = await _sut.GetDeniedAsync(_orgId);

        denied.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeniedAsync_LeavesOutAnotherOrganizationsDenials()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenDeniedRequestElsewhereAsync(requester.Id);

        IReadOnlyList<ReviewedMembershipDto> denied = await _sut.GetDeniedAsync(_orgId);

        denied.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_ActivatesTheMembershipWithTheOrganizationsDefaultRole()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);

        await _sut.ApproveAsync(_orgId, requester.Id, _actorId);

        Membership membership = await LoadMembershipAsync(requester.Id);
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.RoleIds.Should().ContainSingle().Which.Should().Be(_defaultRoleId);
        membership.ReviewedBy.Should().Be(_actorId);
        await _roleResolver.Received(1).ResolveAsync(_orgId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAsync_RaisesTheSameEventADirectlyAddedMemberRaises()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);

        await _sut.ApproveAsync(_orgId, requester.Id, _actorId);

        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationMemberAddedEvent>(e =>
            e.OrganizationId == _orgId && e.UserId == requester.Id && e.Email == "ada@acme.test"));
    }

    [Fact]
    public async Task ApproveAsync_ForAMembershipThatIsNotPending_IsRefused()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        Func<Task> approve = () => _sut.ApproveAsync(_orgId, member.Id, _actorId);

        await approve.Should().ThrowAsync<BusinessRuleException>();
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<OrganizationMemberAddedEvent>());
    }

    [Fact]
    public async Task DenyAsync_RefusesTheRequestAndTakesNothingAway()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);

        await _sut.DenyAsync(_orgId, requester.Id, _actorId);

        Membership membership = await LoadMembershipAsync(requester.Id);
        membership.Status.Should().Be(MembershipStatus.Denied);
        membership.ReviewedBy.Should().Be(_actorId);
        await _accessRevoker.DidNotReceive().RevokeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendAsync_EndsWhatTheMemberAlreadyHolds()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        await _sut.SuspendAsync(_orgId, member.Id, _actorId);

        (await LoadMembershipAsync(member.Id)).Status.Should().Be(MembershipStatus.Suspended);
        await _accessRevoker.Received(1).RevokeAsync(member.Id, _orgId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendAsync_KeepsTheRowSoItCanBeReinstated()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);
        await _sut.SuspendAsync(_orgId, member.Id, _actorId);

        await _sut.ReinstateAsync(_orgId, member.Id, _actorId);

        (await LoadMembershipAsync(member.Id)).Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task ReinstateAsync_ForAMembershipThatWasNeverSuspended_IsRefused()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        Func<Task> reinstate = () => _sut.ReinstateAsync(_orgId, member.Id, _actorId);

        await reinstate.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task LeaveAsync_EndsWhatTheMemberHeldAndTellsTheOtherModules()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        await _sut.LeaveAsync(_orgId, member.Id);

        await _accessRevoker.Received(1).RevokeAsync(member.Id, _orgId, Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationMemberRemovedEvent>(e =>
            e.OrganizationId == _orgId && e.UserId == member.Id && e.Email == "ada@acme.test"));
    }

    [Fact]
    public async Task LeaveAsync_DeletesTheRowSoTheyMayAskToJoinAgain()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        await _sut.LeaveAsync(_orgId, member.Id);

        bool remains = await _dbContext.Memberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == member.Id);
        remains.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveAsync_WithdrawsARequestNobodyHasAnsweredYet()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);

        await _sut.LeaveAsync(_orgId, requester.Id);

        bool remains = await _dbContext.Memberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == requester.Id);
        remains.Should().BeFalse();
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("deny")]
    [InlineData("suspend")]
    [InlineData("reinstate")]
    [InlineData("leave")]
    public async Task EveryDecision_AboutSomebodyWhoIsNotAMemberHere_IsRefused(string decision)
    {
        Guid stranger = Guid.NewGuid();

        Func<Task> decide = () => decision switch
        {
            "approve" => _sut.ApproveAsync(_orgId, stranger, _actorId),
            "deny" => _sut.DenyAsync(_orgId, stranger, _actorId),
            "suspend" => _sut.SuspendAsync(_orgId, stranger, _actorId),
            "leave" => _sut.LeaveAsync(_orgId, stranger),
            _ => _sut.ReinstateAsync(_orgId, stranger, _actorId),
        };

        await decide.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "Identity.MemberNotFound");
    }

    [Fact]
    public async Task ApproveAsync_AuditsWhoLetThePersonIn()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);

        await _sut.ApproveAsync(_orgId, requester.Id, _actorId);

        await ThenTransitionPublished(MembershipTransition.Approved, requester.Id, _actorId);
    }

    [Fact]
    public async Task DenyAsync_AuditsWhoRefused()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);

        await _sut.DenyAsync(_orgId, requester.Id, _actorId);

        await ThenTransitionPublished(MembershipTransition.Denied, requester.Id, _actorId);
    }

    [Fact]
    public async Task ClearDenialAsync_AuditsWhoTookTheRefusalBack()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);
        await _sut.DenyAsync(_orgId, requester.Id, _actorId);

        await _sut.ClearDenialAsync(_orgId, requester.Id, _actorId);

        await ThenTransitionPublished(MembershipTransition.DenialCleared, requester.Id, _actorId);
    }

    [Fact]
    public async Task SuspendAsync_AuditsWhoTookTheAccessAway()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        await _sut.SuspendAsync(_orgId, member.Id, _actorId);

        await ThenTransitionPublished(MembershipTransition.Suspended, member.Id, _actorId);
    }

    [Fact]
    public async Task ReinstateAsync_AuditsWhoGaveTheAccessBack()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);
        await _sut.SuspendAsync(_orgId, member.Id, _actorId);

        await _sut.ReinstateAsync(_orgId, member.Id, _actorId);

        await ThenTransitionPublished(MembershipTransition.Reinstated, member.Id, _actorId);
    }

    /// <summary>
    /// Nobody acts on the leaver's behalf, so the actor and the subject are the same person. An
    /// audit trail that left the actor blank here would read as an unattributed removal.
    /// </summary>
    [Fact]
    public async Task LeaveAsync_AuditsTheLeaverAsTheirOwnActor()
    {
        WallowUser member = await GivenUserAsync("ada@acme.test");
        await GivenActiveMembershipAsync(member.Id);

        await _sut.LeaveAsync(_orgId, member.Id);

        await ThenTransitionPublished(MembershipTransition.Left, member.Id, member.Id);
    }

    [Fact]
    public async Task ApproveAsync_StampsTheAuditEventFromTheInjectedClock()
    {
        WallowUser requester = await GivenUserAsync("ada@acme.test");
        await GivenPendingRequestAsync(requester.Id);

        await _sut.ApproveAsync(_orgId, requester.Id, _actorId);

        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipTransitionedEvent>(e =>
            e.OccurredAt == _time.GetUtcNow().UtcDateTime));
    }

    private ValueTask ThenTransitionPublished(
        MembershipTransition transition, Guid userId, Guid actorId) =>
        _messageBus.Received(1).PublishAsync(Arg.Is<MembershipTransitionedEvent>(e =>
            e.Transition == transition
            && e.UserId == userId
            && e.ActorId == actorId
            && e.OrganizationId == _orgId
            && e.TenantId == _orgId));

    private async Task<WallowUser> GivenUserAsync(
        string email, string firstName = "Ada", string lastName = "Lovelace")
    {
        WallowUser user = WallowUser.Create(firstName, lastName, email, _time);
        user.EmailConfirmed = true;
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task GivenPendingRequestAsync(Guid userId)
    {
        _dbContext.Memberships.Add(Membership.RequestAccess(
            userId, _organization.Id, _time));
        await _dbContext.SaveChangesAsync();
    }

    private async Task GivenActiveMembershipAsync(Guid userId)
    {
        _dbContext.Memberships.Add(Membership.Enroll(
            userId, _organization.Id, _defaultRoleId, _time));
        await _dbContext.SaveChangesAsync();
    }

    private async Task GivenSuspendedMembershipElsewhereAsync(Guid userId)
    {
        Membership membership = Membership.Enroll(
            userId, OrganizationId.Create(Guid.NewGuid()), _defaultRoleId, _time);
        membership.Suspend(_actorId, _time);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();
    }

    private async Task GivenDeniedRequestElsewhereAsync(Guid userId)
    {
        Membership membership = Membership.RequestAccess(
            userId, OrganizationId.Create(Guid.NewGuid()), _time);
        membership.Deny(_actorId, _time);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<Membership> LoadMembershipAsync(Guid userId)
    {
        Membership? membership = await _dbContext.Memberships
            .IgnoreQueryFilters()
            .Include(m => m.Roles)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        membership.Should().NotBeNull();
        return membership!;
    }
}
