using Wallow.Branding.Domain.Entities;

namespace Wallow.Branding.Application.Interfaces;

public interface IClientBrandingRepository
{
    Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default);
    void Add(ClientBranding branding);
    void Remove(ClientBranding branding);
    Task SaveChangesAsync(CancellationToken ct = default);
}
