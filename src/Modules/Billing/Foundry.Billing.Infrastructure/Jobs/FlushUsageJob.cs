using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Events;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wolverine;

namespace Foundry.Billing.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that flushes Valkey counters to PostgreSQL for billing accuracy.
/// Uses atomic get-and-reset to prevent data loss.
/// </summary>
public sealed partial class FlushUsageJob
{
    private const string MeterIndexKey = "meter:__index";
    private readonly IConnectionMultiplexer _redis;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContextFactory _tenantContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FlushUsageJob> _logger;

    public FlushUsageJob(
        IConnectionMultiplexer redis,
        IUsageRecordRepository usageRepository,
        IMessageBus messageBus,
        ITenantContextFactory tenantContextFactory,
        TimeProvider timeProvider,
        ILogger<FlushUsageJob> logger)
    {
        _redis = redis;
        _usageRepository = usageRepository;
        _messageBus = messageBus;
        _tenantContextFactory = tenantContextFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        LogStartingFlushJob(_logger);
        int flushedCount = 0;

        try
        {
            IDatabase db = _redis.GetDatabase();
            RedisValue[] members = await db.SetMembersAsync(MeterIndexKey);
            List<string> keys = members.Select(m => m.ToString()).ToList();

            LogFoundMeterKeys(_logger, keys.Count);

            foreach (string key in keys)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                bool processed = await ProcessKeyAsync(key);
                if (processed)
                {
                    flushedCount++;
                }
            }

            await _usageRepository.SaveChangesAsync(cancellationToken);

            if (flushedCount > 0)
            {
                await _messageBus.PublishAsync(new UsageFlushedEvent(_timeProvider.GetUtcNow().UtcDateTime, flushedCount));
            }

            LogFlushCompleted(_logger, flushedCount);
        }
        catch (Exception ex)
        {
            LogFlushJobError(_logger, ex);
            throw;
        }
    }

    private async Task<bool> ProcessKeyAsync(string key)
    {
        try
        {
            // Parse key: meter:{tenantId}:{meterCode}:{period}
            string[] parts = key.Split(':');
            if (parts.Length != 4 || parts[0] != "meter")
            {
                LogInvalidKeyFormat(_logger, key);
                return false;
            }

            if (!Guid.TryParse(parts[1], out Guid tenantGuid))
            {
                LogInvalidTenantIdInKey(_logger, key);
                return false;
            }

            TenantId tenantId = TenantId.Create(tenantGuid);
            string meterCode = parts[2];
            string period = parts[3];

            // Atomic get-and-reset
            IDatabase db = _redis.GetDatabase();
            long value = (long?)await db.StringGetSetAsync(key, 0) ?? 0;

            if (value <= 0)
            {
                return false;
            }

            (DateTime periodStart, DateTime periodEnd) = ParsePeriod(period);

            // Set tenant context so repository queries filter correctly
            using (_tenantContextFactory.CreateScope(tenantId))
            {
                // Upsert: find existing record or create new
                UsageRecord? existing = await _usageRepository.GetForPeriodAsync(
                    meterCode,
                    periodStart,
                    periodEnd,
                    CancellationToken.None);

                if (existing is not null)
                {
                    existing.AddValue(value, _timeProvider);
                    _usageRepository.Update(existing);
                }
                else
                {
                    UsageRecord record = UsageRecord.Create(
                        tenantId,
                        meterCode,
                        periodStart,
                        periodEnd,
                        value,
                        _timeProvider);

                    _usageRepository.Add(record);
                }
            }

            // Remove from index Set after successful flush
            await db.SetRemoveAsync(MeterIndexKey, key);

            LogFlushedMeterValue(_logger, value, meterCode, tenantId.Value);
            return true;
        }
        catch (Exception ex)
        {
            LogProcessKeyError(_logger, ex, key);
            return false;
        }
    }

    private static (DateTime Start, DateTime End) ParsePeriod(string period)
    {
        // Period formats: yyyy-MM (monthly), yyyy-MM-dd (daily), yyyy-MM-dd-HH (hourly)
        string[] parts = period.Split('-');

        return parts.Length switch
        {
            2 => // Monthly: yyyy-MM
            (
                new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)),
            3 => // Daily: yyyy-MM-dd
            (
                new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, DateTimeKind.Utc),
                new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, DateTimeKind.Utc).AddDays(1)),
            4 => // Hourly: yyyy-MM-dd-HH
            (
                new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]), 0, 0, DateTimeKind.Utc),
                new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]), 0, 0, DateTimeKind.Utc).AddHours(1)),
            _ => throw new ArgumentException($"Invalid period format: {period}", nameof(period))
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting usage flush job")]
    private static partial void LogStartingFlushJob(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {KeyCount} meter keys to process")]
    private static partial void LogFoundMeterKeys(ILogger logger, int keyCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usage flush completed. Flushed {Count} records")]
    private static partial void LogFlushCompleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during usage flush job")]
    private static partial void LogFlushJobError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid key format: {Key}")]
    private static partial void LogInvalidKeyFormat(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid tenant ID in key: {Key}")]
    private static partial void LogInvalidTenantIdInKey(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flushed {Value} for {MeterCode} tenant {TenantId}")]
    private static partial void LogFlushedMeterValue(ILogger logger, long value, string meterCode, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing key: {Key}")]
    private static partial void LogProcessKeyError(ILogger logger, Exception ex, string key);
}
