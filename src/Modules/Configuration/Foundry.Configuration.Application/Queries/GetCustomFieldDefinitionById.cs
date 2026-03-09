using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;

namespace Foundry.Configuration.Application.Queries;

public sealed record GetCustomFieldDefinitionById(Guid Id);

public sealed class GetCustomFieldDefinitionByIdHandler(ICustomFieldDefinitionRepository repository)
{

    public async Task<CustomFieldDefinitionDto?> Handle(
        GetCustomFieldDefinitionById query,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinition? definition = await repository.GetByIdAsync(
            CustomFieldDefinitionId.Create(query.Id),
            cancellationToken);

        return definition?.ToDto();
    }
}
