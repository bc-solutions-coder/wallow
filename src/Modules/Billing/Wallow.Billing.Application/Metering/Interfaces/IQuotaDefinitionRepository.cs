using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Identity;

namespace Wallow.Billing.Application.Metering.Interfaces;

/// <summary>
/// Repository for quota definitions.
/// </summary>
public interface IQuotaDefinitionRepository
{
    Task<QuotaDefinition?> GetByIdAsync(QuotaDefinitionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effective quota for a tenant and meter.
    /// Checks tenant overrides first, then plan defaults.
    /// Tenant is resolved from ITenantContext.
    /// </summary>
    Task<QuotaDefinition?> GetEffectiveQuotaAsync(
        string meterCode,
        string? planCode,
        CancellationToken cancellationToken = default);

    Task<QuotaDefinition?> GetTenantOverrideAsync(
        string meterCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuotaDefinition>> GetAllForTenantAsync(
        CancellationToken cancellationToken = default);

    void Add(QuotaDefinition quotaDefinition);
    void Remove(QuotaDefinition quotaDefinition);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
