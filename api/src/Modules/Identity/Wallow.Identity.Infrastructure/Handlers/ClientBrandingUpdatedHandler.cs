using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Shared.Contracts.Branding;
using Wallow.Shared.Contracts.Branding.Events;

namespace Wallow.Identity.Infrastructure.Handlers;

/// <summary>
/// Updates the OpenIddict display name from an uncached <see cref="IClientBrandingProvider"/> read.
/// Re-delivery reads current branding rather than replaying the event payload.
/// </summary>
public sealed partial class ClientBrandingUpdatedHandler(
    IOpenIddictApplicationManager applicationManager,
    IClientBrandingProvider brandingProvider,
    ILogger<ClientBrandingUpdatedHandler> logger)
{
    public async Task HandleAsync(ClientBrandingUpdatedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        object? application = await applicationManager.FindByClientIdAsync(message.ClientId, ct);
        if (application is null)
        {
            LogClientMissing(message.ClientId);
            return;
        }

        string? currentDisplayName = await brandingProvider.FindCurrentDisplayNameAsync(message.ClientId, ct);
        if (currentDisplayName is null)
        {
            // Skip missing branding; deletion owns application cleanup.
            LogBrandingRowGone(message.ClientId);
            return;
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);
        if (string.Equals(descriptor.DisplayName, currentDisplayName, StringComparison.Ordinal))
        {
            return;
        }

        descriptor.DisplayName = currentDisplayName;
        await applicationManager.UpdateAsync(application, descriptor, ct);
        LogDisplayNameSynced(message.ClientId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Branding updated for unknown client {ClientId}; nothing to sync")]
    private partial void LogClientMissing(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Branding row for client {ClientId} no longer exists; skipping display-name sync")]
    private partial void LogBrandingRowGone(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Synced OpenIddict display name for client {ClientId}")]
    private partial void LogDisplayNameSynced(string clientId);
}
