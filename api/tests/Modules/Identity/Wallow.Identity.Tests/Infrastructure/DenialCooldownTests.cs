using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// A denial answers one request rather than barring a person: it stands for the cooldown, and after
/// that the organization's CURRENT policy decides what asking again gets them. A reviewer can also
/// lift it early.
///
/// Both ways back reuse or delete the one row, never add a second — (UserId, OrganizationId) is
/// unique, so a spec that lets a duplicate through here is describing an insert the database refuses.
/// </summary>
public sealed class DenialCooldownTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly IDefaultMemberRoleResolver _roleResolver = Substitute.For<IDefaultMemberRoleResolver>();
    private readonly IAccessRequestRecipientResolver _recipients = Substitute.For<IAccessRequestRecipientResolver>();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-03-01T09:00:00Z", null));
    private readonly UserEnrollmentService _enrollment;
    private readonly MembershipReviewService _review;
    private readonly Organization _organization;
    private readonly Guid _orgId;
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _defaultRoleId = Guid.NewGuid();

    public DenialCooldownTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, DataProtectionProvider.Create("test"));

        _organization = Organization.Create(
            TenantId.Create(Guid.NewGuid()), "Acme", "acme", Guid.NewGuid(), _time);
        _orgId = _organization.Id.Value;
        _dbContext.SetTenant(_organization.TenantId);
        _dbContext.Organizations.Add(_organization);
        _dbContext.SaveChanges();

        _roleResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_defaultRoleId);
        _recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "owner@acme.test" });

        _enrollment = new UserEnrollmentService(
            _dbContext,
            new MembershipRepository(_dbContext),
            _roleResolver,
            _recipients,
            _messageBus,
            _time,
            NullLogger<UserEnrollmentService>.Instance);

        _review = new MembershipReviewService(
            new MembershipRepository(_dbContext),
            _dbContext,
            _roleResolver,
            Substitute.For<IAccessRevoker>(),
            new UnguardedLastOwnerGuard(),
            _messageBus,
            _time,
            NullLogger<MembershipReviewService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task AskingAgainOneDayShortOfTheCooldown_GetsTheSameRefusal()
    {
        WallowUser user = await GivenDeniedRequesterAsync(EnrollmentPolicy.RequestApproval);
        _time.Advance(Membership.DenialCooldown - TimeSpan.FromDays(1));

        EnrollmentOutcome outcome = await _enrollment.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Rejected>().Which.Reason.Should().Be("membership_denied");
        _dbContext.Memberships.Should().ContainSingle()
            .Which.Status.Should().Be(MembershipStatus.Denied);
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<AccessRequestedEvent>());
    }

    [Fact]
    public async Task AskingAgainOnceTheCooldownHasRun_PutsThemBackInTheQueue()
    {
        WallowUser user = await GivenDeniedRequesterAsync(EnrollmentPolicy.RequestApproval);
        _time.Advance(Membership.DenialCooldown);

        EnrollmentOutcome outcome = await _enrollment.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<PendingApproval>();
        Membership membership = _dbContext.Memberships.Should().ContainSingle().Subject;
        membership.Status.Should().Be(MembershipStatus.Pending);
        membership.ReviewedBy.Should().BeNull();
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<AccessRequestedEvent>(e => e.RequesterUserId == user.Id));
    }

    /// <summary>
    /// The organization opened up while the denial ran. Nothing about a spent denial outranks the
    /// policy in force when they ask.
    /// </summary>
    [Fact]
    public async Task AskingAgainOfAnOpenOrganization_AdmitsThemOutright()
    {
        WallowUser user = await GivenDeniedRequesterAsync(EnrollmentPolicy.Open);
        _time.Advance(Membership.DenialCooldown);

        EnrollmentOutcome outcome = await _enrollment.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<Enrolled>();
        Membership membership = await LoadMembershipAsync(user.Id);
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.RoleIds.Should().BeEquivalentTo([_defaultRoleId]);
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<OrganizationMemberAddedEvent>(e => e.UserId == user.Id));
    }

    [Fact]
    public async Task AReviewerLiftingTheDenial_LetsThemAskAgainThatDay()
    {
        WallowUser user = await GivenDeniedRequesterAsync(EnrollmentPolicy.RequestApproval);

        await _review.ClearDenialAsync(_orgId, user.Id, _actorId);

        _dbContext.Memberships.Should().BeEmpty();

        EnrollmentOutcome outcome = await _enrollment.EnrollAsync(user.Id, _orgId);

        outcome.Should().BeOfType<PendingApproval>();
        (await LoadMembershipAsync(user.Id)).Status.Should().Be(MembershipStatus.Pending);
    }

    [Fact]
    public async Task LiftingADenialNobodyMade_IsRefused()
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(EnrollmentPolicy.Open);
        await _enrollment.EnrollAsync(user.Id, _orgId);

        Func<Task> clear = () => _review.ClearDenialAsync(_orgId, user.Id, _actorId);

        await clear.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "Identity.MembershipNotDenied");
        (await LoadMembershipAsync(user.Id)).Status.Should().Be(MembershipStatus.Active);
    }

    private async Task<WallowUser> GivenDeniedRequesterAsync(EnrollmentPolicy policy)
    {
        WallowUser user = await GivenVerifiedUserAsync();
        await GivenPolicyAsync(policy);

        Membership request = Membership.RequestAccess(user.Id, _organization.Id, _time);
        request.Deny(_actorId, _time);
        _dbContext.Memberships.Add(request);
        await _dbContext.SaveChangesAsync();

        _messageBus.ClearReceivedCalls();
        return user;
    }

    private async Task<WallowUser> GivenVerifiedUserAsync()
    {
        WallowUser user = WallowUser.Create("Ada", "Lovelace", "ada@acme.test", _time);
        user.EmailConfirmed = true;
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task GivenPolicyAsync(EnrollmentPolicy policy)
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _organization.Id, _organization.TenantId, false, false, 0, _actorId, _time);
        settings.UpdateEnrollment(policy, null, null, _actorId, _time);
        _dbContext.OrganizationSettings.Add(settings);
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
