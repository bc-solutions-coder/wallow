using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Shared.Contracts.Storage;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Storage.Infrastructure.Jobs;

/// <summary>
/// Deletes backend objects that no <c>StoredFile</c> row references — the residue of an
/// upload whose backend write succeeded but whose <c>SaveChangesAsync</c> failed. Only
/// objects under the <c>tenant-</c> key prefix are considered, which keeps foreign keyspaces
/// (Branding's <c>client-logos/</c>) untouchable, and only objects older than
/// <see cref="MinimumAge"/> are deleted so the sweep can never race an in-flight upload
/// (presigned upload URLs are clamped to minutes, not hours).
/// </summary>
public sealed partial class OrphanedObjectSweepJob(
    StorageDbContext dbContext,
    IStorageProvider storageProvider,
    TimeProvider timeProvider,
    ILogger<OrphanedObjectSweepJob> logger)
{
    internal const string TenantKeyPrefix = "tenant-";
    internal static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    private const int BatchSize = 500;

    public async Task<int> ExecuteAsync(CancellationToken ct)
    {
        LogSweepStarted(logger);

        try
        {
            DateTimeOffset cutoff = timeProvider.GetUtcNow() - MinimumAge;
            int deleted = 0;
            List<StorageObjectInfo> batch = new(BatchSize);

            await foreach (StorageObjectInfo storageObject in storageProvider.ListAsync(TenantKeyPrefix, ct))
            {
                if (storageObject.LastModified > cutoff)
                {
                    continue;
                }

                batch.Add(storageObject);

                if (batch.Count == BatchSize)
                {
                    deleted += await SweepBatchAsync(batch, ct);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                deleted += await SweepBatchAsync(batch, ct);
            }

            LogSweepCompleted(logger, deleted);

            return deleted;
        }
        catch (Exception ex)
        {
            LogSweepFailed(logger, ex);
            throw;
        }
    }

    private async Task<int> SweepBatchAsync(List<StorageObjectInfo> batch, CancellationToken ct)
    {
        List<string> keys = batch.ConvertAll(storageObject => storageObject.Key);

        // The sweep is platform-wide garbage collection: rows from EVERY tenant reference
        // objects under tenant-*/, so the tenant query filter must not hide any of them.
        List<string> referencedKeys = await dbContext.Files
            .IgnoreQueryFilters()
            .Where(file => keys.Contains(file.StorageKey))
            .Select(file => file.StorageKey)
            .ToListAsync(ct);
        HashSet<string> referenced = new(referencedKeys, StringComparer.Ordinal);

        int deleted = 0;

        foreach (StorageObjectInfo storageObject in batch)
        {
            if (referenced.Contains(storageObject.Key))
            {
                continue;
            }

            await storageProvider.DeleteAsync(storageObject.Key, ct);
            LogOrphanDeleted(logger, storageObject.Key, storageObject.LastModified);
            deleted++;
        }

        return deleted;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting orphaned object sweep")]
    private static partial void LogSweepStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Orphaned object sweep completed, deleted {Count} objects")]
    private static partial void LogSweepCompleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted orphaned object {StorageKey} last modified {LastModified}")]
    private static partial void LogOrphanDeleted(ILogger logger, string storageKey, DateTimeOffset lastModified);

    [LoggerMessage(Level = LogLevel.Error, Message = "Orphaned object sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);
}
