using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Application.Metering.Services;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Billing.Domain.Metering.Events;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wolverine;

namespace Foundry.Billing.Infrastructure.Services;

/// <summary>
/// Valkey/Redis-based metering service for real-time usage tracking.
/// Uses Redis counters for sub-millisecond quota checks.
/// </summary>
public sealed partial class ValkeyMeteringService : IMeteringService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ITenantContext _tenantContext;
    private readonly IQuotaDefinitionRepository _quotaRepository;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMeterDefinitionRepository _meterRepository;
    private readonly IMessageBus _messageBus;
    private readonly ISubscriptionQueryService _subscriptionQueryService;
    private readonly ILogger<ValkeyMeteringService> _logger;

    private static readonly TimeSpan _keyExpiration = TimeSpan.FromDays(90);
    private const string MeterIndexKey = "meter:__index";
    private static readonly int[] _thresholds = [80, 90, 100];

    public ValkeyMeteringService(
        IConnectionMultiplexer redis,
        ITenantContext tenantContext,
        IQuotaDefinitionRepository quotaRepository,
        IUsageRecordRepository usageRepository,
        IMeterDefinitionRepository meterRepository,
        IMessageBus messageBus,
        ISubscriptionQueryService subscriptionQueryService,
        ILogger<ValkeyMeteringService> logger)
    {
        _redis = redis;
        _tenantContext = tenantContext;
        _quotaRepository = quotaRepository;
        _usageRepository = usageRepository;
        _meterRepository = meterRepository;
        _messageBus = messageBus;
        _subscriptionQueryService = subscriptionQueryService;
        _logger = logger;
    }

    public Task IncrementAsync(string meterCode, decimal value = 1, Dictionary<string, string>? dimensions = null)
    {
        TenantId tenantId = _tenantContext.TenantId;
        string period = GetCurrentPeriodKey(QuotaPeriod.Monthly);
        string key = $"meter:{tenantId.Value}:{meterCode}:{period}";

        IDatabase db = _redis.GetDatabase();

        // Fire-and-forget: does not block the request pipeline
        Task incrementTask = db.StringIncrementAsync(key, (double)value, CommandFlags.FireAndForget);
        _ = incrementTask.ContinueWith(
            t => LogMeteringIncrementFailed(_logger, t.Exception!, key),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        Task expireTask = db.KeyExpireAsync(key, _keyExpiration, ExpireWhen.HasNoExpiry, CommandFlags.FireAndForget);
        _ = expireTask.ContinueWith(
            t => LogMeteringExpireFailed(_logger, t.Exception!, key),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        // Track this meter key in the index Set for efficient flush enumeration
        Task indexTask = db.SetAddAsync(MeterIndexKey, key, CommandFlags.FireAndForget);
        _ = indexTask.ContinueWith(
            t => LogMeteringIndexAddFailed(_logger, t.Exception!, key),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        return Task.CompletedTask;
    }

    public async Task<QuotaCheckResult> CheckQuotaAsync(string meterCode)
    {
        TenantId tenantId = _tenantContext.TenantId;

        // Get tenant's active plan code from Billing module
        string? planCode = await _subscriptionQueryService.GetActivePlanCodeAsync(tenantId.Value, CancellationToken.None);

        // Get effective quota (tenant override > plan default)
        QuotaDefinition? quota = await _quotaRepository.GetEffectiveQuotaAsync(
            meterCode,
            planCode,
            CancellationToken.None);

        if (quota is null)
        {
            return QuotaCheckResult.Unlimited;
        }

        string period = GetCurrentPeriodKey(quota.Period);
        string key = $"meter:{tenantId.Value}:{meterCode}:{period}";
        string thresholdKey = $"threshold:{tenantId.Value}:{meterCode}:{period}";

        IDatabase db = _redis.GetDatabase();
        long currentValue = (long?)await db.StringGetAsync(key) ?? 0;

        decimal percentUsed = quota.Limit > 0 ? (currentValue / quota.Limit) * 100 : 0;
        int percentInt = (int)percentUsed;

        // Check for threshold crossings and raise events
        await CheckAndRaiseThresholdEventsAsync(
            db, thresholdKey, tenantId.Value, meterCode, currentValue, quota.Limit, percentInt);

        return new QuotaCheckResult(
            IsAllowed: currentValue < quota.Limit,
            CurrentUsage: currentValue,
            Limit: quota.Limit,
            PercentUsed: percentUsed,
            ActionIfExceeded: currentValue >= quota.Limit ? quota.OnExceeded : null);
    }

    private async Task CheckAndRaiseThresholdEventsAsync(
        IDatabase db,
        string thresholdKey,
        Guid tenantId,
        string meterCode,
        decimal currentUsage,
        decimal limit,
        int currentPercent)
    {
        // Get the last triggered threshold from Redis
        int lastTriggered = (int?)await db.StringGetAsync(thresholdKey) ?? 0;

        foreach (int threshold in _thresholds)
        {
            if (currentPercent >= threshold && lastTriggered < threshold)
            {
                // Get meter display name
                MeterDefinition? meter = await _meterRepository.GetByCodeAsync(meterCode, CancellationToken.None);
                string displayName = meter?.DisplayName ?? meterCode;

                // Raise domain event
                await _messageBus.PublishAsync(new QuotaThresholdReachedEvent(
                    tenantId,
                    meterCode,
                    displayName,
                    currentUsage,
                    limit,
                    threshold));

                // Update last triggered threshold
                await db.StringSetAsync(thresholdKey, threshold, TimeSpan.FromDays(31));
            }
        }
    }

    public async Task<decimal> GetCurrentUsageAsync(string meterCode, QuotaPeriod period)
    {
        TenantId tenantId = _tenantContext.TenantId;
        string periodKey = GetCurrentPeriodKey(period);
        string key = $"meter:{tenantId.Value}:{meterCode}:{periodKey}";

        IDatabase db = _redis.GetDatabase();
        long value = (long?)await db.StringGetAsync(key) ?? 0;

        return value;
    }

    public async Task<IReadOnlyList<UsageRecordDto>> GetUsageHistoryAsync(string meterCode, DateTime from, DateTime to)
    {
        IReadOnlyList<UsageRecord> records = await _usageRepository.GetHistoryAsync(
            meterCode,
            from,
            to,
            CancellationToken.None);

        return records.Select(r => new UsageRecordDto(
            Id: r.Id.Value,
            TenantId: r.TenantId.Value,
            MeterCode: r.MeterCode,
            PeriodStart: r.PeriodStart,
            PeriodEnd: r.PeriodEnd,
            Value: r.Value,
            FlushedAt: r.FlushedAt)).ToList();
    }

    private static string GetCurrentPeriodKey(QuotaPeriod period)
    {
        DateTime now = DateTime.UtcNow;
        return period switch
        {
            QuotaPeriod.Hourly => now.ToString("yyyy-MM-dd-HH"),
            QuotaPeriod.Daily => now.ToString("yyyy-MM-dd"),
            QuotaPeriod.Monthly or _ => now.ToString("yyyy-MM")
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fire-and-forget metering increment failed for key {Key}")]
    private static partial void LogMeteringIncrementFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fire-and-forget metering key expiry failed for key {Key}")]
    private static partial void LogMeteringExpireFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fire-and-forget metering index add failed for key {Key}")]
    private static partial void LogMeteringIndexAddFailed(ILogger logger, Exception ex, string key);
}
