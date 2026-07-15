using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

public sealed class OrganizationSettings : AuditableEntity<OrganizationSettingsId>, ITenantScoped
{
    public OrganizationId OrganizationId { get; private set; }
    public TenantId TenantId { get; init; }
    public bool RequireMfa { get; private set; }
    public bool AllowPasswordlessLogin { get; private set; }
    public int MfaGracePeriodDays { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private OrganizationSettings() { } // EF Core

    private OrganizationSettings(
        OrganizationId organizationId,
        TenantId tenantId,
        bool requireMfa,
        bool allowPasswordlessLogin,
        int mfaGracePeriodDays,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = OrganizationSettingsId.New();
        OrganizationId = organizationId;
        TenantId = tenantId;
        RequireMfa = requireMfa;
        AllowPasswordlessLogin = allowPasswordlessLogin;
        MfaGracePeriodDays = mfaGracePeriodDays;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static OrganizationSettings Create(
        OrganizationId organizationId,
        TenantId tenantId,
        bool requireMfa,
        bool allowPasswordlessLogin,
        int mfaGracePeriodDays,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        return new OrganizationSettings(
            organizationId,
            tenantId,
            requireMfa,
            allowPasswordlessLogin,
            mfaGracePeriodDays,
            createdByUserId,
            timeProvider);
    }

    public void Update(bool requireMfa, bool allowPasswordlessLogin, int mfaGracePeriodDays, Guid updatedByUserId, TimeProvider timeProvider)
    {
        RequireMfa = requireMfa;
        AllowPasswordlessLogin = allowPasswordlessLogin;
        MfaGracePeriodDays = mfaGracePeriodDays;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
