using Microsoft.EntityFrameworkCore;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;

namespace Wallow.Billing.Infrastructure.Persistence.Repositories;

public sealed class MeterDefinitionRepository(BillingDbContext context) : IMeterDefinitionRepository
{
    public Task<MeterDefinition?> GetByIdAsync(MeterDefinitionId id, CancellationToken cancellationToken = default)
    {
        return context.MeterDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<MeterDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return context.MeterDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<MeterDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.MeterDefinitions
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);
    }

    public void Add(MeterDefinition meterDefinition)
    {
        context.MeterDefinitions.Add(meterDefinition);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
