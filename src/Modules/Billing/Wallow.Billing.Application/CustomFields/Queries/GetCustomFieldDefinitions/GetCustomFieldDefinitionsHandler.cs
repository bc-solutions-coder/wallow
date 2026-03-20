using Wallow.Billing.Application.CustomFields.DTOs;
using Wallow.Billing.Application.CustomFields.Interfaces;
using Wallow.Billing.Domain.CustomFields.Entities;

namespace Wallow.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitions;

public sealed class GetCustomFieldDefinitionsHandler(ICustomFieldDefinitionRepository repository)
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(
        GetCustomFieldDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomFieldDefinition> definitions = await repository.GetByEntityTypeAsync(
            query.EntityType,
            query.IncludeInactive,
            cancellationToken);

        return definitions.ToDtoList();
    }
}
