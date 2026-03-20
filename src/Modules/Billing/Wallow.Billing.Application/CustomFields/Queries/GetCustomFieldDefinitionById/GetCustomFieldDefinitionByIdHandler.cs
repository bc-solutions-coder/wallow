using Wallow.Billing.Application.CustomFields.DTOs;
using Wallow.Billing.Application.CustomFields.Interfaces;
using Wallow.Billing.Domain.CustomFields.Entities;
using Wallow.Billing.Domain.CustomFields.Identity;

namespace Wallow.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitionById;

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
