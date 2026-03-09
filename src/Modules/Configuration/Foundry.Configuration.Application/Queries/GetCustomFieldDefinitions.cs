using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;

namespace Foundry.Configuration.Application.Queries;

public sealed record GetCustomFieldDefinitions(string EntityType, bool IncludeInactive = false);

public sealed class GetCustomFieldDefinitionsHandler(ICustomFieldDefinitionRepository repository)
{

    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(
        GetCustomFieldDefinitions query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomFieldDefinition> definitions = await repository.GetByEntityTypeAsync(
            query.EntityType,
            query.IncludeInactive,
            cancellationToken);

        return definitions.ToDtoList();
    }
}
