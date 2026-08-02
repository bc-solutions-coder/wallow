using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The enrollment policy engine: who may join an organization they are not yet a member of,
/// and what is recorded when they do.
///
/// The organization mints its own id and its tenant id from it, so the fixture takes both from
/// the created row rather than choosing a guid — an organization built around a guid of the
/// spec's own choosing is not the one the reads find.
/// </summary>
public sealed class UserEnrollmentServiceTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly IDefaultMemberRoleResolver _roleResolver = Substitute.For<IDefaultMemberRoleResolver>();
    private readonly IAccessRequestRecipientResolver _recipients = Substitute.For<IAccessRequestRecipientResolver>();
    private readonly UserEnrollmentService _sut;
    private readonly Organization _organization;
    private readonly Guid _orgId;
    private readonly Guid _defaultRoleId = Guid.NewGuid();

    public UserEnrollmentServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        IDataProtectionProvider dataProtection = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dataProtection);

        _organization = Organization.Create(
            TenantId.Create(Guid.NewGuid()), "Acme", "acme", Guid.NewGuid(), TimeProvider.System);
        _orgId = _organization.Id.Value;
        _dbContext.SetTenant(_organization.TenantId);
        _dbContext.Organizations.Add(_organization);
        _dbContext.SaveChanges();

        _roleResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_defaultRoleId);
        _recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "owner@acme.test" });

        _sut = new UserEnrollmentService(
            _dbContext,
            new MembershipRepository(_dbContext),
            _roleResolver,
            _recipients,
            _messageBus,
            TimeProvider.System,
            NullLogger<UserEnrollmentService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task EnrollAsync_UnderOpenEnrollment_MakesTheCallerAnActiveMember()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.Open);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Enrolled>();
        Membership membership = await LoadMembershipAsync(user.Id);
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.RoleIds.Should().ContainSingle().Which.Should().Be(_defaultRoleId);
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationMemberAddedEvent>(e => e.UserId == user.Id && e.OrganizationId == _orgId));
    }

    [Fact]
    public async Task EnrollAsync_UnderRequestApproval_RecordsAPendingRequestAndTellsTheRecipients()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.RequestApproval);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<PendingApproval>();
        Membership membership = await LoadMembershipAsync(user.Id);
        membership.Status.Should().Be(MembershipStatus.Pending);
        membership.RoleIds.Should().BeEmpty();
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<AccessRequestedEvent>(e =>
                e.RequesterUserId == user.Id && e.RecipientEmails.Contains("owner@acme.test")));
    }

    [Fact]
    public async Task EnrollAsync_UnderInviteOnly_RecordsNothing()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.InviteOnly);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Rejected>().Which.Reason.Should().Be("not_a_member");
        _dbContext.Memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrollAsync_WithNoSettingsAtAll_RecordsNothing()
    {
        // A policy nobody has chosen has to be the one that grants nothing.
        WallowUser user = await GivenVerifiedUserAsync();

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Rejected>().Which.Reason.Should().Be("not_a_member");
        _dbContext.Memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrollAsync_WithAnUnverifiedEmail_RefusesEvenUnderOpenEnrollment()
    {
        WallowUser user = await GivenUserAsync(emailConfirmed: false);
        await GivenPolicyAsync(EnrollmentPolicy.Open);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Rejected>().Which.Reason.Should().Be("email_unverified");
        _dbContext.Memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrollAsync_AgainstAnArchivedOrganization_Refuses()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.Open);
        _organization.Archive(Guid.NewGuid(), TimeProvider.System);
        await _dbContext.SaveChangesAsync();

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Rejected>().Which.Reason.Should().Be("not_a_member");
    }

    [Fact]
    public async Task EnrollAsync_ForAnUnknownOrganization_Refuses()
    {
        WallowUser user = await GivenVerifiedUserAsync();

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, Guid.NewGuid());

        outcome.Should().BeOfType<Rejected>().Which.Reason.Should().Be("not_a_member");
    }

    [Theory]
    [InlineData(MembershipStatus.Suspended, "membership_suspended")]
    [InlineData(MembershipStatus.Denied, "membership_denied")]
    public async Task EnrollAsync_WithAReviewedRefusalOnFile_DoesNotLetTheRefusedPersonReverseIt(
        MembershipStatus status, string expectedReason)
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.Open);
        await GivenMembershipAsync(user.Id, status);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Rejected>().Which.Reason.Should().Be(expectedReason);
        _dbContext.Memberships.Should().ContainSingle();
    }

    [Fact]
    public async Task EnrollAsync_WithAPendingRequestOnFile_WritesNoSecondRequest()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.RequestApproval);
        await GivenMembershipAsync(user.Id, MembershipStatus.Pending);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<PendingApproval>();
        _dbContext.Memberships.Should().ContainSingle();
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<AccessRequestedEvent>());
    }

    [Fact]
    public async Task EnrollAsync_ForAnExistingMember_ChangesNothing()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.Open);
        await GivenMembershipAsync(user.Id, MembershipStatus.Active);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Enrolled>();
        _dbContext.Memberships.Should().ContainSingle();
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<OrganizationMemberAddedEvent>());
    }

    [Fact]
    public async Task EnrollAsync_WithNobodyToNotify_StillRecordsTheRequest()
    {
        _recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.RequestApproval);

        EnrollmentOutcome outcome = await _sut.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<PendingApproval>();
        (await LoadMembershipAsync(user.Id)).Status.Should().Be(MembershipStatus.Pending);
    }

    /// <summary>
    /// Self-service, so the actor and the subject are the same person. The audit trail records
    /// that equality rather than leaving the actor blank, which would read as an unattributed
    /// admission.
    /// </summary>
    [Fact]
    public async Task EnrollAsync_UnderRequestApproval_AuditsTheRequestAgainstTheRequester()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.RequestApproval);

        await _sut.EnrollAsync(user.Id, _orgId);

        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipTransitionedEvent>(e =>
            e.Transition == MembershipTransition.AccessRequested
            && e.UserId == user.Id
            && e.ActorId == user.Id
            && e.OrganizationId == _orgId
            && e.TenantId == _orgId));
    }

    [Fact]
    public async Task EnrollAsync_UnderOpenEnrollment_AuditsTheJoinAgainstTheJoiner()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.Open);

        await _sut.EnrollAsync(user.Id, _orgId);

        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipTransitionedEvent>(e =>
            e.Transition == MembershipTransition.Enrolled
            && e.UserId == user.Id
            && e.ActorId == user.Id
            && e.OrganizationId == _orgId
            && e.TenantId == _orgId));
    }

    [Fact]
    public async Task EnrollAsync_UnderInviteOnly_AuditsNothing()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.InviteOnly);

        await _sut.EnrollAsync(user.Id, _orgId);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<MembershipTransitionedEvent>());
    }

    private async Task<WallowUser> GivenVerifiedUserAsync() => await GivenUserAsync(emailConfirmed: true);

    private async Task<WallowUser> GivenUserAsync(bool emailConfirmed)
    {
        WallowUser user = WallowUser.Create("Ada", "Lovelace", "ada@acme.test", TimeProvider.System);
        user.EmailConfirmed = emailConfirmed;
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task GivenPolicyAsync(EnrollmentPolicy policy)
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _organization.Id, _organization.TenantId, false, false, 0, Guid.NewGuid(), TimeProvider.System);
        settings.UpdateEnrollment(policy, null, null, Guid.NewGuid(), TimeProvider.System);
        _dbContext.OrganizationSettings.Add(settings);
        await _dbContext.SaveChangesAsync();
    }

    private async Task GivenMembershipAsync(Guid userId, MembershipStatus status)
    {
        OrganizationId organizationId = OrganizationId.Create(_orgId);
        Guid actorId = Guid.NewGuid();

        Membership membership = status == MembershipStatus.Pending
            ? Membership.RequestAccess(userId, organizationId, TimeProvider.System)
            : Membership.Enroll(userId, organizationId, _defaultRoleId, TimeProvider.System);

        switch (status)
        {
            case MembershipStatus.Suspended:
                membership.Suspend(actorId, TimeProvider.System);
                break;

            case MembershipStatus.Denied:
                Membership denied = Membership.RequestAccess(userId, organizationId, TimeProvider.System);
                denied.Deny(actorId, TimeProvider.System);
                membership = denied;
                break;

            default:
                break;
        }

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
