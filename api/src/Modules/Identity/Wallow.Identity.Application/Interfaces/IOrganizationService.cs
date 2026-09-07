using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Application.Interfaces;

public interface IOrganizationService
{
    // A creator receives an active owner membership with the admin role and audit attribution.
    // Null creates an organization without a member, with Guid.Empty audit attribution.
    Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, Guid? creatorUserId = null, CancellationToken ct = default);
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default);

    // roleName is granted only in this organization. actorId identifies who performed the
    // change; it may equal userId when the actor and subject are the same person.
    Task AddMemberAsync(Guid orgId, Guid userId, string roleName, Guid actorId, CancellationToken ct = default);

    // Bootstrap grants an active owner membership with the admin role in an existing organization.
    Task EnrollOwnerAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid orgId, Guid userId, Guid actorId, CancellationToken ct = default);

    // Review transitions and leaving are exposed by IMembershipReviewService.
    Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Active memberships in active organizations. Platform suspension is not filtered here.
    /// </summary>
    Task<IReadOnlyList<MyOrganizationDto>> GetMyOrganizationsAsync(Guid userId, CancellationToken ct = default);

    Task ArchiveAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);
    Task ReactivateAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Deletes organization-owned Identity records and writes the deletion event in one
    /// transaction, after revoking access. Realtime disconnection is an external effect
    /// and cannot roll back. Requires exact name confirmation; platform suspension permits
    /// only operator deletion.
    /// </summary>
    Task DeleteAsync(
        Guid organizationId,
        string confirmedName,
        Guid actorId,
        bool byPlatformOperator,
        CancellationToken ct = default);

    /// <summary>
    /// Records platform suspension and revokes registered clients' and members' access.
    /// Organization active state and individual client status remain separate.
    /// </summary>
    Task SuspendByPlatformAsync(Guid organizationId, string reason, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Clears platform suspension without restoring revoked credentials or changing
    /// organization active state or individual client suspension.
    /// </summary>
    Task ReinstateByPlatformAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);
    Task<OrganizationSettingsDto?> GetSettingsAsync(Guid organizationId, CancellationToken ct = default);
    Task UpdateSettingsAsync(Guid organizationId, bool requireMfa, bool allowPasswordlessLogin, int mfaGracePeriodDays, Guid actorId, CancellationToken ct = default);

    // Enrollment changes require OrganizationsManageMembers, separately from settings updates.
    Task UpdateEnrollmentAsync(Guid organizationId, EnrollmentPolicy enrollmentPolicy, string? accessRequestEmail, Guid? defaultRoleId, Guid actorId, CancellationToken ct = default);
    Task<OrganizationBrandingDto?> GetBrandingAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationBrandingDto> UpdateBrandingAsync(Guid organizationId, string? displayName, string? logoUrl, string? primaryColor, Guid actorId, CancellationToken ct = default);
    Task<string> UploadBrandingLogoAsync(Guid organizationId, Stream logoStream, string fileName, string contentType, Guid actorId, CancellationToken ct = default);
}
