using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;

namespace Wallow.Billing.Application.Metering.Interfaces;

/// <summary>
/// Repository for usage records.
/// </summary>
public interface IUsageRecordRepository
{
    Task<UsageRecord?> GetByIdAsync(UsageRecordId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsageRecord>> GetHistoryAsync(
        string meterCode,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<UsageRecord?> GetForPeriodAsync(
        string meterCode,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default);

    void Add(UsageRecord usageRecord);
    void Update(UsageRecord usageRecord);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
