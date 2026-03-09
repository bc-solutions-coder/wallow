using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence;

public static class MeteringDbSeeder
{
    public static async Task SeedAsync(BillingDbContext context)
    {
        await SeedMeterDefinitionsAsync(context);
        await SeedDefaultQuotasAsync(context);
        await context.SaveChangesAsync();
    }

    private static async Task SeedMeterDefinitionsAsync(BillingDbContext context)
    {
        List<string> existingCodes = await context.MeterDefinitions
            .Select(m => m.Code)
            .ToListAsync();

        MeterDefinition[] meters =
        [
            MeterDefinition.Create(
                "api.calls",
                "API Calls",
                "requests",
                MeterAggregation.Sum,
                true,
                "meter:{tenantId}:api.calls:{period}"),

            MeterDefinition.Create(
                "storage.bytes",
                "Storage Used",
                "bytes",
                MeterAggregation.Max,
                true),

            MeterDefinition.Create(
                "users.active",
                "Active Users",
                "users",
                MeterAggregation.Max,
                true),

            MeterDefinition.Create(
                "workflows.executions",
                "Workflow Executions",
                "executions",
                MeterAggregation.Sum,
                true)
        ];

        foreach (MeterDefinition meter in meters)
        {
            if (!existingCodes.Contains(meter.Code))
            {
                context.MeterDefinitions.Add(meter);
            }
        }
    }

    private static async Task SeedDefaultQuotasAsync(BillingDbContext context)
    {
        // Default quotas are seeded without tenant ID (plan-level defaults)
        TenantId systemTenantId = TenantId.Create(Guid.Empty);
        var existingQuotas = await context.QuotaDefinitions
            .IgnoreQueryFilters()
            .Where(q => q.TenantId == systemTenantId)
            .Select(q => new { q.MeterCode, q.PlanCode })
            .ToListAsync();

        QuotaDefinition[] quotas =
        [
            // Free tier
            CreatePlanQuota("api.calls", "free", 1000, QuotaPeriod.Monthly, QuotaAction.Block),
            CreatePlanQuota("storage.bytes", "free", 100 * 1024 * 1024, QuotaPeriod.Monthly, QuotaAction.Block), // 100 MB
            CreatePlanQuota("users.active", "free", 5, QuotaPeriod.Monthly, QuotaAction.Block),

            // Pro tier
            CreatePlanQuota("api.calls", "pro", 100000, QuotaPeriod.Monthly, QuotaAction.Warn),
            CreatePlanQuota("storage.bytes", "pro", 10L * 1024 * 1024 * 1024, QuotaPeriod.Monthly, QuotaAction.Warn), // 10 GB
            CreatePlanQuota("users.active", "pro", 50, QuotaPeriod.Monthly, QuotaAction.Warn),

            // Enterprise tier (soft limits)
            CreatePlanQuota("api.calls", "enterprise", 1000000, QuotaPeriod.Monthly, QuotaAction.Warn),
            CreatePlanQuota("storage.bytes", "enterprise", 100L * 1024 * 1024 * 1024, QuotaPeriod.Monthly, QuotaAction.Warn), // 100 GB
            CreatePlanQuota("users.active", "enterprise", 500, QuotaPeriod.Monthly, QuotaAction.Warn),
        ];

        foreach (QuotaDefinition quota in quotas)
        {
            bool exists = existingQuotas.Any(q =>
                q.MeterCode == quota.MeterCode && q.PlanCode == quota.PlanCode);

            if (!exists)
            {
                context.QuotaDefinitions.Add(quota);
            }
        }
    }

    private static QuotaDefinition CreatePlanQuota(
        string meterCode,
        string planCode,
        decimal limit,
        QuotaPeriod period,
        QuotaAction onExceeded)
    {
        return QuotaDefinition.CreatePlanQuota(meterCode, planCode, limit, period, onExceeded);
    }
}
