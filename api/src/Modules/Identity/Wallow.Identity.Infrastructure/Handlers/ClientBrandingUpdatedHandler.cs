using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Shared.Contracts.Branding.Events;

namespace Wallow.Identity.Infrastructure.Handlers;

/// <summary>
/// Keeps the OpenIddict application's display name equal to the client's branded display name, so
/// every surface OpenIddict renders (consent, tokens' <c>client_name</c>) shows the one
/// end-user-facing name Branding owns. Branding publishes the event; it never writes OpenIddict.
/// </summary>
public sealed partial class ClientBrandingUpdatedHandler(
    IOpenIddictApplicationManager applicationManager,
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

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);
        if (string.Equals(descriptor.DisplayName, message.DisplayName, StringComparison.Ordinal))
        {
            return;
        }

        descriptor.DisplayName = message.DisplayName;
        await applicationManager.UpdateAsync(application, descriptor, ct);
        LogDisplayNameSynced(message.ClientId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Branding updated for unknown client {ClientId}; nothing to sync")]
    private partial void LogClientMissing(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Synced OpenIddict display name for client {ClientId}")]
    private partial void LogDisplayNameSynced(string clientId);
}
