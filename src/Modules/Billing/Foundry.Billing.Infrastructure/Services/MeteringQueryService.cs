using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Contracts.Metering;

namespace Foundry.Billing.Infrastructure.Services;

public sealed class MeteringQueryService(
    IUsageRecordRepository usageRecordRepository,
    IQuotaDefinitionRepository quotaDefinitionRepository) : IMeteringQueryService
{

    public async Task<QuotaStatus?> CheckQuotaAsync(Guid tenantId, string meterCode, CancellationToken ct = default)
    {
        QuotaDefinition? quota = await quotaDefinitionRepository.GetEffectiveQuotaAsync(
            meterCode,
            planCode: null,
            ct);

        if (quota is null)
        {
            return null;
        }

        (DateTime periodStart, DateTime periodEnd) = GetCurrentPeriodBounds(quota.Period);

        IReadOnlyList<UsageRecord> usageRecords = await usageRecordRepository.GetHistoryAsync(
            meterCode,
            periodStart,
            periodEnd,
            ct);

        decimal totalUsage = usageRecords.Sum(r => r.Value);
        long usedLong = Convert.ToInt64(totalUsage);
        long limitLong = Convert.ToInt64(quota.Limit);
        decimal percentUsed = limitLong > 0 ? (totalUsage / quota.Limit) * 100m : 0m;
        bool isExceeded = totalUsage > quota.Limit;

        return new QuotaStatus(
            meterCode,
            usedLong,
            limitLong,
            Math.Round(percentUsed, 2),
            isExceeded);
    }

    private static (DateTime Start, DateTime End) GetCurrentPeriodBounds(QuotaPeriod period)
    {
        DateTime now = DateTime.UtcNow;

        return period switch
        {
            QuotaPeriod.Hourly => (
                new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc),
                new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1)),
            QuotaPeriod.Daily => (
                new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1)),
            QuotaPeriod.Monthly => (
                new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown quota period")
        };
    }
}
