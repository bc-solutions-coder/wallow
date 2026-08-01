using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Tests.Domain;

/// <summary>
/// Membership is the entity that carries authorization: roles hang off the (user, organization)
/// pair, so a role granted by one organization confers nothing in another.
/// </summary>
public class MembershipTests
{
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly OrganizationId _orgId = OrganizationId.New();
    private static readonly Guid _actorId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void RequestAccess_CreatesPendingMembership_GrantingNothing()
    {
        Membership membership = Membership.RequestAccess(_userId, _orgId, _timeProvider);

        membership.Status.Should().Be(MembershipStatus.Pending);
        membership.RoleIds.Should().BeEmpty();
        membership.IsOwner.Should().BeFalse();
        membership.JoinedAt.Should().BeNull();
        membership.RequestedAt.Should().NotBeNull();
    }

    [Fact]
    public void Enroll_CreatesActiveMembership_CarryingTheDefaultRole()
    {
        Guid defaultRoleId = Guid.NewGuid();

        Membership membership = Membership.Enroll(_userId, _orgId, defaultRoleId, _timeProvider);

        membership.Status.Should().Be(MembershipStatus.Active);
        membership.RoleIds.Should().BeEquivalentTo([defaultRoleId]);
        membership.JoinedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_ActivatesPendingMembership_AndRecordsTheReviewer()
    {
        Guid defaultRoleId = Guid.NewGuid();
        Membership membership = Membership.RequestAccess(_userId, _orgId, _timeProvider);

        membership.Approve(defaultRoleId, _actorId, _timeProvider);

        membership.Status.Should().Be(MembershipStatus.Active);
        membership.ReviewedBy.Should().Be(_actorId);
        membership.ReviewedAt.Should().NotBeNull();
        membership.RoleIds.Should().BeEquivalentTo([defaultRoleId]);
    }

    [Fact]
    public void Approve_Throws_WhenMembershipIsNotPending()
    {
        Membership membership = Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _timeProvider);

        Action act = () => membership.Approve(Guid.NewGuid(), _actorId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.MembershipNotPending");
    }

    [Fact]
    public void Deny_RecordsTheReview_AndLeavesNoRoles()
    {
        Membership membership = Membership.RequestAccess(_userId, _orgId, _timeProvider);

        membership.Deny(_actorId, _timeProvider);

        membership.Status.Should().Be(MembershipStatus.Denied);
        membership.RoleIds.Should().BeEmpty();
        membership.ReviewedBy.Should().Be(_actorId);
    }

    [Fact]
    public void Suspend_KeepsTheRoles_SoReinstatingRestoresThem()
    {
        Membership membership = Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _timeProvider);

        membership.Suspend(_actorId, _timeProvider);

        membership.Status.Should().Be(MembershipStatus.Suspended);
        membership.RoleIds.Should().ContainSingle();
    }

    [Fact]
    public void Reinstate_RestoresActiveStatus()
    {
        Membership membership = Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _timeProvider);
        membership.Suspend(_actorId, _timeProvider);

        membership.Reinstate(_actorId, _timeProvider);

        membership.Status.Should().Be(MembershipStatus.Active);
        membership.RoleIds.Should().ContainSingle();
    }

    [Fact]
    public void AssignRole_IsIdempotent()
    {
        Guid roleId = Guid.NewGuid();
        Membership membership = Membership.Enroll(_userId, _orgId, roleId, _timeProvider);

        membership.AssignRole(roleId, _actorId, _timeProvider);

        membership.RoleIds.Should().ContainSingle();
    }

    [Fact]
    public void RemoveRole_DropsTheAssignment()
    {
        Guid roleId = Guid.NewGuid();
        Membership membership = Membership.Enroll(_userId, _orgId, roleId, _timeProvider);

        membership.RemoveRole(roleId, _actorId, _timeProvider);

        membership.RoleIds.Should().BeEmpty();
    }

    [Fact]
    public void IsActive_IsTrueOnlyForActive()
    {
        Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _timeProvider).IsActive.Should().BeTrue();
        Membership.RequestAccess(_userId, _orgId, _timeProvider).IsActive.Should().BeFalse();
    }

    [Fact]
    public void RequestAccess_Throws_WhenUserIdIsEmpty()
    {
        Action act = () => Membership.RequestAccess(Guid.Empty, _orgId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.UserIdRequired");
    }

    [Fact]
    public void MarkOwner_SetsOwnership_WithoutGrantingRoles()
    {
        Membership membership = Membership.Enroll(_userId, _orgId, Guid.NewGuid(), _timeProvider);

        membership.MarkOwner(true, _actorId, _timeProvider);

        membership.IsOwner.Should().BeTrue();
        membership.RoleIds.Should().ContainSingle();
    }
}
