using Microsoft.Extensions.Logging;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Storage;

namespace Wallow.Branding.Infrastructure.Handlers;

/// <summary>
/// Branding belongs to a client, so it goes when the client goes. Identity announces the deletion
/// and this module drops its own row, the logo object behind it and the cached copy — Identity
/// never reaches into Branding's persistence, and Branding never learns how clients are deleted.
/// </summary>
public sealed partial class ClientDeletedHandler(
    IClientBrandingRepository brandings,
    IClientBrandingService brandingService,
    IStorageProvider storageProvider,
    ILogger<ClientDeletedHandler> logger)
{
    public async Task HandleAsync(ClientDeletedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        ClientBranding? branding = await brandings.GetByClientIdAsync(message.ClientId, ct);
        if (branding is null)
        {
            return;
        }

        if (branding.LogoStorageKey is not null)
        {
            await storageProvider.DeleteAsync(branding.LogoStorageKey, ct);
        }

        brandings.Remove(branding);
        await brandings.SaveChangesAsync(ct);
        brandingService.InvalidateCache(message.ClientId);

        LogBrandingDeleted(message.ClientId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted the branding of deleted client {ClientId}")]
    private partial void LogBrandingDeleted(string clientId);
}
