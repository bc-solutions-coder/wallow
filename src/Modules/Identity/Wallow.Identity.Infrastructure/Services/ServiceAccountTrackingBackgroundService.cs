using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class ServiceAccountTrackingBackgroundService : BackgroundService
{
    private readonly ServiceAccountUsageBuffer _buffer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ServiceAccountTrackingBackgroundService> _logger;

    private static readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(60);

    public ServiceAccountTrackingBackgroundService(
        ServiceAccountUsageBuffer buffer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ServiceAccountTrackingBackgroundService> logger)
    {
        _buffer = buffer;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_flushInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
                if (entries.Count == 0)
                {
                    continue;
                }

                await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
                IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

                List<string> clientIds = entries.Keys.ToList();
                List<DateTimeOffset> timestamps = clientIds.Select(id => entries[id]).ToList();

                // Batch update using raw SQL with unnest for efficiency
                int updated = await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE identity.service_account_metadata AS sam
                    SET last_used_at = v.last_used_at
                    FROM unnest(@clientIds, @timestamps) AS v(client_id, last_used_at)
                    WHERE sam.client_id = v.client_id
                    """,
                    [
                        new Npgsql.NpgsqlParameter<string[]>("clientIds", clientIds.ToArray()),
                        new Npgsql.NpgsqlParameter<DateTimeOffset[]>("timestamps", timestamps.ToArray()),
                    ],
                    stoppingToken);

                LogFlushed(updated, entries.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogFlushFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flushed {UpdatedCount}/{TotalCount} service account usage timestamps")]
    private partial void LogFlushed(int updatedCount, int totalCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to flush service account usage timestamps")]
    private partial void LogFlushFailed(Exception ex);
}
