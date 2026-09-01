using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Application.Interfaces;

public interface IClientBrandingRepository
{
    Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default);

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
}
