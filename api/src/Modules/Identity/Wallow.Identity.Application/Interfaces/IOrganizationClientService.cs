using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The org-scoped client surface: an organization registers and manages its own clients. Every
/// method is addressed by organization, and a client that belongs to a different organization is
/// answered as not found rather than forbidden so the surface never confirms it exists.
/// </summary>
public interface IOrganizationClientService
{
    Task<OrganizationClientRegistrationResult> RegisterApplicationAsync(
        Guid organizationId,
        RegisterApplicationInput input,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<OrganizationClientDto>> ListAsync(Guid organizationId, CancellationToken ct = default);

    Task<OrganizationClientDto?> GetAsync(Guid organizationId, string clientId, CancellationToken ct = default);

    Task<OrganizationClientDto?> UpdateAsync(
        Guid organizationId,
        string clientId,
        UpdateOrganizationClientInput input,
        CancellationToken ct = default);

    /// <summary>Returns false when the client is not one of the organization's.</summary>
    Task<bool> DeleteAsync(Guid organizationId, string clientId, CancellationToken ct = default);
}
