using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

public sealed class UsageRecordRepository : IUsageRecordRepository
{
    private readonly BillingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public UsageRecordRepository(BillingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public Task<UsageRecord?> GetByIdAsync(UsageRecordId id, CancellationToken cancellationToken = default)
    {
        return _context.UsageRecords
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<UsageRecord>> GetHistoryAsync(
        string meterCode,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        TenantId tenantId = _tenantContext.TenantId;
        return await _context.UsageRecords
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
        TenantId tenantId = _tenantContext.TenantId;
        return _context.UsageRecords
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
        _context.UsageRecords.Add(usageRecord);
    }

    public void Update(UsageRecord usageRecord)
    {
        _context.UsageRecords.Update(usageRecord);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
