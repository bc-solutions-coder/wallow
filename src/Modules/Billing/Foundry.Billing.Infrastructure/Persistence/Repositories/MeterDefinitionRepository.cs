using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Billing.Domain.Metering.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

public sealed class MeterDefinitionRepository : IMeterDefinitionRepository
{
    private readonly BillingDbContext _context;

    public MeterDefinitionRepository(BillingDbContext context)
    {
        _context = context;
    }

    public Task<MeterDefinition?> GetByIdAsync(MeterDefinitionId id, CancellationToken cancellationToken = default)
    {
        return _context.MeterDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<MeterDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _context.MeterDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<MeterDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MeterDefinitions
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);
    }

    public void Add(MeterDefinition meterDefinition)
    {
        _context.MeterDefinitions.Add(meterDefinition);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
