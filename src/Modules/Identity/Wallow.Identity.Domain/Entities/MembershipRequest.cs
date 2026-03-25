using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

public sealed class MembershipRequest : AggregateRoot<MembershipRequestId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; init; }
    public string EmailDomain { get; private set; } = string.Empty;
    public MembershipRequestStatus Status { get; private set; }
    public OrganizationId? ResolvedOrganizationId { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private MembershipRequest() { } // EF Core

    private MembershipRequest(
        TenantId tenantId,
        Guid userId,
        string emailDomain,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = MembershipRequestId.New();
        TenantId = tenantId;
        UserId = userId;
        EmailDomain = emailDomain.ToLowerInvariant();
        Status = MembershipRequestStatus.Pending;
        ResolvedOrganizationId = null;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static MembershipRequest Create(
        TenantId tenantId,
        Guid userId,
        string emailDomain,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Identity.UserIdRequired",
                "User ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(emailDomain))
        {
            throw new BusinessRuleException(
                "Identity.EmailDomainRequired",
                "Email domain cannot be empty");
        }

        return new MembershipRequest(tenantId, userId, emailDomain, createdByUserId, timeProvider);
    }

    public void Approve(OrganizationId organizationId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipRequestStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.MembershipRequestNotPending",
                "Membership request is not in a pending state");
        }

        Status = MembershipRequestStatus.Approved;
        ResolvedOrganizationId = organizationId;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void Reject(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != MembershipRequestStatus.Pending)
        {
            throw new BusinessRuleException(
                "Identity.MembershipRequestNotPending",
                "Membership request is not in a pending state");
        }

        Status = MembershipRequestStatus.Rejected;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
