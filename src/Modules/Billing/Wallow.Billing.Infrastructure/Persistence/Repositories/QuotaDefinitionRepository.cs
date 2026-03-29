using Microsoft.EntityFrameworkCore;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Infrastructure.Persistence.Repositories;

public sealed class QuotaDefinitionRepository(BillingDbContext context) : IQuotaDefinitionRepository
{

    public Task<QuotaDefinition?> GetByIdAsync(QuotaDefinitionId id, CancellationToken cancellationToken = default)
    {
        return context.QuotaDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<QuotaDefinition?> GetEffectiveQuotaAsync(
        string meterCode,
        string? planCode,
        CancellationToken cancellationToken = default)
    {
        // First try tenant-specific override
        QuotaDefinition? quotaOverride = await GetTenantOverrideAsync(meterCode, cancellationToken);
        if (quotaOverride is not null)
        {
            return quotaOverride;
        }

        // Then try plan default (if plan code provided)
        if (!string.IsNullOrEmpty(planCode))
        {
            TenantId systemTenantId = TenantId.Create(Guid.Empty);
            return await context.QuotaDefinitions
                .AsTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(q =>
                    q.MeterCode == meterCode &&
                    q.PlanCode == planCode &&
                    q.TenantId == systemTenantId,
                    cancellationToken);
        }

        return null;
    }

    public Task<QuotaDefinition?> GetTenantOverrideAsync(
        string meterCode,
        CancellationToken cancellationToken = default)
    {
        return context.QuotaDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(q =>
                q.MeterCode == meterCode &&
                q.PlanCode == null,
                cancellationToken);
    }

    public async Task<IReadOnlyList<QuotaDefinition>> GetAllForTenantAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.QuotaDefinitions
            .OrderBy(q => q.MeterCode)
            .ToListAsync(cancellationToken);
    }

    public void Add(QuotaDefinition quotaDefinition)
    {
        context.QuotaDefinitions.Add(quotaDefinition);
    }

    public void Remove(QuotaDefinition quotaDefinition)
    {
        context.QuotaDefinitions.Remove(quotaDefinition);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
