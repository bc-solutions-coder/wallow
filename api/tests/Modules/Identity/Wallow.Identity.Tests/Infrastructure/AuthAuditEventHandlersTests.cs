using Wallow.Identity.Infrastructure.Handlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Identity.Tests.Infrastructure;

public class AuthAuditEventHandlersTests
{
    private readonly IAuthAuditService _auditService = Substitute.For<IAuthAuditService>();

    [Fact]
    public async Task Handle_UserLoginSucceededEvent_RecordsAudit()
    {
        UserLoginSucceededEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "10.0.0.1"
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "LoginSucceeded" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId &&
                r.IpAddress == "10.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserLoginFailedEvent_RecordsAudit()
    {
        UserLoginFailedEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "10.0.0.2",
            Reason = "InvalidPassword"
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "LoginFailed" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId &&
                r.IpAddress == "10.0.0.2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserAccountLockedOutEvent_RecordsAudit()
    {
        UserAccountLockedOutEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "10.0.0.3"
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "AccountLockedOut" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId &&
                r.IpAddress == "10.0.0.3"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserMfaLockedOutEvent_RecordsAudit()
    {
        UserMfaLockedOutEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LockoutCount = 5
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "MfaLockedOut" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// One event type per transition, spelled mechanically from the transition name, so a new
    /// transition needs no new handler and a query for "everything that happened to this
    /// membership" is a prefix match.
    /// </summary>
    [Theory]
    [InlineData(MembershipTransition.AccessRequested, "MembershipAccessRequested")]
    [InlineData(MembershipTransition.Enrolled, "MembershipEnrolled")]
    [InlineData(MembershipTransition.Added, "MembershipAdded")]
    [InlineData(MembershipTransition.Approved, "MembershipApproved")]
    [InlineData(MembershipTransition.Denied, "MembershipDenied")]
    [InlineData(MembershipTransition.DenialCleared, "MembershipDenialCleared")]
    [InlineData(MembershipTransition.Suspended, "MembershipSuspended")]
    [InlineData(MembershipTransition.Reinstated, "MembershipReinstated")]
    [InlineData(MembershipTransition.RoleAssigned, "MembershipRoleAssigned")]
    [InlineData(MembershipTransition.RoleRemoved, "MembershipRoleRemoved")]
    [InlineData(MembershipTransition.Left, "MembershipLeft")]
    [InlineData(MembershipTransition.Removed, "MembershipRemoved")]
    [InlineData(MembershipTransition.OwnerMarked, "MembershipOwnerMarked")]
    [InlineData(MembershipTransition.OwnerUnmarked, "MembershipOwnerUnmarked")]
    public async Task Handle_MembershipTransitionedEvent_RecordsTheTransitionUnderItsOwnEventType(
        MembershipTransition transition, string expectedEventType)
    {
        Guid organizationId = Guid.NewGuid();
        MembershipTransitionedEvent evt = new()
        {
            Transition = transition,
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = Guid.NewGuid(),
            ActorId = Guid.NewGuid()
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == expectedEventType &&
                r.UserId == evt.UserId &&
                r.ActorId == evt.ActorId &&
                r.TenantId == organizationId &&
                r.OccurredAt == evt.OccurredAt),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Who did it is the whole point of the record, so it survives even when it is the same person
    /// the record is about.
    /// </summary>
    [Fact]
    public async Task Handle_MembershipTransitionedEvent_KeepsTheActorWhenItIsTheSubject()
    {
        Guid userId = Guid.NewGuid();
        Guid organizationId = Guid.NewGuid();
        MembershipTransitionedEvent evt = new()
        {
            Transition = MembershipTransition.Left,
            OrganizationId = organizationId,
            TenantId = organizationId,
            UserId = userId,
            ActorId = userId
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r => r.ActorId == userId && r.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserLoginSucceededEvent_LeavesTheActorUnset()
    {
        UserLoginSucceededEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "10.0.0.1"
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r => r.ActorId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserLoginSucceededEvent_WithNullIpAddress_RecordsAudit()
    {
        UserLoginSucceededEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = null
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.IpAddress == null),
            Arg.Any<CancellationToken>());
    }
}
