namespace Wallow.Identity.Application.Interfaces;

public interface IDomainAssignmentService
{
    Task<Guid> RegisterDomainAsync(Guid organizationId, string domain, CancellationToken ct = default);
    Task VerifyDomainAsync(Guid domainId, string verificationToken, CancellationToken ct = default);
    Task<Guid> RequestMembershipAsync(Guid userId, string emailDomain, CancellationToken ct = default);
    Task ApproveMembershipRequestAsync(Guid requestId, Guid organizationId, CancellationToken ct = default);
    Task RejectMembershipRequestAsync(Guid requestId, CancellationToken ct = default);
}
