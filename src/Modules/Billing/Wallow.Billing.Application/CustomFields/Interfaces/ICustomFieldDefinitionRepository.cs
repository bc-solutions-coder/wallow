using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Billing.Domain.CustomFields.Identity;

namespace Wallow.Billing.Application.CustomFields.Interfaces;

public interface ICustomFieldDefinitionRepository
{
    Task<CustomFieldDefinition?> GetByIdAsync(CustomFieldDefinitionId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomFieldDefinition>> GetByEntityTypeAsync(string entityType, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<bool> FieldKeyExistsAsync(string entityType, string fieldKey, CancellationToken cancellationToken = default);

    Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default);

    Task UpdateAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
