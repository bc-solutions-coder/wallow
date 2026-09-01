using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The org-scoped client surface: an organization registers and manages its own clients. Every
/// method is addressed by organization, and a client that belongs to a different organization is
/// answered as not found rather than forbidden so the surface never confirms it exists. Every
/// mutation publishes its integration event in the same transaction as its writes, so a crash
/// after the commit can never drop the event; the <c>actor</c> is who performed it, for the
/// event's audit trail.
/// </summary>
public interface IOrganizationClientService
{
    /// <summary>
    /// Registers the client and publishes <c>ClientRegisteredEvent</c> in the same transaction as
    /// the writes, so the event — which is what creates the client's branding row — can never be
    /// lost to a crash after the commit.
    /// </summary>
    Task<OrganizationClientRegistrationResult> RegisterAsync(
        Guid organizationId,
        RegisterClientInput input,
        ClientActorContext actor,
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
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Takes the client out of service: every token it was issued is revoked and its realtime
    /// connections are hung up, while its configuration, branding and consents stay. Returns
    /// <see langword="null"/> when the client is not one of the organization's.
    /// </summary>
    Task<OrganizationClientDto?> SuspendAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Puts a suspended client back in service exactly as it was. Returns <see langword="null"/>
    /// when the client is not one of the organization's.
    /// </summary>
    Task<OrganizationClientDto?> ReinstateAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Places the platform's own suspension on the client, with the operator's reason: every
    /// token it was issued is revoked and its realtime connections are hung up, while the
    /// client's own status is untouched, so the organization can read the reason but none of
    /// its controls lift it. The published event also carries the organization's name and its
    /// admins' addresses, for the notification email. Returns <see langword="null"/> when the
    /// client is not one of the organization's.
    /// </summary>
    Task<OrganizationClientDto?> SuspendByPlatformAsync(
        Guid organizationId,
        string clientId,
        string reason,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Lifts the platform suspension; the client serves again unless the organization's own
    /// suspension still stands. Returns <see langword="null"/> when the client is not one of
    /// the organization's.
    /// </summary>
    Task<OrganizationClientDto?> ReinstateByPlatformAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes every credential the client holds, then removes it for good: the OpenIddict
    /// application with its tokens and consents, and Wallow's own record. Returns false when the
    /// client is not one of the organization's.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);
}
