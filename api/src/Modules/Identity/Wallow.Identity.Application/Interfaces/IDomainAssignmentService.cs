namespace Wallow.Identity.Application.Interfaces;

public interface IDomainAssignmentService
{
    Task<Guid> RegisterDomainAsync(Guid organizationId, string domain, CancellationToken ct = default);
    Task VerifyDomainAsync(Guid domainId, string verificationToken, CancellationToken ct = default);
    Task<Guid> RequestMembershipAsync(Guid userId, string emailDomain, CancellationToken ct = default);

    /// <summary>
    /// Creates a membership request on behalf of a just-registered user, deriving the email domain
    /// from the address the user registered with rather than trusting a caller-supplied domain.
    /// </summary>
    /// <param name="userId">The identifier of the newly created user.</param>
    /// <param name="email">The email address the user registered with.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// The identifier of the created membership request, or <see langword="null"/> when the address
    /// has no domain part or its domain does not match a verified organization domain.
    /// </returns>
    Task<Guid?> RequestMembershipForRegistrationAsync(Guid userId, string email, CancellationToken ct = default);
    Task ApproveMembershipRequestAsync(Guid requestId, Guid organizationId, CancellationToken ct = default);
    Task RejectMembershipRequestAsync(Guid requestId, CancellationToken ct = default);
}
