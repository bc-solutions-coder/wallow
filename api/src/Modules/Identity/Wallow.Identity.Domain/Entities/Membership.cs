using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// A person's organization membership and role assignments.
/// OrganizationId supplies the scope; this entity has no ambient tenant filter,
/// so callers must explicitly constrain reads to the intended user or organization.
/// </summary>
public sealed class Membership : AggregateRoot<MembershipId>
{
    /// <summary>
    /// Cooldown before a denied person may request access or enroll again.
    /// </summary>
    public static readonly TimeSpan DenialCooldown = TimeSpan.FromDays(30);

    public Guid UserId { get; private set; }
    public OrganizationId OrganizationId { get; private set; }
    public MembershipStatus Status { get; private set; }

    /// <summary>
    /// Ownership marker for last-owner protection and access-request recipient fallback.
    /// It does not grant permissions; role resolution uses <see cref="RoleIds"/>.
    /// </summary>
    public bool IsOwner { get; private set; }

    public DateTimeOffset? RequestedAt { get; private set; }
    public DateTimeOffset? JoinedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }

    private readonly List<MembershipRole> _roles = [];

    /// <summary>
    /// Mapped role assignments for querying without materializing memberships.
    /// Use <see cref="RoleIds"/> when only assignment IDs are needed.
    /// </summary>
    public IReadOnlyCollection<MembershipRole> Roles => _roles;

    public IReadOnlyList<Guid> RoleIds => _roles.Select(r => r.RoleId).ToList().AsReadOnly();

    public bool IsActive => Status == MembershipStatus.Active;

    /// <summary>
    /// Denial expiry, or null when not denied or when the review timestamp is missing.
    /// A missing timestamp does not prevent another request.
    /// </summary>
    public DateTimeOffset? DeniedUntil =>
        Status == MembershipStatus.Denied && ReviewedAt is { } reviewedAt
            ? reviewedAt + DenialCooldown
            : null;

    // ReSharper disable once UnusedMember.Local
    private Membership() { } // EF Core

    private Membership(Guid userId, OrganizationId organizationId, TimeProvider timeProvider)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException(IdentityErrors.UserIdRequired);
        }

        Id = MembershipId.New();
        UserId = userId;
        OrganizationId = organizationId;
        SetCreated(timeProvider.GetUtcNow(), userId);
    }

    /// <summary>
    /// Creates a pending membership with no role assignments.
    /// </summary>
    public static Membership RequestAccess(
        Guid userId,
        OrganizationId organizationId,
        TimeProvider timeProvider)
    {
        return new Membership(userId, organizationId, timeProvider)
        {
            Status = MembershipStatus.Pending,
            RequestedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>
    /// Creates an Active membership directly, for Open enrollment or invitation acceptance.
    /// </summary>
    public static Membership Enroll(
        Guid userId,
        OrganizationId organizationId,
        Guid defaultRoleId,
        TimeProvider timeProvider)
    {
        Membership membership = new(userId, organizationId, timeProvider)
        {
            Status = MembershipStatus.Active,
            JoinedAt = timeProvider.GetUtcNow()
        };

        membership._roles.Add(new MembershipRole(membership.Id, defaultRoleId));
        return membership;
    }

    public void Approve(Guid defaultRoleId, Guid approvedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Pending)
        {
            throw new BusinessRuleException(IdentityErrors.MembershipNotPending, "Only a pending membership can be approved");
        }

        Status = MembershipStatus.Active;
        JoinedAt = timeProvider.GetUtcNow();
        ReviewedAt = timeProvider.GetUtcNow();
        ReviewedBy = approvedByUserId;
        AssignRole(defaultRoleId, approvedByUserId, timeProvider);
        SetUpdated(timeProvider.GetUtcNow(), approvedByUserId);
    }

    public void Deny(Guid deniedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Pending)
        {
            throw new BusinessRuleException(IdentityErrors.MembershipNotPending, "Only a pending membership can be denied");
        }

        Status = MembershipStatus.Denied;
        ReviewedAt = timeProvider.GetUtcNow();
        ReviewedBy = deniedByUserId;
        _roles.Clear();
        SetUpdated(timeProvider.GetUtcNow(), deniedByUserId);
    }

    public bool IsWithinDenialCooldown(TimeProvider timeProvider) =>
    DeniedUntil is { } until && timeProvider.GetUtcNow() < until;

    /// <summary>
    /// Reuses a denied membership for another request after the cooldown.
    /// Reusing the row preserves the unique (UserId, OrganizationId) relationship.
    /// </summary>
    public void RequestAgain(TimeProvider timeProvider)
    {
        RequireDenialSpent(timeProvider);

        Status = MembershipStatus.Pending;
        RequestedAt = timeProvider.GetUtcNow();
        ClearReview();
        SetUpdated(timeProvider.GetUtcNow(), UserId);
    }

    /// <summary>
    /// Reactivates a denied membership with the default role after the cooldown.
    /// </summary>
    public void EnrollAgain(Guid defaultRoleId, TimeProvider timeProvider)
    {
        RequireDenialSpent(timeProvider);

        Status = MembershipStatus.Active;
        JoinedAt = timeProvider.GetUtcNow();
        ClearReview();
        AssignRole(defaultRoleId, UserId, timeProvider);
        SetUpdated(timeProvider.GetUtcNow(), UserId);
    }

    public void Suspend(Guid suspendedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Active)
        {
            throw new BusinessRuleException(IdentityErrors.MembershipNotActive);
        }

        Status = MembershipStatus.Suspended;
        SetUpdated(timeProvider.GetUtcNow(), suspendedByUserId);
    }

    public void Reinstate(Guid reinstatedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Suspended)
        {
            throw new BusinessRuleException(IdentityErrors.MembershipNotSuspended);
        }

        Status = MembershipStatus.Active;
        SetUpdated(timeProvider.GetUtcNow(), reinstatedByUserId);
    }

    /// <summary>
    /// Grants a role and activates the membership from any status.
    /// Unlike <see cref="Approve"/>, this does not require a pending request.
    /// </summary>
    public void Grant(Guid roleId, Guid grantedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Active)
        {
            Status = MembershipStatus.Active;
            JoinedAt ??= timeProvider.GetUtcNow();
        }

        AssignRole(roleId, grantedByUserId, timeProvider);
        SetUpdated(timeProvider.GetUtcNow(), grantedByUserId);
    }

    public void AssignRole(Guid roleId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (_roles.Exists(r => r.RoleId == roleId))
        {
            return;
        }

        _roles.Add(new MembershipRole(Id, roleId));
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void RemoveRole(Guid roleId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (_roles.RemoveAll(r => r.RoleId == roleId) > 0)
        {
            SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
        }
    }

    public void MarkOwner(bool isOwner, Guid updatedByUserId, TimeProvider timeProvider)
    {
        IsOwner = isOwner;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    private void RequireDenialSpent(TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Denied)
        {
            throw new BusinessRuleException(IdentityErrors.MembershipNotDenied);
        }

        if (IsWithinDenialCooldown(timeProvider))
        {
            throw new BusinessRuleException(IdentityErrors.DenialCooldown);
        }
    }

    private void ClearReview()
    {
        ReviewedAt = null;
        ReviewedBy = null;
    }
}
