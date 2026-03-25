using Wallow.Branding.Application.DTOs;

namespace Wallow.Branding.Application.Interfaces;

public interface IClientBrandingService
{
    Task<ClientBrandingDto?> GetBrandingAsync(string clientId, CancellationToken ct = default);
    void InvalidateCache(string clientId);
}
