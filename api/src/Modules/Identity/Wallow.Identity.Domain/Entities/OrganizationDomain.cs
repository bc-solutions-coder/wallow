using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

public sealed class OrganizationDomain : AggregateRoot<OrganizationDomainId>, ITenantScoped
{
    public OrganizationId OrganizationId { get; init; }
    public TenantId TenantId { get; init; }
    public string Domain { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public string VerificationToken { get; private set; } = string.Empty;

    // ReSharper disable once UnusedMember.Local
    private OrganizationDomain() { } // EF Core

    private OrganizationDomain(
        TenantId tenantId,
        OrganizationId organizationId,
        string domain,
        string verificationToken,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = OrganizationDomainId.New();
        TenantId = tenantId;
        OrganizationId = organizationId;
        Domain = domain.ToLowerInvariant();
        IsVerified = false;
        VerificationToken = verificationToken;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static OrganizationDomain Create(
        TenantId tenantId,
        OrganizationId organizationId,
        string domain,
        string verificationToken,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new BusinessRuleException(
                "Identity.DomainRequired",
                "Domain cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(verificationToken))
        {
            throw new BusinessRuleException(
                "Identity.VerificationTokenRequired",
                "Verification token cannot be empty");
        }

        return new OrganizationDomain(tenantId, organizationId, domain, verificationToken, createdByUserId, timeProvider);
    }

    public void Verify(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (IsVerified)
        {
            throw new BusinessRuleException(
                "Identity.DomainAlreadyVerified",
                "Domain is already verified");
        }

        IsVerified = true;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
