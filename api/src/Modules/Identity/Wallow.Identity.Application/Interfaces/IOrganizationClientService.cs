using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Organization-scoped client management. Reads and mutations do not return another
/// organization's client. Registration and lifecycle events use the transactional outbox;
/// configuration updates publish no event. Callers supply the audit actor.
/// </summary>
public interface IOrganizationClientService
{
    /// <summary>
    /// Creates the OpenIddict application and registration record with ClientRegisteredEvent
    /// in the same transaction. Branding consumes the event asynchronously.
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
    /// Replaces the secret without a grace period and optionally revokes existing tokens.
    /// Returns null when the owned client or its OpenIddict application is absent.
    /// </summary>
    Task<OrganizationClientRegistrationResult?> RotateSecretAsync(
        Guid organizationId,
        string clientId,
        bool revokeActiveTokens,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Suspends the client and revokes its tokens while retaining configuration, branding
    /// and consent. Returns null when the owned client or application is absent.
    /// </summary>
    Task<OrganizationClientDto?> SuspendAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Clears organization-level client suspension. Platform or organization restrictions
    /// may still block service; revoked tokens remain revoked. Returns null if not found.
    /// </summary>
    Task<OrganizationClientDto?> ReinstateAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Records platform suspension separately from client status and revokes client tokens.
    /// The event carries the reason and organization admin recipients. Returns null if not found.
    /// </summary>
    Task<OrganizationClientDto?> SuspendByPlatformAsync(
        Guid organizationId,
        string clientId,
        string reason,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Clears platform suspension without changing client status or organization state.
    /// Returns null when the owned client or its application is absent.
    /// </summary>
    Task<OrganizationClientDto?> ReinstateByPlatformAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes client credentials, deletes its application and registration, and publishes
    /// the deletion event transactionally. Returns false when no owned registration exists.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default);
}
