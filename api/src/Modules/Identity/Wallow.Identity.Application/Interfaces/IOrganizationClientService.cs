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

    /// <summary>
    /// Takes the client out of service: every token it was issued is revoked and its realtime
    /// connections are hung up, while its configuration, branding and consents stay. Returns
    /// <see langword="null"/> when the client is not one of the organization's.
    /// </summary>
    Task<OrganizationClientDto?> SuspendAsync(Guid organizationId, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Puts a suspended client back in service exactly as it was. Returns <see langword="null"/>
    /// when the client is not one of the organization's.
    /// </summary>
    Task<OrganizationClientDto?> ReinstateAsync(Guid organizationId, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Revokes every credential the client holds, then removes it for good: the OpenIddict
    /// application with its tokens and consents, and Wallow's own record. Returns false when the
    /// client is not one of the organization's.
    /// </summary>
    Task<bool> DeleteAsync(Guid organizationId, string clientId, CancellationToken ct = default);
}
