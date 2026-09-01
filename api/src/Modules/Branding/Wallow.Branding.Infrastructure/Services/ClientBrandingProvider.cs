using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Shared.Contracts.Branding;

namespace Wallow.Branding.Infrastructure.Services;

/// <summary>
/// The cross-module face of Branding's reads. <see cref="FindAsync"/> is the same cached read as
/// <see cref="IClientBrandingService"/>, narrowed to the shape <see cref="PublicClientBranding"/>
/// promises consumers outside the module; <see cref="FindCurrentDisplayNameAsync"/> deliberately
/// bypasses that cache so synchronization consumers always see the latest committed write.
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
