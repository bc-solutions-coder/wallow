using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

public sealed class QuotaDefinitionRepository : IQuotaDefinitionRepository
{
    private readonly BillingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public QuotaDefinitionRepository(BillingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public Task<QuotaDefinition?> GetByIdAsync(QuotaDefinitionId id, CancellationToken cancellationToken = default)
    {
        return _context.QuotaDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<QuotaDefinition?> GetEffectiveQuotaAsync(
        string meterCode,
        string? planCode,
        CancellationToken cancellationToken = default)
    {
        // First try tenant-specific override
        QuotaDefinition? override_ = await GetTenantOverrideAsync(meterCode, cancellationToken);
        if (override_ is not null)
        {
            return override_;
        }

        // Then try plan default (if plan code provided)
        if (!string.IsNullOrEmpty(planCode))
        {
            TenantId systemTenantId = TenantId.Create(Guid.Empty);
            return await _context.QuotaDefinitions
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
        TenantId tenantId = _tenantContext.TenantId;
        return _context.QuotaDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(q =>
                q.TenantId == tenantId &&
                q.MeterCode == meterCode &&
                q.PlanCode == null,
                cancellationToken);
    }

    public async Task<IReadOnlyList<QuotaDefinition>> GetAllForTenantAsync(
        CancellationToken cancellationToken = default)
    {
        TenantId tenantId = _tenantContext.TenantId;
        return await _context.QuotaDefinitions
            .Where(q => q.TenantId == tenantId)
            .OrderBy(q => q.MeterCode)
            .ToListAsync(cancellationToken);
    }

    public void Add(QuotaDefinition quotaDefinition)
    {
        _context.QuotaDefinitions.Add(quotaDefinition);
    }

    public void Remove(QuotaDefinition quotaDefinition)
    {
        _context.QuotaDefinitions.Remove(quotaDefinition);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
