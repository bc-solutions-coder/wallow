using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// A person's relationship with one organization. This is the entity that carries authorization:
/// roles hang off the membership, never off the user, so a role granted by one organization
/// confers nothing in another.
/// </summary>
/// <remarks>
/// Deliberately NOT ITenantScoped. OrganizationId is the scope and every read filters on it
/// explicitly; a TenantId column would duplicate it, and the tenant interceptor (which Identity
/// does not register) would overwrite it from the ambient tenant on insert, mis-stamping any
/// membership created while acting on behalf of a different organization.
/// </remarks>
public sealed class Membership : AggregateRoot<MembershipId>
{
    /// <summary>
    /// How long a denial stands before the same person may ask this organization again. A denial
    /// is an answer to one request, not a ban: left permanent, the only way back is an
    /// administrator noticing a row that appears on no list.
    /// </summary>
    public static readonly TimeSpan DenialCooldown = TimeSpan.FromDays(30);

    public Guid UserId { get; private set; }
    public OrganizationId OrganizationId { get; private set; }
    public MembershipStatus Status { get; private set; }

    /// <summary>
    /// Ownership only. Grants NO permission. It answers "who is the last person who cannot be
    /// removed" and seeds the access-request recipient fallback. Every authorization decision
    /// reads <see cref="RoleIds"/>.
    /// </summary>
    public bool IsOwner { get; private set; }

    public DateTimeOffset? RequestedAt { get; private set; }
    public DateTimeOffset? JoinedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }

    private readonly List<MembershipRole> _roles = [];

    /// <summary>
    /// The mapped navigation onto membership_roles. Callers want <see cref="RoleIds"/>; this
    /// exists so a query can reach the assignments without materializing memberships.
    /// </summary>
    public IReadOnlyCollection<MembershipRole> Roles => _roles;

    public IReadOnlyList<Guid> RoleIds => _roles.Select(r => r.RoleId).ToList().AsReadOnly();

    public bool IsActive => Status == MembershipStatus.Active;

    /// <summary>
    /// When a standing denial stops being an answer. Null unless the membership is denied — and
    /// null for a denial with no review timestamp, which resolves the unanswerable case in the
    /// direction that lets someone ask rather than the one that silently bars them forever.
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
    /// Creates a Pending membership. A Pending membership authenticates nothing and resolves
    /// no roles.
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

    /// <summary>
    /// Whether a denial is still the organization's answer.
    /// </summary>
    public bool IsWithinDenialCooldown(TimeProvider timeProvider) =>
        DeniedUntil is { } until && timeProvider.GetUtcNow() < until;

    /// <summary>
    /// The person asks again once a denial has run its course, and waits for a review as any
    /// requester would.
    /// </summary>
    /// <remarks>
    /// The row is reused rather than replaced: (UserId, OrganizationId) is unique, so deleting one
    /// and inserting another in the same save would race the index for no gain.
    /// </remarks>
    public void RequestAgain(TimeProvider timeProvider)
    {
        RequireDenialSpent(timeProvider);

        Status = MembershipStatus.Pending;
        RequestedAt = timeProvider.GetUtcNow();
        ClearReview();
        SetUpdated(timeProvider.GetUtcNow(), UserId);
    }

    /// <summary>
    /// The same second chance in an organization that admits anyone: there is no review to wait for.
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
    /// An administrator directly grants this membership a role. Unlike <see cref="Approve"/>
    /// this is not the review of a request: it activates the membership from whatever status
    /// it held, which is what "add this user to the organization" has to mean when a denied or
    /// suspended membership already exists.
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
