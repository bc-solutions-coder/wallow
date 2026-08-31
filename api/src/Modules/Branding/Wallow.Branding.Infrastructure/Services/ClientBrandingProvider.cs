using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Shared.Contracts.Branding;

namespace Wallow.Branding.Infrastructure.Services;

/// <summary>
/// The cross-module face of <see cref="IClientBrandingService"/>: same cached read, narrowed to
/// the shape <see cref="PublicClientBranding"/> promises consumers outside the module.
/// </summary>
public sealed class ClientBrandingProvider(IClientBrandingService brandingService) : IClientBrandingProvider
{
    public async Task<PublicClientBranding?> FindAsync(string clientId, CancellationToken ct = default)
    {
        ClientBrandingDto? branding = await brandingService.GetBrandingAsync(clientId, ct);
        return branding is null
            ? null
            : new PublicClientBranding(
                branding.ClientId,
                branding.DisplayName,
                branding.Tagline,
                branding.LogoUrl,
                branding.ThemeJson);
    }
}
