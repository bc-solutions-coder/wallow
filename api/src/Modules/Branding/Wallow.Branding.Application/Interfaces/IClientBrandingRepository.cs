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

    void Add(ClientBranding branding);
    void Remove(ClientBranding branding);
    Task SaveChangesAsync(CancellationToken ct = default);
}
