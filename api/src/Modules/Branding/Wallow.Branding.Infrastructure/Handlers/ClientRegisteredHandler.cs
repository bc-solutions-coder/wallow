using Microsoft.Extensions.Logging;
using Wallow.Branding.Application.Exceptions;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Infrastructure.Handlers;

/// <summary>
/// Creates branding for registered applications, defaulting to the registered name.
/// Service accounts receive no row; existing branding is preserved on redelivery.
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

        // The owning organization may differ from the publisher's ambient tenant.
        brandings.UseTenant(TenantId.Create(message.OrganizationId));

        ClientBranding branding = ClientBranding.Create(
            message.ClientId,
            message.BrandingDisplayName ?? message.ClientName,
            message.BrandingTagline,
            timeProvider: timeProvider);
        brandings.Add(branding);
        try
        {
            await brandings.SaveChangesAsync(ct);
        }
        catch (DuplicateClientBrandingException)
        {
            // A concurrent writer created the row; preserve its values. Our insert is detached.
            return;
        }

        // Clear cached misses so branding becomes visible after registration.
        brandingService.InvalidateCache(message.ClientId);

        LogBrandingCreated(message.ClientId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the branding row for registered client {ClientId}")]
    private partial void LogBrandingCreated(string clientId);
}
