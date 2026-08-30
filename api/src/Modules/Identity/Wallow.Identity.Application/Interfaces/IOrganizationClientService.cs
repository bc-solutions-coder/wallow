using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The org-scoped client surface: an organization registers and manages its own clients. Every
/// method is addressed by organization, and a client that belongs to a different organization is
/// answered as not found rather than forbidden so the surface never confirms it exists.
/// </summary>
public interface IOrganizationClientService
{
    Task<OrganizationClientRegistrationResult> RegisterAsync(
        Guid organizationId,
        RegisterClientInput input,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<OrganizationClientDto>> ListAsync(Guid organizationId, CancellationToken ct = default);

    Task<OrganizationClientDto?> GetAsync(Guid organizationId, string clientId, CancellationToken ct = default);

    Task<OrganizationClientDto?> UpdateAsync(
        Guid organizationId,
        string clientId,
        ClientConfigurationInput configuration,
        CancellationToken ct = default);

    /// <summary>
    /// Replaces the client secret immediately, with no overlap: the old secret stops working the
    /// moment the new one is revealed. <paramref name="revokeActiveTokens"/> also ends every token
    /// the client was already issued. Returns <see langword="null"/> when the client is not one of
    /// the organization's.
    /// </summary>
    Task<OrganizationClientRegistrationResult?> RotateSecretAsync(
        Guid organizationId,
        string clientId,
        bool revokeActiveTokens,
        Guid actorUserId,
        CancellationToken ct = default);

    /// <summary>Returns false when the client is not one of the organization's.</summary>
    Task<bool> DeleteAsync(Guid organizationId, string clientId, CancellationToken ct = default);
}
