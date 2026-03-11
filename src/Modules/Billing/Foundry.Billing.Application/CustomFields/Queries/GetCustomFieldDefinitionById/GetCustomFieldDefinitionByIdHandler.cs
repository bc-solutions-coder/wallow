using Foundry.Billing.Application.CustomFields.DTOs;
using Foundry.Billing.Application.CustomFields.Interfaces;
using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Billing.Domain.CustomFields.Identity;

namespace Foundry.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitionById;

public sealed class GetCustomFieldDefinitionByIdHandler(ICustomFieldDefinitionRepository repository)
{
    public async Task<CustomFieldDefinitionDto?> Handle(
        GetCustomFieldDefinitionByIdQuery query,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinition? definition = await repository.GetByIdAsync(
            CustomFieldDefinitionId.Create(query.Id),
            cancellationToken);

        return definition?.ToDto();
    }
}
