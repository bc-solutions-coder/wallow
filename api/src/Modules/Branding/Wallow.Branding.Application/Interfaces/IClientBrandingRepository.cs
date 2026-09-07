using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Application.Interfaces;

public interface IClientBrandingRepository
{
    Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Reads the current stored display name without tracking or tenant filters.
    /// Returns <see langword="null"/> when absent; use for synchronization reads.
    /// </summary>
    Task<string?> FindDisplayNameAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Sets the tenant for this unit of work, including newly added rows. Call before acting
    /// for another organization; changing ambient tenant context does not update an existing DbContext.
    /// </summary>
    void UseTenant(TenantId tenantId);

    /// <summary>
    /// Lists tracked rows for the addressed tenant. Call <see cref="UseTenant"/> first
    /// when acting for another organization.
    /// </summary>
    Task<IReadOnlyList<ClientBranding>> ListAsync(CancellationToken ct = default);

    void Add(ClientBranding branding);
    void Remove(ClientBranding branding);

    /// <summary>
    /// Persists changes. A conflicting insert throws <see cref="Exceptions.DuplicateClientBrandingException"/>
    /// with added rows detached so callers can re-fetch and update the winner. Concurrent deletion throws
    /// <see cref="Exceptions.ClientBrandingConcurrentlyDeletedException"/> with stale entries detached.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Commits changes and the event atomically through the durable outbox, then flushes delivery.
    /// Throws the same typed save exceptions as <see cref="SaveChangesAsync"/>; rejected saves
    /// publish nothing, so retries may reuse the event. Use from controllers/services only.
    /// Wolverine handler chains already supply their own transaction, outbox and save.
    /// </summary>
    Task SaveChangesAndPublishAsync(IIntegrationEvent @event, CancellationToken ct = default);
}
