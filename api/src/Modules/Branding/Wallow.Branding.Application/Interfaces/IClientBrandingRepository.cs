using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Application.Interfaces;

public interface IClientBrandingRepository
{
    Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// The row's display name straight off the store, or <see langword="null"/> when no row
    /// exists. NoTracking on purpose: a tracked read would hand back an instance already sitting
    /// in this scope's change tracker, masking a newer committed write — synchronization reads
    /// must never see that. Ignores tenant filters for the same reasons as
    /// <see cref="GetByClientIdAsync"/>.
    /// </summary>
    Task<string?> FindDisplayNameAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// The tenant that owns rows added after this call. The unit of work snapshots the ambient
    /// tenant when it is created, so a caller writing on behalf of a different organization (a
    /// global admin, a cross-org manager, an event handler running under the publisher's tenant)
    /// must state the owning organization here — mutating the scoped tenant context after the
    /// fact does not reach an already-created unit of work.
    /// </summary>
    void UseTenant(TenantId tenantId);

    /// <summary>
    /// Every branding row the addressed tenant owns, tracked so they can be removed — call
    /// <see cref="UseTenant"/> first when acting for another organization.
    /// </summary>
    Task<IReadOnlyList<ClientBranding>> ListAsync(CancellationToken ct = default);

    void Add(ClientBranding branding);
    void Remove(ClientBranding branding);

    /// <summary>
    /// Persists pending changes. When an added row loses a race on the repo-wide client_id unique
    /// index, throws <see cref="Exceptions.DuplicateClientBrandingException"/> with the losing
    /// insert detached — re-fetch the winning row and apply the write as an update. When a tracked
    /// row was deleted underneath the save, throws
    /// <see cref="Exceptions.ClientBrandingConcurrentlyDeletedException"/> with the stale entries
    /// detached — answer as if the row never existed.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists pending changes and the given integration event in one atomic unit: the event's
    /// envelope commits or rolls back with the rows (Wolverine's durable outbox) and is flushed
    /// to its subscribers only after the commit, so a crash between the save and the publish can
    /// no longer drop the event. Throws the same typed exceptions as
    /// <see cref="SaveChangesAsync"/>, with the same detach contract; a failed save publishes
    /// nothing, so the caller's retry may pass the same event again. For controller and service
    /// callers only — inside a Wolverine handler chain the transaction middleware already
    /// supplies the outbox and the save, and this would open a second unit of work on top of it.
    /// </summary>
    Task SaveChangesAndPublishAsync(IIntegrationEvent @event, CancellationToken ct = default);
}
