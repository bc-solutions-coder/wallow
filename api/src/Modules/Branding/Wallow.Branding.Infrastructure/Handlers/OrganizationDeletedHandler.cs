using Microsoft.Extensions.Logging;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Infrastructure.Handlers;

/// <summary>
/// Deletes an organization's branding rows, logos and cached copies after Identity reports deletion.
/// A repeated delivery with no remaining rows does nothing.
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

        // Select the deleted organization, not the publisher's ambient tenant.
        brandings.UseTenant(TenantId.Create(message.OrganizationId));
        IReadOnlyList<ClientBranding> rows = await brandings.ListAsync(ct);

        if (rows.Count == 0)
        {
            return;
        }

        // Commit row deletion before removing logos. A later storage failure may orphan
        // objects; deleting logos first could leave live rows pointing to missing objects.
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

        // Clear cached branding after deletion.
        foreach (ClientBranding branding in rows)
        {
            brandingService.InvalidateCache(branding.ClientId);
        }

        LogTenantBrandingsDeleted(rows.Count, message.OrganizationId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} branding rows of deleted organization {OrganizationId}")]
    private partial void LogTenantBrandingsDeleted(int count, Guid organizationId);
}
