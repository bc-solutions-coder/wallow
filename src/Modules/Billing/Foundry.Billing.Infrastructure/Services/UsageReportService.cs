using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Shared.Contracts.Metering;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Services;

public sealed class UsageReportService : IUsageReportService
{
    private readonly BillingDbContext _dbContext;

    public UsageReportService(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UsageReportRow>> GetUsageAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        TenantId tenantIdTyped = new TenantId(tenantId);

        List<UsageReportRow> results = await _dbContext.UsageRecords
            .Where(ur => ur.TenantId == tenantIdTyped
                && ur.PeriodStart >= from
                && ur.PeriodStart < to)
            .Join(
                _dbContext.MeterDefinitions,
                ur => ur.MeterCode,
                md => md.Code,
                (ur, md) => new
                {
                    ur.PeriodStart.Date,
                    ur.MeterCode,
                    md.DisplayName,
                    md.Unit,
                    ur.Value
                })
            .GroupBy(x => new { x.Date, x.MeterCode, x.DisplayName, x.Unit })
            .Select(g => new UsageReportRow(
                g.Key.Date,
                g.Key.DisplayName,
                (long)g.Sum(x => x.Value),
                g.Key.Unit,
                0m, // BillableAmount - no pricing integration yet
                "USD")) // Currency - hardcoded for now
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Metric)
            .ToListAsync(ct);

        return results;
    }
}
