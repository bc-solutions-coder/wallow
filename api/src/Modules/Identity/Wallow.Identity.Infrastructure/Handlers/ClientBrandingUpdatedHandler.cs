using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Shared.Contracts.Branding;
using Wallow.Shared.Contracts.Branding.Events;

namespace Wallow.Identity.Infrastructure.Handlers;

/// <summary>
/// Keeps the OpenIddict application's display name equal to the client's branded display name, so
/// every surface OpenIddict renders (consent, tokens' <c>client_name</c>) shows the one
/// end-user-facing name Branding owns. The event is a trigger, not a payload: the handler pulls
/// the row's CURRENT name through <see cref="IClientBrandingProvider"/> (an uncached read), so
/// redelivered or reordered events converge on the latest write instead of freezing an older
/// value. Injecting the provider is chain-legal despite it reaching Branding's DbContext because
/// the OpenIddict manager is service-located (Program.cs pins it), leaving this chain exactly one
/// codegen-visible DbContext. Branding publishes the event; it never writes OpenIddict.
/// Concurrent deliveries can interleave read and update, but OpenIddict's concurrency token fails
/// the loser and the retry policy re-runs it, which re-reads and converges — the name could only
/// stay stale if the last event both collided and exhausted its retry, and nothing races the last
/// event.
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
            // The branding row is already gone — the client is being deleted; the deletion
            // cascade owns the OpenIddict cleanup, so a redelivered event must not write.
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
