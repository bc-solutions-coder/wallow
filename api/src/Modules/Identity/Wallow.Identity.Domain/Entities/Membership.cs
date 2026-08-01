using Wallow.Identity.Domain.Enums;
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

    public IReadOnlyList<Guid> RoleIds => _roles.Select(r => r.RoleId).ToList().AsReadOnly();

    public bool IsActive => Status == MembershipStatus.Active;

    // ReSharper disable once UnusedMember.Local
    private Membership() { } // EF Core

    private Membership(Guid userId, OrganizationId organizationId, TimeProvider timeProvider)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Identity.UserIdRequired",
                "User ID cannot be empty");
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
            throw new BusinessRuleException(
                "Identity.MembershipNotPending",
                "Only a pending membership can be approved");
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
            throw new BusinessRuleException(
                "Identity.MembershipNotPending",
                "Only a pending membership can be denied");
        }

        Status = MembershipStatus.Denied;
        ReviewedAt = timeProvider.GetUtcNow();
        ReviewedBy = deniedByUserId;
        _roles.Clear();
        SetUpdated(timeProvider.GetUtcNow(), deniedByUserId);
    }

    public void Suspend(Guid suspendedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Active)
        {
            throw new BusinessRuleException(
                "Identity.MembershipNotActive",
                "Only an active membership can be suspended");
        }

        Status = MembershipStatus.Suspended;
        SetUpdated(timeProvider.GetUtcNow(), suspendedByUserId);
    }

    public void Reinstate(Guid reinstatedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipStatus.Suspended)
        {
            throw new BusinessRuleException(
                "Identity.MembershipNotSuspended",
                "Only a suspended membership can be reinstated");
        }

        Status = MembershipStatus.Active;
        SetUpdated(timeProvider.GetUtcNow(), reinstatedByUserId);
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
}
