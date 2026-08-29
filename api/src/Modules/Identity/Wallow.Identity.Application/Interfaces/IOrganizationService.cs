using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Application.Interfaces;

public interface IOrganizationService
{
    // creatorUserId: the authenticated user creating the organization. When provided, the creator
    // gets an Active owner membership carrying the admin role, and is stamped as the audit
    // "created by" user. Leave null for system-initiated creation (e.g. SCIM sync,
    // pre-registered client provisioning).
    Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, Guid? creatorUserId = null, CancellationToken ct = default);
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default);

    // roleName names the role this organization grants the member. Roles are per (user,
    // organization), so there is no implicit default: an add-path that picks a role for the
    // caller is the cross-org escalation surface this model exists to close.
    //
    // actorId is the user performing the change, which is never the member being added or removed.
    // Stamping the membership with userId instead erases who granted the access - the one question
    // the audit trail exists to answer.
    Task AddMemberAsync(Guid orgId, Guid userId, string roleName, Guid actorId, CancellationToken ct = default);

    // The bootstrap administrator claiming an organization that already exists - the one the seed
    // created and bound the dashboard client to before any person did. It grants what
    // CreateOrganizationAsync grants its creator (an Active owner membership carrying the admin
    // role) and nothing an existing member could reach: it is not exposed by any endpoint.
    Task EnrollOwnerAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid orgId, Guid userId, Guid actorId, CancellationToken ct = default);

    // Suspending, reinstating, approving and denying live on IMembershipReviewService: they are
    // one reviewer's four answers to the same question, and each one that ends access has to
    // revoke what the member already holds.
    Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The doors this person may walk through, for the app that can only open one of them.
    /// Active memberships of organizations that are themselves active, and nothing else: a
    /// pending request is not an answer yet, and an archived organization is not a door.
    /// </summary>
    Task<IReadOnlyList<MyOrganizationDto>> GetMyOrganizationsAsync(Guid userId, CancellationToken ct = default);

    Task ArchiveAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);
    Task ReactivateAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);
    Task DeleteAsync(Guid organizationId, string confirmedName, CancellationToken ct = default);
    Task<OrganizationSettingsDto?> GetSettingsAsync(Guid organizationId, CancellationToken ct = default);
    Task UpdateSettingsAsync(Guid organizationId, bool requireMfa, bool allowPasswordlessLogin, int mfaGracePeriodDays, Guid actorId, CancellationToken ct = default);

    // Separate from UpdateSettingsAsync because these three decide who belongs to the organization,
    // and are therefore gated on OrganizationsManageMembers rather than OrganizationsUpdate. One
    // method spanning both permissions could only either write part of a request or refuse all of it.
    Task UpdateEnrollmentAsync(Guid organizationId, EnrollmentPolicy enrollmentPolicy, string? accessRequestEmail, Guid? defaultRoleId, Guid actorId, CancellationToken ct = default);
    Task<OrganizationBrandingDto?> GetBrandingAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationBrandingDto> UpdateBrandingAsync(Guid organizationId, string? displayName, string? logoUrl, string? primaryColor, Guid actorId, CancellationToken ct = default);
    Task<string> UploadBrandingLogoAsync(Guid organizationId, Stream logoStream, string fileName, string contentType, Guid actorId, CancellationToken ct = default);
}
