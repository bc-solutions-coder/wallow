using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class SsoClientSessionService(
    IdentityDbContext dbContext,
    IOpenIddictApplicationManager applicationManager,
    TimeProvider timeProvider,
    ILogger<SsoClientSessionService> logger) : ISsoClientSessionService
{
    public async Task RecordAsync(string sid, string clientId, Guid userId, CancellationToken ct)
    {
        bool exists = await dbContext.SsoSessionClients
            .AnyAsync(s => s.Sid == sid && s.ClientId == clientId, ct);
        if (exists)
        {
            return;
        }

        dbContext.SsoSessionClients.Add(SsoSessionClient.Create(sid, clientId, userId, timeProvider));

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two concurrent authorize requests for the same (sid, client) raced past the
            // existence check; the unique index kept one row, which is the state we wanted.
            dbContext.ChangeTracker.Clear();
        }

        LogParticipationRecorded(clientId);
    }

    public async Task<IReadOnlyList<Uri>> BuildLogoutNotificationUrisAsync(
        string sid, Uri issuer, CancellationToken ct)
    {
        List<string> clientIds = await dbContext.SsoSessionClients
            .Where(s => s.Sid == sid)
            .Select(s => s.ClientId)
            .Distinct()
            .ToListAsync(ct);

        List<Uri> uris = [];
        foreach (string clientId in clientIds)
        {
            object? application = await applicationManager.FindByClientIdAsync(clientId, ct);
            if (application is null)
            {
                continue;
            }

            OpenIddictApplicationDescriptor descriptor = new();
            await applicationManager.PopulateAsync(descriptor, application, ct);

            Uri? frontchannelUri = descriptor.GetFrontchannelLogoutUri();
            if (frontchannelUri is null)
            {
                continue;
            }

            uris.Add(BuildNotificationUri(frontchannelUri, issuer, sid));
        }

        return uris;
    }

    public async Task ForgetAsync(string sid, CancellationToken ct)
    {
        // Tracked delete rather than ExecuteDeleteAsync: bulk statements bypass the unit of
        // work, and the in-memory provider used by unit tests does not support them.
        List<SsoSessionClient> rows = await dbContext.SsoSessionClients
            .AsTracking()
            .Where(s => s.Sid == sid)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return;
        }

        dbContext.SsoSessionClients.RemoveRange(rows);
        await dbContext.SaveChangesAsync(ct);
    }

    private static Uri BuildNotificationUri(Uri frontchannelUri, Uri issuer, string sid)
    {
        string separator = string.IsNullOrEmpty(frontchannelUri.Query) ? "?" : "&";
        string query = "iss=" + Uri.EscapeDataString(issuer.AbsoluteUri)
            + "&sid=" + Uri.EscapeDataString(sid);
        return new Uri(frontchannelUri.AbsoluteUri + separator + query);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recorded SSO session participation for client {ClientId}")]
    private partial void LogParticipationRecorded(string clientId);
}
