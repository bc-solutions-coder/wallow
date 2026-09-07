using Wallow.Identity.Domain.Enums;
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

    /// <summary>How this organization admits people who are not members yet.</summary>
    public EnrollmentPolicy EnrollmentPolicy { get; private set; }

    /// <summary>
    /// Explicit access-request recipient. When absent, the resolver uses active owners
    /// with email addresses; it can return no recipients.
    /// </summary>
    public string? AccessRequestEmail { get; private set; }

    /// <summary>
    /// Default role for enrollment, approval, and invitation acceptance.
    /// Null or a deleted role falls back to the platform's baseline user role.
    /// </summary>
    public Guid? DefaultRoleId { get; private set; }

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
        EnrollmentPolicy = EnrollmentPolicy.InviteOnly;
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
            TenantScope.Require(tenantId),
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

    /// <summary>
    /// Updates enrollment settings separately from general settings because its endpoint
    /// requires <c>OrganizationsManageMembers</c>.
    /// </summary>
    public void UpdateEnrollment(
        EnrollmentPolicy enrollmentPolicy,
        string? accessRequestEmail,
        Guid? defaultRoleId,
        Guid updatedByUserId,
        TimeProvider timeProvider)
    {
        EnrollmentPolicy = enrollmentPolicy;
        AccessRequestEmail = string.IsNullOrWhiteSpace(accessRequestEmail) ? null : accessRequestEmail.Trim();
        DefaultRoleId = defaultRoleId;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
