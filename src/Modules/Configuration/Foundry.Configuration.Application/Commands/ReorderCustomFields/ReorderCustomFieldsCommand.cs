using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;

namespace Foundry.Configuration.Application.Commands.ReorderCustomFields;

public sealed record ReorderCustomFieldsCommand(string EntityType, IReadOnlyList<Guid> FieldIdsInOrder);

public sealed class ReorderCustomFieldsHandler(
    ICustomFieldDefinitionRepository repository,
    TimeProvider timeProvider)
{

    public async Task Handle(
        ReorderCustomFieldsCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomFieldDefinition> definitions = await repository.GetByEntityTypeAsync(
            command.EntityType,
            includeInactive: true,
            cancellationToken);

        Dictionary<Guid, CustomFieldDefinition> definitionsById = definitions.ToDictionary(d => d.Id.Value);
        Guid userId = Guid.Empty;

        for (int i = 0; i < command.FieldIdsInOrder.Count; i++)
        {
            Guid fieldId = command.FieldIdsInOrder[i];

            if (!definitionsById.TryGetValue(fieldId, out CustomFieldDefinition? definition))
            {
                throw new CustomFieldException($"Field with ID '{fieldId}' not found for entity type '{command.EntityType}'");
            }

            definition.SetDisplayOrder(i, userId, timeProvider);
            await repository.UpdateAsync(definition, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}
