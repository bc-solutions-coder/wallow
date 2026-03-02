using Foundry.Billing.Domain.Metering.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Domain.Metering.Entities;

/// <summary>
/// Billing-grade usage record flushed from Valkey.
/// Represents aggregated usage for a tenant/meter/period combination.
/// </summary>
public sealed class UsageRecord : Entity<UsageRecordId>, ITenantScoped
{
    /// <summary>
    /// The tenant this usage belongs to.
    /// </summary>
    public TenantId TenantId { get; init; }

    /// <summary>
    /// The meter code this usage is for.
    /// </summary>
    public string MeterCode { get; private set; } = string.Empty;

    /// <summary>
    /// Start of the usage period (inclusive).
    /// </summary>
    public DateTime PeriodStart { get; private set; }

    /// <summary>
    /// End of the usage period (exclusive).
    /// </summary>
    public DateTime PeriodEnd { get; private set; }

    /// <summary>
    /// The usage value for this period.
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>
    /// When this record was synced from Valkey.
    /// </summary>
    public DateTime FlushedAt { get; private set; }

    private UsageRecord() { } // EF Core

    private UsageRecord(
        TenantId tenantId,
        string meterCode,
        DateTime periodStart,
        DateTime periodEnd,
        decimal value,
        TimeProvider timeProvider)
    {
        Id = UsageRecordId.New();
        TenantId = tenantId;
        MeterCode = meterCode;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Value = value;
        FlushedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public static UsageRecord Create(
        TenantId tenantId,
        string meterCode,
        DateTime periodStart,
        DateTime periodEnd,
        decimal value,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(meterCode))
        {
            throw new BusinessRuleException(
                "Metering.MeterCodeRequired",
                "Meter code cannot be empty");
        }

        if (periodEnd <= periodStart)
        {
            throw new BusinessRuleException(
                "Metering.InvalidPeriod",
                "Period end must be after period start");
        }

        if (value < 0)
        {
            throw new BusinessRuleException(
                "Metering.InvalidValue",
                "Usage value must be non-negative");
        }

        return new UsageRecord(tenantId, meterCode, periodStart, periodEnd, value, timeProvider);
    }

    /// <summary>
    /// Adds value to an existing usage record (for upsert operations).
    /// </summary>
    public void AddValue(decimal additionalValue, TimeProvider timeProvider)
    {
        if (additionalValue < 0)
        {
            throw new BusinessRuleException(
                "Metering.InvalidValue",
                "Additional value must be non-negative");
        }

        Value += additionalValue;
        FlushedAt = timeProvider.GetUtcNow().UtcDateTime;
    }
}
