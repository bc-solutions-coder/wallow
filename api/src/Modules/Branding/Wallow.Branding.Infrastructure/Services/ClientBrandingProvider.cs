using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Shared.Contracts.Branding;

namespace Wallow.Branding.Infrastructure.Services;

/// <summary>
/// Exposes cached public branding to other modules. Display-name synchronization bypasses
/// that cache to read the current stored value.
/// </summary>
public sealed class ClientBrandingProvider(
    IClientBrandingService brandingService,
    IClientBrandingRepository repository) : IClientBrandingProvider
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

    public Task<string?> FindCurrentDisplayNameAsync(string clientId, CancellationToken ct = default) =>
        repository.FindDisplayNameAsync(clientId, ct);
}
