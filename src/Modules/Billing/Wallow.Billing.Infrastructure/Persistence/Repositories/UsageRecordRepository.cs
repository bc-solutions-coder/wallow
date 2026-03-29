using Microsoft.EntityFrameworkCore;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Billing.Infrastructure.Persistence.Repositories;

public sealed class UsageRecordRepository(BillingDbContext context, ITenantContext tenantContext) : IUsageRecordRepository
{

    public Task<UsageRecord?> GetByIdAsync(UsageRecordId id, CancellationToken cancellationToken = default)
    {
        return context.UsageRecords
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<UsageRecord>> GetHistoryAsync(
        string meterCode,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        TenantId tenantId = tenantContext.TenantId;
        return await context.UsageRecords
            .Where(u =>
                u.TenantId == tenantId &&
                u.MeterCode == meterCode &&
                u.PeriodStart >= from &&
                u.PeriodEnd <= to)
            .OrderBy(u => u.PeriodStart)
            .ToListAsync(cancellationToken);
    }

    public Task<UsageRecord?> GetForPeriodAsync(
        string meterCode,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        TenantId tenantId = tenantContext.TenantId;
        return context.UsageRecords
            .AsTracking()
            .FirstOrDefaultAsync(u =>
                u.TenantId == tenantId &&
                u.MeterCode == meterCode &&
                u.PeriodStart == periodStart &&
                u.PeriodEnd == periodEnd,
                cancellationToken);
    }

    public void Add(UsageRecord usageRecord)
    {
        context.UsageRecords.Add(usageRecord);
    }

    public void Update(UsageRecord usageRecord)
    {
        context.UsageRecords.Update(usageRecord);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
