using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

public interface IOrganizationService
{
    // creatorUserId: the authenticated user creating the organization. When provided, the creator is
    // added as an admin member and stamped as the audit "created by" user. Leave null for
    // system-initiated creation (e.g. SCIM sync, pre-registered client provisioning).
    Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, Guid? creatorUserId = null, CancellationToken ct = default);
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default);
    Task AddMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default);
    Task ArchiveAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);
    Task ReactivateAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);
    Task DeleteAsync(Guid organizationId, string confirmedName, CancellationToken ct = default);
    Task<OrganizationSettingsDto?> GetSettingsAsync(Guid organizationId, CancellationToken ct = default);
    Task UpdateSettingsAsync(Guid organizationId, bool requireMfa, bool allowPasswordlessLogin, int mfaGracePeriodDays, Guid actorId, CancellationToken ct = default);
    Task<OrganizationBrandingDto?> GetBrandingAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationBrandingDto> UpdateBrandingAsync(Guid organizationId, string? displayName, string? logoUrl, string? primaryColor, Guid actorId, CancellationToken ct = default);
    Task<string> UploadBrandingLogoAsync(Guid organizationId, Stream logoStream, string fileName, string contentType, Guid actorId, CancellationToken ct = default);
}
