using Microsoft.Extensions.Logging;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Infrastructure.Handlers;

/// <summary>
/// Every developer application has exactly one end-user-facing display name from the moment it
/// exists: Identity announces the registration and this module creates the branding row, defaulting
/// the display name to the registered name when no branding was chosen. Service accounts face no
/// end user and get no row. Idempotent — a redelivered event finds the row and leaves it alone.
/// </summary>
public sealed partial class ClientRegisteredHandler(
    IClientBrandingRepository brandings,
    IClientBrandingService brandingService,
    TimeProvider timeProvider,
    ILogger<ClientRegisteredHandler> logger)
{
    public async Task HandleAsync(ClientRegisteredEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Kind != OrganizationClientKind.Application)
        {
            return;
        }

        if (await brandings.GetByClientIdAsync(message.ClientId, ct) is not null)
        {
            return;
        }

        // The envelope restores the PUBLISHER'S tenant, which is the caller's organization — not
        // necessarily the one the client was registered into (a global admin registers across
        // organizations). The row belongs to the owning organization, so say so explicitly.
        brandings.UseTenant(TenantId.Create(message.OrganizationId));

        ClientBranding branding = ClientBranding.Create(
            message.ClientId,
            message.BrandingDisplayName ?? message.ClientName,
            message.BrandingTagline,
            timeProvider: timeProvider);
        brandings.Add(branding);
        await brandings.SaveChangesAsync(ct);

        // The anonymous read caches misses too; without this a sign-in hitting the screen before
        // registration finished keeps answering 404 for five minutes.
        brandingService.InvalidateCache(message.ClientId);

        LogBrandingCreated(message.ClientId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the branding row for registered client {ClientId}")]
    private partial void LogBrandingCreated(string clientId);
}
