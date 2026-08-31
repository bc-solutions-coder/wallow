using Microsoft.Extensions.Logging;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Infrastructure.Handlers;

/// <summary>
/// Branding belongs to an organization's clients, so it goes when the organization goes.
/// Identity announces the deletion and this module drops every branding row the tenant owned,
/// the logo objects behind them and the cached copies — Identity never reaches into Branding's
/// persistence. Idempotent: a redelivered event finds an empty tenant and does nothing.
/// </summary>
public sealed partial class OrganizationDeletedHandler(
    IClientBrandingRepository brandings,
    IClientBrandingService brandingService,
    IStorageProvider storageProvider,
    ILogger<OrganizationDeletedHandler> logger)
{
    public async Task HandleAsync(OrganizationDeletedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The envelope restores the PUBLISHER'S tenant — a global admin deleting across
        // organizations publishes under their own — so the tenant whose rows die is stated
        // explicitly.
        brandings.UseTenant(TenantId.Create(message.OrganizationId));
        IReadOnlyList<ClientBranding> rows = await brandings.ListAsync(ct);

        if (rows.Count == 0)
        {
            return;
        }

        // Rows go first: once they are committed the brandings are gone for every reader,
        // and a storage failure after that merely orphans logo objects. The reverse order
        // could delete a logo and then fail before the save, leaving a live row whose image
        // is already gone.
        foreach (ClientBranding branding in rows)
        {
            brandings.Remove(branding);
        }

        await brandings.SaveChangesAsync(ct);

        foreach (ClientBranding branding in rows)
        {
            if (branding.LogoStorageKey is not null)
            {
                await storageProvider.DeleteAsync(branding.LogoStorageKey, ct);
            }
        }

        // The anonymous read caches hits for five minutes; drop them so a sign-in screen does
        // not keep painting a dead organization's branding.
        foreach (ClientBranding branding in rows)
        {
            brandingService.InvalidateCache(branding.ClientId);
        }

        LogTenantBrandingsDeleted(rows.Count, message.OrganizationId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} branding rows of deleted organization {OrganizationId}")]
    private partial void LogTenantBrandingsDeleted(int count, Guid organizationId);
}
