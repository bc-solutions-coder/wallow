using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

public sealed class OrganizationBranding : AuditableEntity<OrganizationBrandingId>, ITenantScoped
{
    public OrganizationId OrganizationId { get; private set; }
    public TenantId TenantId { get; init; }
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? AccentColor { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private OrganizationBranding() { } // EF Core

    private OrganizationBranding(
        OrganizationId organizationId,
        TenantId tenantId,
        string? logoUrl,
        string? primaryColor,
        string? accentColor,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = OrganizationBrandingId.New();
        OrganizationId = organizationId;
        TenantId = tenantId;
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        AccentColor = accentColor;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static OrganizationBranding Create(
        OrganizationId organizationId,
        TenantId tenantId,
        string? logoUrl,
        string? primaryColor,
        string? accentColor,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        return new OrganizationBranding(
            organizationId,
            tenantId,
            logoUrl,
            primaryColor,
            accentColor,
            createdByUserId,
            timeProvider);
    }

    public void Update(string? logoUrl, string? primaryColor, string? accentColor, Guid updatedByUserId, TimeProvider timeProvider)
    {
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        AccentColor = accentColor;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
