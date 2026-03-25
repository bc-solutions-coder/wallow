using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class DomainAssignmentService(
    IOrganizationDomainRepository domainRepository,
    IMembershipRequestRepository membershipRequestRepository,
    IMessageBus messageBus,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<DomainAssignmentService> logger) : IDomainAssignmentService
{
    public async Task<Guid> RegisterDomainAsync(Guid organizationId, string domain, CancellationToken ct = default)
    {
        LogRegisteringDomain(domain, organizationId);

        string verificationToken = Guid.NewGuid().ToString("N");

        OrganizationDomain orgDomain = OrganizationDomain.Create(
            tenantContext.TenantId,
            OrganizationId.Create(organizationId),
            domain,
            verificationToken,
            Guid.Empty,
            timeProvider);

        domainRepository.Add(orgDomain);
        await domainRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationDomainRegisteredEvent
        {
            OrganizationDomainId = orgDomain.Id.Value,
            OrganizationId = organizationId,
            TenantId = tenantContext.TenantId.Value,
            Domain = domain.ToLowerInvariant()
        });

        LogDomainRegistered(domain, orgDomain.Id.Value);
        return orgDomain.Id.Value;
    }

    public async Task VerifyDomainAsync(Guid domainId, string verificationToken, CancellationToken ct = default)
    {
        LogVerifyingDomain(domainId);

        OrganizationDomainId id = OrganizationDomainId.Create(domainId);
        OrganizationDomain? orgDomain = await domainRepository.GetByIdAsync(id, ct);

        if (orgDomain is null)
        {
            throw new EntityNotFoundException("OrganizationDomain", domainId);
        }

        if (!string.Equals(orgDomain.VerificationToken, verificationToken, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "Identity.InvalidVerificationToken",
                "The verification token does not match");
        }

        orgDomain.Verify(Guid.Empty, timeProvider);
        await domainRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationDomainVerifiedEvent
        {
            OrganizationDomainId = orgDomain.Id.Value,
            OrganizationId = orgDomain.OrganizationId.Value,
            TenantId = tenantContext.TenantId.Value,
            Domain = orgDomain.Domain
        });

        LogDomainVerified(domainId);
    }

    public async Task<Guid> RequestMembershipAsync(Guid userId, string emailDomain, CancellationToken ct = default)
    {
        LogRequestingMembership(userId, emailDomain);

        MembershipRequest request = MembershipRequest.Create(
            tenantContext.TenantId,
            userId,
            emailDomain,
            userId,
            timeProvider);

        membershipRequestRepository.Add(request);
        await membershipRequestRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new MembershipRequestCreatedEvent
        {
            MembershipRequestId = request.Id.Value,
            TenantId = tenantContext.TenantId.Value,
            UserId = userId,
            EmailDomain = emailDomain.ToLowerInvariant()
        });

        LogMembershipRequested(request.Id.Value);
        return request.Id.Value;
    }

    public async Task ApproveMembershipRequestAsync(Guid requestId, Guid organizationId, CancellationToken ct = default)
    {
        LogApprovingMembershipRequest(requestId);

        MembershipRequestId id = MembershipRequestId.Create(requestId);
        MembershipRequest? request = await membershipRequestRepository.GetByIdAsync(id, ct);

        if (request is null)
        {
            throw new EntityNotFoundException("MembershipRequest", requestId);
        }

        request.Approve(OrganizationId.Create(organizationId), Guid.Empty, timeProvider);
        await membershipRequestRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new MembershipRequestApprovedEvent
        {
            MembershipRequestId = request.Id.Value,
            TenantId = tenantContext.TenantId.Value,
            UserId = request.UserId,
            OrganizationId = organizationId,
            EmailDomain = request.EmailDomain
        });

        LogMembershipRequestApproved(requestId);
    }

    public async Task RejectMembershipRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        LogRejectingMembershipRequest(requestId);

        MembershipRequestId id = MembershipRequestId.Create(requestId);
        MembershipRequest? request = await membershipRequestRepository.GetByIdAsync(id, ct);

        if (request is null)
        {
            throw new EntityNotFoundException("MembershipRequest", requestId);
        }

        request.Reject(Guid.Empty, timeProvider);
        await membershipRequestRepository.SaveChangesAsync(ct);

        LogMembershipRequestRejected(requestId);
    }
}

public sealed partial class DomainAssignmentService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Registering domain {Domain} for organization {OrgId}")]
    private partial void LogRegisteringDomain(string domain, Guid orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Domain {Domain} registered with ID {DomainId}")]
    private partial void LogDomainRegistered(string domain, Guid domainId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verifying domain {DomainId}")]
    private partial void LogVerifyingDomain(Guid domainId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Domain {DomainId} verified")]
    private partial void LogDomainVerified(Guid domainId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} requesting membership for domain {EmailDomain}")]
    private partial void LogRequestingMembership(Guid userId, string emailDomain);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership request {RequestId} created")]
    private partial void LogMembershipRequested(Guid requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Approving membership request {RequestId}")]
    private partial void LogApprovingMembershipRequest(Guid requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership request {RequestId} approved")]
    private partial void LogMembershipRequestApproved(Guid requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejecting membership request {RequestId}")]
    private partial void LogRejectingMembershipRequest(Guid requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Membership request {RequestId} rejected")]
    private partial void LogMembershipRequestRejected(Guid requestId);
}
