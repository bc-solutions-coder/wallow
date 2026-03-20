using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;

namespace Wallow.Billing.Application.Metering.Interfaces;

/// <summary>
/// Repository for meter definitions.
/// </summary>
public interface IMeterDefinitionRepository
{
    Task<MeterDefinition?> GetByIdAsync(MeterDefinitionId id, CancellationToken cancellationToken = default);
    Task<MeterDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MeterDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    void Add(MeterDefinition meterDefinition);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
